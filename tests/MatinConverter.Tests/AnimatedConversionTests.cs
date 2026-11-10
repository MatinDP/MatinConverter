using ImageMagick;
using MatinConverter.Core;
using Xunit;

namespace MatinConverter.Tests;

public sealed class AnimatedConversionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Matin-frames-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFormatService formats = new();
    private ImageConversionService Engine => new(formats);
    public AnimatedConversionTests() => Directory.CreateDirectory(root);

    private string Gif()
    {
        var path = Path.Combine(root, "انیمیشن.gif");
        using var images = new MagickImageCollection();
        images.Add(new MagickImage(MagickColors.Red, 4, 3) { AnimationDelay = 10, GifDisposeMethod = GifDisposeMethod.None });
        images.Add(new MagickImage(MagickColors.Blue, 1, 1) { Page = new MagickGeometry("4x3+1+1"), AnimationDelay = 10, GifDisposeMethod = GifDisposeMethod.None });
        images.Add(new MagickImage(MagickColors.Lime, 1, 1) { Page = new MagickGeometry("4x3+2+1"), AnimationDelay = 10, GifDisposeMethod = GifDisposeMethod.None });
        images.Write(path);
        return path;
    }

    [Fact]
    public async Task OptimizedGifExportsCompleteFramesAndCountsEachFrame()
    {
        var file = Gif();
        var original = File.ReadAllBytes(file);
        var reports = new List<ConversionProgress>();
        var result = await Engine.ConvertAsync([file], new(), 2, new Reports(reports.Add), CancellationToken.None);
        Assert.Equal(3, result.Succeeded);
        Assert.Equal(3, reports.Last().Completed);
        Assert.Equal(3, reports.Last().Total);
        foreach (var (output, index) in result.Files.Select((x, i) => (x.Output!, i)))
        {
            Assert.EndsWith($"-frame-{index + 1:D3}.png", output);
            using var frame = new MagickImage(output);
            Assert.Equal(4u, frame.Width);
            Assert.Equal(3u, frame.Height);
            using var pixels = frame.GetPixels();
            Assert.Equal(MagickColors.Red, pixels.GetPixel(0, 0).ToColor());
            if (index > 0) Assert.Equal(MagickColors.Blue, pixels.GetPixel(1, 1).ToColor());
            if (index > 1) Assert.Equal(MagickColors.Lime, pixels.GetPixel(2, 1).ToColor());
        }
        Assert.Equal(original, File.ReadAllBytes(file));
        var estimate = await Engine.EstimateSizeAsync(file, new(), CancellationToken.None);
        Assert.Equal(new FileInfo(result.Files[0].Output!).Length, estimate);
    }

    [Fact]
    public async Task ExistingPreserveSettingSplitsStaticOutputsAndCollisionCanSkipOneFrame()
    {
        var file = Gif();
        var existing = Path.Combine(root, "انیمیشن-frame-002.png");
        File.WriteAllText(existing, "keep");
        var result = await Engine.ConvertAsync([file], new() { Frames = FrameMode.Preserve, Collision = CollisionMode.Skip }, 2, null, CancellationToken.None);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("keep", File.ReadAllText(existing));
    }

    [Fact]
    public async Task CancelBetweenFramesKeepsCompletedOutputAndStopsRemainingFrames()
    {
        var file = Gif();
        using var cancellation = new CancellationTokenSource();
        var result = await Engine.ConvertAsync([file], new(), 1, new Reports(p =>
        {
            if (p.Result?.Success == true) cancellation.Cancel();
        }), cancellation.Token);
        Assert.True(result.Canceled);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.CanceledCount);
        Assert.Single(Directory.GetFiles(root, "*.png"));
        Assert.Empty(Directory.GetFiles(root, ".matin-*"));
    }

    [Fact]
    public void FirstFrameAndPreservedAnimationRemainAvailable()
    {
        var file = Gif();
        var first = Engine.ConvertOne(file, new() { Frames = FrameMode.FirstFrame }, CancellationToken.None);
        Assert.True(first.Success, first.Error);
        Assert.Single(first.Outputs);
        var preserved = Engine.ConvertOne(file, new() { Frames = FrameMode.Preserve, Format = "Gif" }, CancellationToken.None);
        Assert.True(preserved.Success, preserved.Error);
        Assert.Equal(3, Engine.Inspect(preserved.Output!).Frames);
    }

    [Fact]
    public void MultipageTiffAlsoSplitsIntoIndependentImages()
    {
        var file = Path.Combine(root, "pages.tiff");
        using (var pages = new MagickImageCollection())
        {
            pages.Add(new MagickImage(MagickColors.Red, 30, 20));
            pages.Add(new MagickImage(MagickColors.Blue, 50, 40));
            pages.Write(file);
        }
        var result = Engine.ConvertOne(file, new(), CancellationToken.None);
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Outputs.Count);
        using var second = new MagickImage(result.Outputs[1]);
        Assert.Equal(50u, second.Width);
    }

    [Fact]
    public void EveryAdvertisedOutputCanActuallyEncodeAndValidate()
    {
        var input = Path.Combine(root, "input.png");
        using (var image = new MagickImage(MagickColors.Red, 32, 24)) image.Write(input);
        var errors = new List<string>();
        foreach (var output in formats.OutputFormats)
        {
            var result = Engine.ConvertOne(input, new() { Format = output.Id, Destination = DestinationMode.Subfolder, Subfolder = output.Id }, CancellationToken.None);
            if (!result.Success) errors.Add($"{output.Id}: {result.Error}");
            else
            {
                var decoded = Engine.ConvertOne(result.Output!, new() { Format = "Png", Destination = DestinationMode.Subfolder, Subfolder = "decoded" }, CancellationToken.None);
                if (!decoded.Success) errors.Add($"{output.Id} decode: {decoded.Error}");
            }
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("jfif")][InlineData("jxl")][InlineData("qoi")][InlineData("psb")]
    [InlineData("dds")][InlineData("exr")][InlineData("hdr")][InlineData("pcx")]
    [InlineData("3fr")][InlineData("crw")][InlineData("dcm")][InlineData("xcf")]
    public void ExtendedInputsAppearInPickerAndFolderScan(string extension)
    {
        Assert.True(formats.IsLikelyImage("file." + extension));
        Assert.Contains("*." + extension, formats.FileFilter);
    }

    [Theory]
    [InlineData("msl")][InlineData("mvg")][InlineData("http")][InlineData("ps")][InlineData("mp4")]
    public void NonImageAndExternalDelegateFormatsAreNotAdvertised(string extension)
        => Assert.False(formats.IsLikelyImage("file." + extension));

    private sealed class Reports(Action<ConversionProgress> report) : IProgress<ConversionProgress>
    { public void Report(ConversionProgress value) => report(value); }
    public void Dispose() => Directory.Delete(root, true);
}
