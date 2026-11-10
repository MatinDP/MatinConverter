using ImageMagick;
using MatinConverter.Core;
using System.Text;
using Xunit;

namespace MatinConverter.Tests;

public sealed class PdfConversionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Matin-PDF-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFormatService formats = new();
    public PdfConversionTests() => Directory.CreateDirectory(root);

    private string Pdf()
    {
        var path = Path.Combine(root, "سند.pdf");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 36] /Resources << >> /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 36] /Resources << >> /Contents 6 0 R >>",
            Stream("1 0 0 rg 0 0 72 36 re f\n"),
            Stream("0 0 1 rg 0 0 72 36 re f\n")
        };
        static string Stream(string content) => $"<< /Length {content.Length} >>\nstream\n{content}endstream";
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append($"xref\n0 7\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllText(path, text.ToString(), Encoding.ASCII);
        return path;
    }

    [Fact]
    public async Task EveryPageBecomesSeparateImageWithCorrectPixelsAndProgress()
    {
        var source = Pdf();
        var original = File.ReadAllBytes(source);
        var engine = new ImageConversionService(formats);
        Assert.True(formats.IsLikelyImage(source));
        Assert.Contains("*.pdf", formats.FileFilter);
        Assert.Equal(2, engine.Inspect(source).Frames);
        using var thumbnail = new MagickImage(engine.Thumbnail(source));
        Assert.True(thumbnail.Width <= 80);
        var progress = new List<ConversionProgress>();
        var result = await engine.ConvertAsync([source], new() { Format = "Png", Frames = FrameMode.FirstFrame }, 2,
            new InlineProgress(progress.Add), CancellationToken.None);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(2, progress.Last().Total);
        Assert.Equal(2, progress.Last().Completed);
        for (int i = 0; i < 2; i++)
        {
            Assert.EndsWith($"-page-{i + 1:D3}.png", result.Files[i].Output);
            using var image = new MagickImage(result.Files[i].Output!);
            Assert.Equal(150u, image.Width);
            Assert.Equal(75u, image.Height);
            using var pixels = image.GetPixels();
            var color = pixels.GetPixel(30, 30).ToColor()!;
            Assert.Equal(i == 0 ? (byte)255 : (byte)0, color.R);
            Assert.Equal(i == 1 ? (byte)255 : (byte)0, color.B);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
        var estimate = await engine.EstimateSizeAsync(source, new() { Format = "Png" }, CancellationToken.None);
        Assert.Equal(new FileInfo(result.Files[0].Output!).Length, estimate);
    }

    [Fact]
    public void PdfUsesDestinationResizeAndCollisionSettings()
    {
        var source = Pdf();
        var engine = new ImageConversionService(formats);
        var options = new ConversionOptions { Format = "Jpeg", Width = 60, Height = 60, Destination = DestinationMode.Subfolder, Subfolder = "photos", Suffix = "-new" };
        var result = engine.ConvertOne(source, options, CancellationToken.None);
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Outputs.Count);
        using var image = new MagickImage(result.Outputs[0]);
        Assert.Equal(60u, image.Width);
        Assert.Equal(30u, image.Height);
        Assert.Contains("photos", result.Outputs[0]);
        Assert.EndsWith("-new-page-001.jpg", result.Outputs[0]);
        var skipped = engine.ConvertOne(source, options with { Collision = CollisionMode.Skip }, CancellationToken.None);
        Assert.True(skipped.Skipped);
        var renamed = engine.ConvertOne(source, options, CancellationToken.None);
        Assert.EndsWith("-page-001 (1).jpg", renamed.Outputs[0]);
    }

    [Fact]
    public async Task PdfNeverOverwritesAnotherSelectedSource()
    {
        var source = Pdf();
        var protectedFile = Path.Combine(root, "سند-page-001.png");
        using (var image = new MagickImage(MagickColors.Green, 20, 20)) image.Write(protectedFile);
        var original = File.ReadAllBytes(protectedFile);
        var result = await new ImageConversionService(formats).ConvertAsync([source, protectedFile],
            new() { Format = "Png", Collision = CollisionMode.Overwrite }, 2, null, CancellationToken.None);
        Assert.Equal(3, result.Succeeded);
        Assert.Equal(original, File.ReadAllBytes(protectedFile));
        Assert.Contains("-page-001 (", result.Files[0].Output);
        Assert.NotEqual(protectedFile, result.Files[0].Output);
    }

    [Fact]
    public async Task InvalidPdfFailsWithoutStoppingOtherFilesAndCancellationWritesNothing()
    {
        var bad = Path.Combine(root, "bad.pdf");
        File.WriteAllText(bad, "invalid PDF");
        var engine = new ImageConversionService(formats);
        var source = Pdf();
        var result = await engine.ConvertAsync([bad, source], new(), 2, null, CancellationToken.None);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.Succeeded);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var stopped = await engine.ConvertAsync([source], new() { Suffix = "-canceled" }, 2, null, canceled.Token);
        Assert.True(stopped.Canceled);
        Assert.Equal(2, stopped.CanceledCount);
        Assert.Empty(Directory.GetFiles(root, "*-canceled*"));
    }

    private sealed class InlineProgress(Action<ConversionProgress> report) : IProgress<ConversionProgress>
    { public void Report(ConversionProgress value) => report(value); }
    public void Dispose() => Directory.Delete(root, true);
}
