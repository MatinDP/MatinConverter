using ImageMagick;

namespace MatinConverter.Core;

public sealed class ImageFormatService
{
    private static readonly OutputFormat[] Candidates =
    [new("Jpeg","jpg",true,false),new("JpegLong","jpeg",true,false,"Jpeg"),new("Png","png",false,false),new("WebP","webp",true,true),new("Avif","avif",true,true),
     new("Bmp","bmp",false,false),new("Tiff","tiff",false,true),new("Gif","gif",false,true),new("Ico","ico",false,false),
     new("Jp2","jp2",true,false),new("Tga","tga",false,false),new("Ppm","ppm",false,false)];
    private readonly HashSet<string> readable;
    public IReadOnlyList<OutputFormat> OutputFormats
    {
        get;
    }
    public ImageFormatService()
    {
        ImageRuntime.Initialize();
        var formats = MagickNET.SupportedFormats.ToArray();
        readable = formats.Where(x => x.SupportsReading).Select(x => x.Format.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        OutputFormats = Candidates.Where(c => formats.Any(f => f.Format.ToString().Equals(c.EncoderId, StringComparison.OrdinalIgnoreCase) && f.SupportsWriting)).ToArray();
    }
    public OutputFormat GetOutput(string id) => OutputFormats.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new NotSupportedException($"Output encoder '{id}' is unavailable in this build.");
    public bool IsLikelyImage(string file)
    {
        var ext = Path.GetExtension(file).TrimStart('.');
        ext = ext.ToLowerInvariant() switch
        {
            "jpg" or "jpe" or "jfif" => "jpeg",
            "tif" => "tiff",
            "svgz" => "svg",
            _ => ext
        };
        return readable.Contains(ext);
    }
    public string FileFilter => "Image files|" + string.Join(';', new[] { "jpg", "jpeg", "jfif", "png", "webp", "avif", "heic", "heif", "gif", "bmp", "tif", "tiff", "ico", "svg", "svgz", "psd", "tga", "jp2", "dng", "cr2", "cr3", "nef", "arw", "orf", "raf", "rw2" }.Where(e => IsLikelyImage("a." + e)).Select(e => "*." + e)) + "|All files (*.*)|*.*";
}

public static class ImageRuntime
{
    private static readonly Lazy<bool> Init = new(() =>
    {
        Directory.CreateDirectory(SettingsService.TempDirectory);
        MagickNET.SetTempDirectory(SettingsService.TempDirectory);
        var configuration = ImageMagick.Configuration.ConfigurationFiles.Default;
        configuration.Policy.Data = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "policy.xml"));
        MagickNET.Initialize(configuration);
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.ListLength = 256;
        ResourceLimits.Area = 100_000_000;
        ResourceLimits.Disk = 1024UL * 1024 * 1024;
        ResourceLimits.Thread = 1;
        ResourceLimits.Width = 32768;
        ResourceLimits.Height = 32768;
        return true;
    });
    public static void Initialize() => _ = Init.Value;
}
