using ImageMagick;
using MatinConverter.Core;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Xunit;

namespace MatinConverter.Tests;

public sealed class ConversionTests : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existingName, IntPtr attributes);

    [Fact]
    public async Task OverwriteNeverReplacesAnotherSelectedSource()
    {
        var png = Image("same.png");
        var jpeg = Image("same.jpg", MagickColors.Blue);
        var original = SHA256.HashData(File.ReadAllBytes(jpeg));
        var result = await Engine.ConvertAsync([png, jpeg], new()
        {
            Format = "Jpeg",
            Collision = CollisionMode.Overwrite
        }, 2, null, CancellationToken.None);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(jpeg)));
        Assert.DoesNotContain(result.Files, file => file.Output == jpeg);
    }

    [Fact]
    public void OverwriteProtectsSourceThroughFilesystemAliases()
    {
        var source = Image("original.jpg");
        var original = SHA256.HashData(File.ReadAllBytes(source));
        var output = Path.Combine(root, "alias");
        Directory.CreateDirectory(output);
        var alias = Path.Combine(output, "original.jpg");
        Assert.True(CreateHardLink(alias, source, IntPtr.Zero));
        var result = Run(source, new()
        {
            Format = "Jpeg",
            Collision = CollisionMode.Overwrite,
            Destination = DestinationMode.Custom,
            OutputDirectory = output
        });
        Assert.True(result.Success, result.Error);
        Assert.NotEqual(alias, result.Output);
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(alias)));
    }
    private readonly string root = Path.Combine(Path.GetTempPath(), "MatinConverter-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFormatService formats = new();
    private ImageConversionService Engine => new(formats);
    public ConversionTests() => Directory.CreateDirectory(root);
    private string Image(string name = "تصویر تست.png", MagickColor? color = null, uint width = 80, uint height = 40)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new MagickImage(color ?? MagickColors.Red, width, height);
        image.Write(path);
        return path;
    }
    private ConversionResult Run(string input, ConversionOptions? options = null) => Engine.ConvertOne(input, options ?? new() { Format = "Jpeg" }, CancellationToken.None);
    [Fact]
    public void CenterCropAndOriginalSizeAreApplied()
    {
        var source = Image(width: 80, height: 40);
        var cropped = Run(source, new() { Format = "Jpeg", Resize = ResizeMode.Cover, Width = 20, Height = 20 });
        Assert.True(cropped.Success, cropped.Error);
        Assert.Equal(20u, Engine.Inspect(cropped.Output!).Width);
        Assert.Equal(20u, Engine.Inspect(cropped.Output!).Height);
        var original = Run(source, new() { Format = "Jpeg", Resize = ResizeMode.Original, Width = 10, Height = 10 });
        Assert.True(original.Success, original.Error);
        Assert.Equal(80u, Engine.Inspect(original.Output!).Width);
    }
    [Fact]
    public void TargetSizeIsHonoredOrReportedWithoutIncompleteOutput()
    {
        var source = Image(width: 1000, height: 1000);
        var success = Run(source, new() { Format = "Jpeg", TargetSizeKiB = 20 });
        Assert.True(success.Success, success.Error);
        Assert.InRange(new FileInfo(success.Output!).Length, 1, 20 * 1024);
        var failure = Run(source, new() { Format = "Jpeg", TargetSizeKiB = 1, MinimumQuality = 85 });
        Assert.NotNull(failure.Error);
        Assert.Empty(Directory.GetFiles(root, ".matin-*.tmp*"));
    }
    [Fact]
    public void LongJpegExtensionProducesRealJpeg()
    {
        var result = Run(Image(), new() { Format = "JpegLong" });
        Assert.True(result.Success, result.Error);
        Assert.EndsWith(".jpeg", result.Output!);
        Assert.Equal("Jpeg", Engine.Inspect(result.Output!).Format);
    }
    [Theory]
    [InlineData("Jpeg")]
    [InlineData("Png")]
    [InlineData("WebP")]
    [InlineData("Avif")]
    [InlineData("Bmp")]
    [InlineData("Tiff")]
    [InlineData("Gif")]
    [InlineData("Ico")]
    [InlineData("Jp2")]
    [InlineData("Tga")]
    [InlineData("Ppm")]
    public void AdvertisedEncoderReallyWrites(string format)
    {
        if (!formats.OutputFormats.Any(x => x.Id == format))
            return;
        var input = Image();
        var original = SHA256.HashData(File.ReadAllBytes(input));
        var result = Run(input, new()
        {
            Format = format
        });
        Assert.True(result.Success, result.Error);
        using var converted = new MagickImage(result.Output!);
        Assert.True(converted.Width > 0);
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(input)));
    }
    [Fact]
    public void TransparencyToJpegUsesChosenBackground()
    {
        var result = Run(Image(color: MagickColors.Transparent), new()
        {
            Format = "Jpeg",
            Background = "#00FF00",
            Quality = 100
        });
        Assert.True(result.Success, result.Error);
        using var image = new MagickImage(result.Output!);
        var color = image.GetPixels().GetPixel(0, 0).ToColor()!;
        Assert.True(color.G > 240 && color.R < 10 && color.B < 10, color.ToString());
    }
    [Fact]
    public void JpegWebpPngRoundTrip()
    {
        var jpg = Run(Image());
        var webp = Run(jpg.Output!, new()
        {
            Format = "WebP"
        });
        var png = Run(webp.Output!, new()
        {
            Format = "Png"
        });
        Assert.True(png.Success, png.Error);
        Assert.Equal(80u, Engine.Inspect(png.Output!).Width);
    }
    [Fact]
    public void ResizePreservesAspectAndPreventsEnlargement()
    {
        var input = Image();
        var small = Run(input, new()
        {
            Width = 20,
            Height = 20
        });
        Assert.Equal(new ImageDetails(20, 10, 1, "Png"), Engine.Inspect(small.Output!));
        var large = Run(input, new()
        {
            Width = 800
        });
        Assert.Equal(80u, Engine.Inspect(large.Output!).Width);
        var allowed = Run(input, new()
        {
            Width = 160,
            AllowEnlargement = true
        });
        Assert.Equal(160u, Engine.Inspect(allowed.Output!).Width);
    }
    [Fact]
    public void OrientationIsCorrectedBeforeStrippingMetadata()
    {
        var input = Image("orientation.jpg");
        using (var image = new MagickImage(input))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif);
            image.Orientation = Orientation.RightTop;
            image.Write(input);
        }
        var result = Run(input, new()
        {
            Format = "Png",
            StripMetadata = true
        });
        Assert.True(result.Success, result.Error);
        Assert.Equal(40u, Engine.Inspect(result.Output!).Width);
    }
    [Fact]
    public void DuplicateCollisionPoliciesAndSameFormatAreSafe()
    {
        var input = Image();
        var hash = SHA256.HashData(File.ReadAllBytes(input));
        var one = Run(input);
        var two = Run(input);
        Assert.NotEqual(one.Output, two.Output);
        var skip = Run(input, new()
        {
            Format = "Jpeg",
            Collision = CollisionMode.Skip
        });
        Assert.True(skip.Skipped);
        var overwrite = Run(input, new()
        {
            Format = "Jpeg",
            Collision = CollisionMode.Overwrite
        });
        Assert.Equal(one.Output, overwrite.Output);
        var same = Run(input, new()
        {
            Format = "Png",
            Collision = CollisionMode.Overwrite
        });
        Assert.NotEqual(input, same.Output);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(input)));
    }
    [Fact]
    public async Task BatchContinuesAfterCorruptImageAndUsesMultipleSourceFolders()
    {
        var a = Image("یک/a.png");
        var b = Image("دو/b.png");
        var bad = Path.Combine(root, "corrupt.png");
        File.WriteAllText(bad, "invalid image");
        var result = await Engine.ConvertAsync([a, b, bad], new()
        {
            Format = "Jpeg"
        }, 2, null, CancellationToken.None);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.OutputDirectories.Count);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task BatchCountAndProgressAreAccurate(int count)
    {
        var files = Enumerable.Range(0, count).Select(i => Image($"batch-{i}.png")).ToArray();
        var observations = new List<ConversionProgress>();
        var result = await Engine.ConvertAsync(files, new()
        {
            Format = "WebP"
        }, 2, new CallbackProgress(p => observations.Add(p)), CancellationToken.None);
        Assert.Equal(count, result.Succeeded);
        Assert.Equal(count, observations.Last().Completed);
        Assert.All(observations, p => Assert.InRange(p.Completed, 0, count));
    }
    [Fact]
    public async Task PreCanceledBatchWritesNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var input = Image();
        var result = await Engine.ConvertAsync([input], new()
        {
            Format = "Jpeg"
        }, 2, null, cts.Token);
        Assert.True(result.Canceled);
        Assert.False(File.Exists(Path.ChangeExtension(input, "jpg")));
    }
    [Fact]
    public async Task CancellationDuringBatchLeavesNoTemporaryOutputs()
    {
        var inputs = Enumerable.Range(0, 30).Select(i => Image($"cancel-{i}.png")).ToArray();
        using var cts = new CancellationTokenSource();
        var result = await Engine.ConvertAsync(inputs, new()
        {
            Format = "Jpeg"
        }, 1, new CallbackProgress(p => { if (p.Completed >= 2) cts.Cancel(); }), cts.Token);
        Assert.True(result.Canceled);
        Assert.InRange(result.Succeeded, 1, 3);
        Assert.Empty(Directory.GetFiles(root, ".matin-*.tmp"));
    }
    [Fact]
    public void MissingCustomFolderIsCreatedAndSubfoldersArePredictable()
    {
        var input = Image();
        var custom = Path.Combine(root, "new", "output");
        var result = Run(input, new()
        {
            Format = "Jpeg",
            Destination = DestinationMode.Custom,
            OutputDirectory = custom
        });
        Assert.True(result.Success, result.Error);
        Assert.Equal(custom, Path.GetDirectoryName(result.Output));
        var sub = Run(input, new()
        {
            Format = "Jpeg",
            Destination = DestinationMode.Subfolder,
            Subfolder = "نتیجه"
        });
        Assert.Contains("نتیجه", sub.Output!);
    }
    [Fact]
    public void UnwritableDestinationReturnsFailureAndPreservesSource()
    {
        var input = Image();
        var blocker = Path.Combine(root, "not-a-folder");
        File.WriteAllText(blocker, "block");
        var result = Run(input, new()
        {
            Destination = DestinationMode.Custom,
            OutputDirectory = blocker
        });
        Assert.NotNull(result.Error);
        Assert.True(File.Exists(input));
    }
    [Fact]
    public void UnknownEncoderIsNotAdvertisedOrAccepted()
    {
        Assert.Throws<NotSupportedException>(() => formats.GetOutput("ImaginaryEncoder"));
        var result = Run(Image(), new()
        {
            Format = "ImaginaryEncoder"
        });
        Assert.NotNull(result.Error);
    }
    [Fact]
    public void MultiFrameIsPreservedOrExplicitlySelected()
    {
        var input = Path.Combine(root, "animation.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 20, 20) { AnimationDelay = 10 });
            frames.Add(new MagickImage(MagickColors.Blue, 20, 20) { AnimationDelay = 10 });
            frames.Write(input);
        }
        var split = Run(input, new()
        {
            Format = "Jpeg"
        });
        Assert.True(split.Success, split.Error);
        Assert.Equal(2, split.Outputs.Count);
        var first = Run(input, new()
        {
            Format = "Png",
            Frames = FrameMode.FirstFrame
        });
        Assert.True(first.Success, first.Error);
        Assert.Equal(1, Engine.Inspect(first.Output!).Frames);
        var preserved = Run(input, new()
        {
            Format = "Tiff", Frames = FrameMode.Preserve
        });
        Assert.True(preserved.Success, preserved.Error);
        Assert.Equal(2, Engine.Inspect(preserved.Output!).Frames);
    }
    [Fact]
    public async Task ParallelSameNameOutputsAreNotLost()
    {
        var files = Enumerable.Range(0, 10).Select(i => Image($"folder-{i}/same.png")).ToArray();
        var result = await Engine.ConvertAsync(files, new()
        {
            Format = "Jpeg",
            Destination = DestinationMode.Custom,
            OutputDirectory = Path.Combine(root, "out")
        }, 4, null, CancellationToken.None);
        Assert.Equal(10, result.Succeeded);
        Assert.Equal(10, result.Files.Select(x => x.Output).Distinct().Count());
    }
    [Fact]
    public void JfifIsAcceptedAndThumbnailIsSmall()
    {
        Assert.True(formats.IsLikelyImage("file.jfif"));
        var thumbnail = Engine.Thumbnail(Image(width: 1000, height: 500));
        using var image = new MagickImage(thumbnail);
        Assert.True(image.Width <= 80);
    }
    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
    private sealed class CallbackProgress(Action<ConversionProgress> callback) : IProgress<ConversionProgress>
    {
        public void Report(ConversionProgress value) => callback(value);
    }
}
