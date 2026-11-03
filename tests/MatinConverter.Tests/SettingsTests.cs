using MatinConverter.Core;
using Xunit;
namespace MatinConverter.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Matin-settings-tests-" + Guid.NewGuid().ToString("N"));
    private SettingsService Store => new(Path.Combine(root, "settings.json"));
    [Fact]
    public void QueuedRequestCapturesIndependentSettings()
    {
        var settings = new AppSettings { Defaults = new() { Quality = 75 } };
        var request = ConversionRequest.Capture([Path.Combine(root, "image.png")], settings);
        settings.Defaults.Quality = 40;
        Assert.Equal(75, request.Settings.EffectiveQuick().Quality);
    }
    [Fact]
    public void DefaultsAndProfilesPersist()
    {
        var settings = new AppSettings { Theme = AppTheme.Dark, Language = "en", Defaults = new() { Format = "WebP", Quality = 74 }, QuickProfile = new() { Format = "Jpeg" }, QuickUsesDefaults = false };
        Store.Save(settings);
        var read = Store.Load();
        Assert.Equal(settings, read);
    }
    [Fact]
    public void ManualOverrideDoesNotChangeDefaults()
    {
        var settings = new AppSettings();
        var manual = settings.Defaults with
        {
        };
        manual.Quality = 20;
        Assert.Equal(85, settings.Defaults.Quality);
    }
    [Fact]
    public void QuickInitiallyUsesGlobalAndCanUseOwnProfile()
    {
        var settings = new AppSettings { Defaults = new() { Format = "WebP" }, QuickProfile = new() { Format = "Jpeg" } };
        Assert.Equal("WebP", settings.EffectiveQuick().Format);
        settings.QuickUsesDefaults = false;
        Assert.Equal("Jpeg", settings.EffectiveQuick().Format);
    }
    [Fact]
    public void InvalidJsonIsBackedUpAndDefaultsRecovered()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Store.FilePath, "{ invalid");
        var store = Store;
        var loaded = store.Load();
        Assert.Equal(85, loaded.Defaults.Quality);
        Assert.NotNull(store.RecoveryMessage);
        Assert.Single(Directory.GetFiles(root, "*.invalid-*"));
    }
    [Fact]
    public void PartialOldJsonUsesDefaults()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Store.FilePath, "{\"Language\":\"en\"}");
        Assert.Equal(85, Store.Load().Defaults.Quality);
    }
    [Fact]
    public void InvalidValuesCannotBeSaved()
    {
        Assert.Throws<ArgumentException>(() => Store.Save(new() { Parallelism = 999 }));
        Assert.Throws<ArgumentException>(() => new ConversionOptions { Destination = DestinationMode.Subfolder, Subfolder = ".." }.Validate());
    }
    [Fact]
    public void SafeWritesLeaveNoTemporaryFiles()
    {
        Store.Save(new());
        Store.Save(new()
        {
            Language = "en"
        });
        Assert.Equal("en", Store.Load().Language);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
