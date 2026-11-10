using ImageMagick;
using MatinConverter.Core;
using Xunit;

namespace MatinConverter.Tests;

public sealed class SizeEstimateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Matin-size-" + Guid.NewGuid().ToString("N"));
    private readonly ImageConversionService engine = new(new ImageFormatService());
    public SizeEstimateTests() => Directory.CreateDirectory(root);

    private string Noise()
    {
        var file = Path.Combine(root, "عکس.png");
        var pixels = new byte[256 * 256 * 3];
        new Random(42).NextBytes(pixels);
        using var image = new MagickImage();
        image.ReadPixels(pixels, new PixelReadSettings(256, 256, StorageType.Char, PixelMapping.RGB));
        image.Write(file);
        return file;
    }

    [Fact]
    public async Task QualityChangesSizeAndEstimatesMatchActualEncoding()
    {
        var source = Noise();
        var original = File.ReadAllBytes(source);
        var low = new ConversionOptions { Format = "Jpeg", Quality = 25 };
        var high = low with { Quality = 95 };
        var lowSize = await engine.EstimateSizeAsync(source, low, CancellationToken.None);
        var highSize = await engine.EstimateSizeAsync(source, high, CancellationToken.None);
        Assert.True(lowSize < highSize);
        Assert.Single(Directory.GetFiles(root));
        var actual = engine.ConvertOne(source, high, CancellationToken.None);
        Assert.True(actual.Success, actual.Error);
        Assert.Equal(new FileInfo(actual.Output!).Length, highSize);
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task ResizeAndLosslessOutputAlsoHaveValidSizeEstimates()
    {
        var source = Noise();
        var original = await engine.EstimateSizeAsync(source, new(), CancellationToken.None);
        var options = new ConversionOptions { Width = 64, Height = 64 };
        var resized = await engine.EstimateSizeAsync(source, options, CancellationToken.None);
        Assert.True(resized < original);
        var actual = engine.ConvertOne(source, options, CancellationToken.None);
        Assert.Equal(new FileInfo(actual.Output!).Length, resized);
    }

    [Fact]
    public async Task CancellationAndInvalidInputsLeaveNoPreviewFiles()
    {
        var source = Noise();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.EstimateSizeAsync(source, new(), canceled.Token));
        var invalid = Path.Combine(root, "invalid.png");
        File.WriteAllText(invalid, "invalid image");
        await Assert.ThrowsAsync<InvalidDataException>(() => engine.EstimateSizeAsync(invalid, new(), CancellationToken.None));
        Assert.Equal(2, Directory.GetFiles(root).Length);
        Assert.Empty(Directory.GetDirectories(root));
    }

    public void Dispose() => Directory.Delete(root, true);
}
