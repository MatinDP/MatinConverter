using ImageMagick;
using System.Collections.Concurrent;

namespace MatinConverter.Core;

public sealed class ImageConversionService(ImageFormatService formats)
{
    private const ulong MaximumPixels = 100_000_000;
    private static readonly SemaphoreSlim EstimateGate = new(1);

    public async Task<long> EstimateSizeAsync(string source, ConversionOptions options, CancellationToken token)
    {
        var snapshot = options with { Frames = FrameMode.FirstFrame, Suffix = "", Collision = CollisionMode.Rename };
        await EstimateGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var directory = Path.Combine(SettingsService.TempDirectory, "estimate-" + Guid.NewGuid().ToString("N"));
                snapshot.Destination = DestinationMode.Custom;
                snapshot.OutputDirectory = directory;
                try
                {
                    using var pdf = PdfDocument.IsPdf(source) ? new PdfDocument(source) : null;
                    var result = ConvertOne(source, snapshot, token, null, pdf is null ? null : 0, pdf);
                    token.ThrowIfCancellationRequested();
                    if (!result.Success) throw new InvalidDataException(result.Error ?? "Unable to estimate output size.");
                    return new FileInfo(result.Output!).Length;
                }
                finally
                {
                    // Only this request's generated files are stored in this directory.
                    if (Directory.Exists(directory))
                    {
                        foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                        Directory.Delete(directory);
                    }
                }
            }, token).ConfigureAwait(false);
        }
        finally { EstimateGate.Release(); }
    }
    public ImageDetails Inspect(string file)
    {
        if (PdfDocument.IsPdf(file))
        {
            using var pdf = new PdfDocument(file);
            var size = pdf.Size(0);
            return new(size.Width, size.Height, pdf.PageCount, "PDF");
        }
        using var info = new MagickImageCollection();
        info.Ping(file, ImageFormatService.ReadSettings(file));
        if (info.Count == 0)
            throw new InvalidDataException("No image frames found.");
        if (info.Count > 10000 || info.Sum(x => (double)Math.Max(x.Width, x.Page.Width) * Math.Max(x.Height, x.Page.Height)) > MaximumPixels)
            throw new InvalidDataException("This image exceeds the 100 megapixel / 10000 frame safety limit.");
        return new(info[0].Width, info[0].Height, info.Count, info[0].Format.ToString());
    }

    public byte[] Thumbnail(string file)
    {
        if (PdfDocument.IsPdf(file))
        {
            using var pdf = new PdfDocument(file);
            using var preview = pdf.Render(0, CancellationToken.None, thumbnail: true);
            return preview.ToByteArray(MagickFormat.Png);
        }
        _ = Inspect(file);
        var read = ImageFormatService.ReadSettings(file);
        read.FrameCount = 1; read.Width = 96; read.Height = 96;
        read.SetDefine(MagickFormat.Jpeg, "size", "96x96");
        using var image = new MagickImage(file, read);
        image.AutoOrient();
        image.Thumbnail(80, 80);
        image.Strip();
        return image.ToByteArray(MagickFormat.Png);
    }

    public async Task<BatchResult> ConvertAsync(IEnumerable<string> sources, ConversionOptions options, int parallelism, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        options = options with { };
        options.Validate();
        var outputFormat = formats.GetOutput(options.Format);
        var jobId = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow;
        var files = sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        AppLog.Write("job-start", $"id={jobId}; files={files.Length}; format={options.Format}");
        var protection = await Task.Run(() => new SourceProtection(files, options.Collision == CollisionMode.Overwrite)).ConfigureAwait(false);
        var work = await Task.Run(() => files.SelectMany(file =>
        {
            try
            {
                var details = Inspect(file);
                var kind = PdfDocument.IsPdf(file) ? "page" : "frame";
                if (kind == "page" || details.Frames > 1 && (options.Frames == FrameMode.Split || options.Frames == FrameMode.Preserve && !outputFormat.MultiFrame))
                    return Enumerable.Range(0, details.Frames).Select(part => (File: file, Part: (int?)part, Kind: kind)).ToArray();
            }
            catch (Exception ex) when (ex is MagickException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            return [(File: file, Part: (int?)null, Kind: "")];
        }).ToArray()).ConfigureAwait(false);
        var results = new ConcurrentDictionary<int, ConversionResult>();
        int completed = 0, success = 0, failed = 0, skipped = 0;
        var gate = new object();
        try
        {
            var groups = work.Select((item, index) => (Item: item, Index: index)).GroupBy(x => x.Item.File, StringComparer.OrdinalIgnoreCase);
            await Parallel.ForEachAsync(groups, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(parallelism, 1, 8), CancellationToken = cancellationToken }, async (group, token) =>
            {
                await Task.Run(() =>
                {
                    using var frames = new MagickImageCollection();
                    PdfDocument? pdf = null;
                    Exception? loadError = null;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        if (group.First().Item.Kind == "page") pdf = new PdfDocument(group.Key);
                        else if (group.First().Item.Kind == "frame")
                        {
                            frames.Read(group.Key, ImageFormatService.ReadSettings(group.Key));
                            if (frames[0].Format is MagickFormat.Gif or MagickFormat.WebP or MagickFormat.Mng or MagickFormat.APng)
                                frames.Coalesce();
                        }
                    }
                    catch (Exception ex) when (ex is MagickException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OperationCanceledException) { loadError = ex; }
                    try
                    {
                        foreach (var entry in group)
                        {
                            if (token.IsCancellationRequested) break;
                            var (file, part, kind) = entry.Item;
                            var current = part.HasValue ? $"{file} · {kind} {part + 1}" : file;
                            lock (gate) progress?.Report(new(completed, work.Length, success, failed, skipped, current, null));
                            var result = token.IsCancellationRequested ? new ConversionResult(file, null, null, Canceled: true)
                                : loadError is not null ? new ConversionResult(file, null, loadError.Message)
                                : ConvertOne(file, options, token, protection, part, pdf, kind == "frame" ? frames[part!.Value] : null);
                            results[entry.Index] = result;
                            lock (gate)
                            {
                                completed++;
                                if (result.Success) success++;
                                if (result.Error is not null) failed++;
                                if (result.Skipped) skipped++;
                                progress?.Report(new(completed, work.Length, success, failed, skipped, current, result));
                            }
                        }
                    }
                    finally { pdf?.Dispose(); }
                }, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        var batch = new BatchResult(work.Select((item, index) => results.GetValueOrDefault(index) ?? new ConversionResult(item.File, null, null, Canceled: true)).ToArray(), cancellationToken.IsCancellationRequested)
        { JobId = jobId, CreatedAt = started, StartedAt = started, CompletedAt = DateTimeOffset.UtcNow };
        AppLog.Write("job-finish", $"id={jobId}; status={batch.Status}; success={batch.Succeeded}; failed={batch.Failed}; canceled={batch.CanceledCount}; durationMs={(batch.CompletedAt - started).TotalMilliseconds:0}");
        return batch;
    }

    public ConversionResult ConvertOne(string source, ConversionOptions options, CancellationToken token)
    {
        try
        {
            var batch = ConvertAsync([source], options, 1, null, token).GetAwaiter().GetResult();
            return new(source, batch.Files.FirstOrDefault(x => x.Success)?.Output,
                batch.Files.FirstOrDefault(x => x.Error is not null)?.Error,
                Skipped: batch.Files.All(x => x.Skipped), Canceled: batch.Canceled)
            { Outputs = batch.Files.Where(x => x.Output is not null).Select(x => x.Output!).ToArray() };
        }
        catch (Exception ex) when (ex is MagickException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(source, null, ex.Message); }
    }

    private ConversionResult ConvertOne(string source, ConversionOptions options, CancellationToken token, SourceProtection? protection, int? part = null, PdfDocument? pdf = null, IMagickImage<byte>? frame = null)
    {
        string? temporary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            options = options with { };
            source = Path.GetFullPath(source);
            protection ??= new SourceProtection([source], options.Collision == CollisionMode.Overwrite);
            options.Validate();
            var format = formats.GetOutput(options.Format);
            var details = part.HasValue ? new ImageDetails(0, 0, 1, pdf is null ? "Frame" : "PDF") : Inspect(source);
            if (details.Frames > 1 && !format.MultiFrame && options.Frames != FrameMode.FirstFrame)
                throw new InvalidDataException("This file has multiple frames. Choose 'First frame' or an animated / multipage output format.");
            using var images = new MagickImageCollection();
            if (pdf is not null) images.Add(pdf.Render(part!.Value, token));
            else if (frame is not null) images.Add(frame.Clone());
            else if (options.Frames == FrameMode.FirstFrame)
            {
                var read = ImageFormatService.ReadSettings(source);
                read.FrameCount = 1;
                images.Read(source, read);
            }
            else
                images.Read(source, ImageFormatService.ReadSettings(source));
            if (details.Frames > 1 && details.Format is "Gif" or "WebP")
                images.Coalesce();
            foreach (var image in images)
            {
                token.ThrowIfCancellationRequested();
                image.Progress += (_, e) => e.Cancel = token.IsCancellationRequested;
                image.AutoOrient();
                ImageResize.Apply(image, options);
                if (format.EncoderId == "Ico")
                    image.Resize(new MagickGeometry(256, 256) { Greater = true });
                if (format.EncoderId == "Jpeg")
                {
                    image.ColorSpace = ColorSpace.sRGB;
                    image.ColorType = ColorType.TrueColorAlpha;
                    image.BackgroundColor = new MagickColor(options.Background);
                    image.Alpha(AlphaAction.Remove);
                }
                if (options.StripMetadata)
                    image.Strip();
                image.Quality = (uint)options.Quality;
            }
            var directory = options.Destination switch
            {
                DestinationMode.Custom => Path.GetFullPath(options.OutputDirectory),
                DestinationMode.Subfolder => Path.Combine(Path.GetDirectoryName(source)!, options.Subfolder),
                _ => Path.GetDirectoryName(source)!
            };
            Directory.CreateDirectory(directory);
            var baseName = Path.GetFileNameWithoutExtension(source) + options.Suffix + (part.HasValue ? $"-{(pdf is null ? "frame" : "page")}-{part.Value + 1:D3}" : "");
            var destination = Path.Combine(directory, baseName + "." + format.Extension);
            var sourceConflict = protection.Contains(destination, options.Collision == CollisionMode.Overwrite);
            if (File.Exists(destination) && options.Collision == CollisionMode.Skip && !sourceConflict)
                return new(source, null, null, Skipped: true);
            temporary = Path.Combine(directory, ".matin-" + Guid.NewGuid().ToString("N") + ".tmp");
            TargetSizeEncoder.Write(images, temporary, format, options, token);
            token.ThrowIfCancellationRequested();
            using (var verify = new MagickImageCollection())
            {
                verify.Ping(temporary, new MagickReadSettings { Format = format.EncoderId is "Svg" or "Svgz" ? MagickFormat.Rsvg : Enum.Parse<MagickFormat>(format.EncoderId) });
                if (verify.Count == 0 || verify[0].Width == 0 || (options.Frames == FrameMode.Preserve && format.MultiFrame && verify.Count != images.Count))
                    throw new InvalidDataException("Output validation failed.");
            }
            for (int index = 0; ; index++)
            {
                token.ThrowIfCancellationRequested();
                var candidate = index == 0 ? destination : Path.Combine(directory, $"{baseName} ({index}).{format.Extension}");
                if (protection.Contains(candidate, options.Collision == CollisionMode.Overwrite))
                    continue;
                if (File.Exists(candidate) && options.Collision == CollisionMode.Skip)
                    return new(source, null, null, Skipped: true);
                try
                {
                    File.Move(temporary!, candidate, options.Collision == CollisionMode.Overwrite && !sourceConflict);
                    temporary = null;
                    return new(source, candidate, null);
                }
                catch (IOException) when (File.Exists(candidate) && (options.Collision == CollisionMode.Rename || sourceConflict)) { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return new(source, null, null, Canceled: true); }
        catch (Exception ex) when (ex is MagickException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (token.IsCancellationRequested)
                return new(source, null, null, Canceled: true);
            AppLog.Write("conversion", ex.Message);
            return new(source, null, part.HasValue ? $"{(pdf is null ? "Frame" : "PDF page")} {part.Value + 1}: {ex.Message}" : ex.Message);
        }
        finally { if (temporary is not null && File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    }
}
