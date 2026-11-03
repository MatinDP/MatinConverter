using ImageMagick;
using System.Collections.Concurrent;

namespace MatinConverter.Core;

public sealed class ImageConversionService(ImageFormatService formats)
{
    private const ulong MaximumPixels = 100_000_000;
    public ImageDetails Inspect(string file)
    {
        using var info = new MagickImageCollection();
        info.Ping(file);
        if (info.Count == 0)
            throw new InvalidDataException("No image frames found.");
        if (info.Count > 256 || info.Sum(x => (double)x.Width * x.Height) > MaximumPixels)
            throw new InvalidDataException("This image exceeds the 100 megapixel / 256 frame safety limit.");
        return new(info[0].Width, info[0].Height, info.Count, info[0].Format.ToString());
    }

    public byte[] Thumbnail(string file)
    {
        _ = Inspect(file);
        var read = new MagickReadSettings { FrameCount = 1, Width = 96, Height = 96 };
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
        _ = formats.GetOutput(options.Format);
        var jobId = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow;
        var files = sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        AppLog.Write("job-start", $"id={jobId}; files={files.Length}; format={options.Format}");
        var protection = await Task.Run(() => new SourceProtection(files, options.Collision == CollisionMode.Overwrite));
        var results = new ConcurrentDictionary<string, ConversionResult>(StringComparer.OrdinalIgnoreCase);
        int completed = 0, success = 0, failed = 0, skipped = 0;
        var gate = new object();
        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(parallelism, 1, 8), CancellationToken = cancellationToken }, async (file, token) =>
            {
                lock (gate)
                    progress?.Report(new(completed, files.Length, success, failed, skipped, file, null));
                var result = await Task.Run(() => ConvertOne(file, options, token, protection), CancellationToken.None);
                results[file] = result;
                lock (gate)
                {
                    completed++;
                    if (result.Success)
                        success++;
                    if (result.Error is not null)
                        failed++;
                    if (result.Skipped)
                        skipped++;
                    progress?.Report(new(completed, files.Length, success, failed, skipped, file, result));
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        var batch = new BatchResult(files.Select(f => results.GetValueOrDefault(f) ?? new ConversionResult(f, null, null, Canceled: true)).ToArray(), cancellationToken.IsCancellationRequested)
        { JobId = jobId, CreatedAt = started, StartedAt = started, CompletedAt = DateTimeOffset.UtcNow };
        AppLog.Write("job-finish", $"id={jobId}; status={batch.Status}; success={batch.Succeeded}; failed={batch.Failed}; canceled={batch.CanceledCount}; durationMs={(batch.CompletedAt - started).TotalMilliseconds:0}");
        return batch;
    }

    public ConversionResult ConvertOne(string source, ConversionOptions options, CancellationToken token) => ConvertOne(source, options, token, null);

    private ConversionResult ConvertOne(string source, ConversionOptions options, CancellationToken token, SourceProtection? protection)
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
            var details = Inspect(source);
            if (details.Frames > 1 && !format.MultiFrame && options.Frames != FrameMode.FirstFrame)
                throw new InvalidDataException("This file has multiple frames. Choose 'First frame' or an animated / multipage output format.");
            using var images = new MagickImageCollection();
            if (options.Frames == FrameMode.FirstFrame)
                images.Read(source, new MagickReadSettings { FrameCount = 1 });
            else
                images.Read(source);
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
            var baseName = Path.GetFileNameWithoutExtension(source) + options.Suffix;
            var destination = Path.Combine(directory, baseName + "." + format.Extension);
            var sourceConflict = protection.Contains(destination, options.Collision == CollisionMode.Overwrite);
            if (File.Exists(destination) && options.Collision == CollisionMode.Skip && !sourceConflict)
                return new(source, null, null, Skipped: true);
            temporary = Path.Combine(directory, ".matin-" + Guid.NewGuid().ToString("N") + ".tmp");
            TargetSizeEncoder.Write(images, temporary, format, options, token);
            token.ThrowIfCancellationRequested();
            using (var verify = new MagickImageCollection())
            {
                verify.Ping(temporary, new MagickReadSettings { Format = Enum.Parse<MagickFormat>(format.EncoderId) });
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
            return new(source, null, ex.Message);
        }
        finally { if (temporary is not null && File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    }
}
