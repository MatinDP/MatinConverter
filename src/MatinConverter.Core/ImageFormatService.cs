using ImageMagick;

namespace MatinConverter.Core;

public sealed class ImageFormatService
{
    // File formats that contain their own image dimensions; excludes pseudo-images,
    // raw pixel buffers, scripts and formats requiring external delegates.
    private const string FileFormats = "3fr aai art arw ase aseprite avci avif avs bmp bmp2 bmp3 cal cals cin cr2 cr3 crw cur cut dcm dcr dcx dds dib dng dpx dxt1 dxt5 emf erf exr farbfeld ff fff fits fl32 fts gif gif87 hdr heic heif hrz icb icn ico icon iiq ipl j2c j2k jng jnx jp2 jpc jpe jpeg jpg jpm jps jpt jxl k25 kdc mac mat mdc mef miff mng mos mpo mrw mtv nef nrw ora orf otb palm pam pbm pcd pcds pct pcx pdb pef pes pfm pgm pgx phm picon pict pix pjpeg png png00 png24 png32 png48 png64 png8 pnm ppm psb psd ptif pwp qoi raf ras rgf rla rle rmf rw2 rwl scr sct sf3 sfw sgi six sixel sr2 srf srw sti sun svg svgz tga tiff tiff64 tim tm2 vda vicar viff vips vst wbinfo wbmp webp wmf wpg x3f xbm xcf xpm xv";
    private const string ExtraOutputs = "Jxl Qoi Dds Exr Hdr Psd Psb Pcx Dcx Pbm Pgm Pam Pnm Pfm Phm Pgx Png8 Png24 Png32 Bmp2 Bmp3 Tiff64 Dpx Cin Sgi Ras Sun Cur Xbm Xpm Wbmp Miff Mng Jng Fits Viff Mtv Otb Palm Pcd Pict Farbfeld Aai Avs Cal Svg Svgz";
    private readonly HashSet<string> extensions;
    public IReadOnlyList<string> InputExtensions { get; }
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
        extensions = FileFormats.Split(' ').Where(e => readable.Contains(e == "3fr" ? "ThreeFr" : e)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        extensions.UnionWith(["pdf", "jfif", "tif"]);
        InputExtensions = extensions.Order(StringComparer.OrdinalIgnoreCase).Select(e => "." + e).ToArray();
        OutputFormats = Candidates.Concat(ExtraOutputs.Split(' ').Select(id =>
            new OutputFormat(id, id.ToLowerInvariant(), id is "Jxl", formats.Any(f => f.Format.ToString() == id && f.SupportsMultipleFrames))))
            .Where(c => formats.Any(f => f.Format.ToString().Equals(c.EncoderId, StringComparison.OrdinalIgnoreCase) && f.SupportsWriting)).ToArray();
    }
    public OutputFormat GetOutput(string id) => OutputFormats.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new NotSupportedException($"Output encoder '{id}' is unavailable in this build.");
    internal static MagickReadSettings ReadSettings(string file) => new()
    {
        Format = Path.GetExtension(file).ToLowerInvariant() is ".svg" or ".svgz" ? MagickFormat.Rsvg : MagickFormat.Unknown
    };
    public bool IsLikelyImage(string file)
    {
        return extensions.Contains(Path.GetExtension(file).TrimStart('.'));
    }
    public string FileFilter => "Images and PDF|" + string.Join(';', InputExtensions.Select(e => "*" + e)) + "|All files (*.*)|*.*";
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
        ResourceLimits.ListLength = 10000;
        ResourceLimits.Area = 100_000_000;
        ResourceLimits.Disk = 1024UL * 1024 * 1024;
        ResourceLimits.Thread = 1;
        ResourceLimits.Width = 32768;
        ResourceLimits.Height = 32768;
        return true;
    });
    public static void Initialize() => _ = Init.Value;
}
