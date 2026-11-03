namespace MatinConverter.Core;

public enum DestinationMode
{
    Source, Custom, Subfolder
}
public enum CollisionMode
{
    Rename, Skip, Overwrite
}
public enum FrameMode
{
    Preserve, FirstFrame
}
public enum AppTheme
{
    System, Light, Dark
}
public enum ResizeMode { FitInside, Original, Cover, Exact }
public enum JobStatus { Queued, Preparing, Running, Completed, PartiallyCompleted, Failed, Cancelling, Canceled }

public static class ProductInfo
{
    public static string Version => typeof(ProductInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}

public sealed record ConversionOptions
{
    public string Format { get; set; } = "Png";
    public int Quality { get; set; } = 85;
    public DestinationMode Destination { get; set; } = DestinationMode.Source;
    public string OutputDirectory { get; set; } = "";
    public string Subfolder { get; set; } = "Converted";
    public int Width
    {
        get; set;
    }
    public int Height
    {
        get; set;
    }
    public bool PreserveAspect { get; set; } = true;
    public ResizeMode Resize { get; set; } = ResizeMode.FitInside;
    public int TargetSizeKiB { get; set; }
    public int MinimumQuality { get; set; } = 35;
    public bool AllowTargetDownsize { get; set; }
    public bool AllowEnlargement
    {
        get; set;
    }
    public string Background { get; set; } = "#FFFFFF";
    public bool StripMetadata
    {
        get; set;
    }
    public FrameMode Frames { get; set; } = FrameMode.Preserve;
    public string Suffix { get; set; } = "";
    public CollisionMode Collision { get; set; } = CollisionMode.Rename;

    public void Validate()
    {
        if (Quality is < 1 or > 100)
            throw new ArgumentException("Quality must be between 1 and 100.");
        if (Width is < 0 or > 32768 || Height is < 0 or > 32768)
            throw new ArgumentException("Dimensions must be between 0 and 32768.");
        if (!Enum.IsDefined(Destination) || !Enum.IsDefined(Collision) || !Enum.IsDefined(Frames) || !Enum.IsDefined(Resize))
            throw new ArgumentException("Invalid conversion option.");
        if (TargetSizeKiB is < 0 or > 1_048_576 || MinimumQuality is < 1 or > 100 || (TargetSizeKiB > 0 && MinimumQuality > Quality))
            throw new ArgumentException("Target size must be 0–1048576 KiB; minimum quality must be between 1 and the selected maximum quality.");
        if (Resize == ResizeMode.Cover && (Width == 0 || Height == 0))
            throw new ArgumentException("Center crop requires both width and height.");
        if (Suffix is null || Subfolder is null || Background is null)
            throw new ArgumentException("Conversion text options cannot be null.");
        if (Suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The filename suffix contains invalid characters.");
        if (string.IsNullOrWhiteSpace(Subfolder) || Subfolder is "." or ".." || Subfolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Enter a single valid subfolder name.");
        if (Destination == DestinationMode.Custom && (string.IsNullOrWhiteSpace(OutputDirectory) || !Path.IsPathFullyQualified(OutputDirectory)))
            throw new ArgumentException("Select an absolute output directory.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Background, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("Background color must be #RRGGBB.");
    }
}

public sealed record AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public ConversionOptions Defaults { get; set; } = new();
    public ConversionOptions QuickProfile { get; set; } = new();
    public bool QuickUsesDefaults { get; set; } = true;
    public bool ExplorerEnabled
    {
        get; set;
    }
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string Language { get; set; } = "fa";
    public int Parallelism { get; set; } = 2;
    public bool Notify { get; set; } = true;
    public bool QuickAutoClose { get; set; } = true;
    public int ZoomPercent { get; set; } = 100;
    public bool OpenOutput
    {
        get; set;
    }
    public ConversionOptions EffectiveQuick() => (QuickUsesDefaults ? Defaults : QuickProfile) with { };
    public AppSettings Snapshot() => this with { Defaults = Defaults with { }, QuickProfile = QuickProfile with { } };
    public void Validate()
    {
        Defaults ??= new();
        QuickProfile ??= new();
        Defaults.Validate();
        QuickProfile.Validate();
        if (Language is not ("fa" or "en") || Parallelism is < 1 or > 8 || ZoomPercent is < 75 or > 150 || !Enum.IsDefined(Theme))
            throw new ArgumentException("Invalid general settings.");
    }
}

public sealed record ImageDetails(uint Width, uint Height, int Frames, string Format);
public sealed record OutputFormat(string Id, string Extension, bool Quality, bool MultiFrame, string? Codec = null)
{
    public string Label => Extension.ToUpperInvariant();
    public string EncoderId => Codec ?? Id;
}
public sealed record ConversionResult(string Source, string? Output, string? Error, bool Skipped = false, bool Canceled = false)
{
    public bool Success => Error is null && !Skipped && !Canceled && Output is not null;
}
public sealed record ConversionProgress(int Completed, int Total, int Succeeded, int Failed, int Skipped, string Current, ConversionResult? Result);
public sealed record BatchResult(IReadOnlyList<ConversionResult> Files, bool Canceled)
{
    public Guid JobId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public int CanceledCount => Files.Count(x => x.Canceled);
    public JobStatus Status => Canceled ? JobStatus.Canceled : Failed == 0 ? JobStatus.Completed : Succeeded > 0 || Skipped > 0 ? JobStatus.PartiallyCompleted : JobStatus.Failed;
    public int Succeeded => Files.Count(x => x.Success);
    public int Failed => Files.Count(x => x.Error is not null);
    public int Skipped => Files.Count(x => x.Skipped);
    public IReadOnlyList<string> OutputDirectories => Files.Where(x => x.Output is not null).Select(x => Path.GetDirectoryName(x.Output!)!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
