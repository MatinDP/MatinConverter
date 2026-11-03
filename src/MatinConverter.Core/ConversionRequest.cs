namespace MatinConverter.Core;

public sealed record ConversionRequest(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<string> Files, AppSettings Settings)
{
    public static ConversionRequest Capture(IEnumerable<string> files, AppSettings settings) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow,
            files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), settings.Snapshot());
}
