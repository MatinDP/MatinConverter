using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatinConverter.Core;

public sealed class SettingsService
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatinConverter");
    public static string TempDirectory => Path.Combine(DataDirectory, "Temp");
    public string FilePath
    {
        get;
    }
    public string? RecoveryMessage
    {
        get; private set;
    }
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public SettingsService(string? filePath = null) => FilePath = filePath ?? Path.Combine(DataDirectory, "settings.json");

    public AppSettings Load()
    {
        RecoveryMessage = null;
        if (!File.Exists(FilePath))
            return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? throw new JsonException("Empty settings.");
            if (settings.SchemaVersion > 1)
                throw new JsonException("Settings are from a newer version.");
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            RecoveryMessage = ex.Message;
            try
            {
                File.Copy(FilePath, FilePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void ClearTemporaryFiles()
    {
        Directory.CreateDirectory(TempDirectory);
        foreach (var file in Directory.EnumerateFiles(TempDirectory))
        {
            try
            {
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
                File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

public static class AppLog
{
    private static readonly object Gate = new();
    public static void Write(string operation, string message)
    {
        try
        {
            lock (Gate)
            {
                var folder = Path.Combine(SettingsService.DataDirectory, "Logs");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, DateTime.Today.ToString("yyyy-MM-dd") + ".jsonl");
                if (new FileInfo(file) is { Exists: true, Length: > 2_000_000 })
                    return;
                File.AppendAllText(file, JsonSerializer.Serialize(new
                {
                    time = DateTimeOffset.Now,
                    operation,
                    message
                }) + Environment.NewLine);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
