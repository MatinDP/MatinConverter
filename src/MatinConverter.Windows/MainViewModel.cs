using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MatinConverter.Windows;

public sealed class FileItem(string path) : Observable
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public string Extension => System.IO.Path.GetExtension(Path).ToUpperInvariant();
    public string Size => new FileInfo(Path) is { Exists: true } f ? f.Length < 1024 * 1024 ? $"{f.Length / 1024d:0.#} KB" : $"{f.Length / 1048576d:0.#} MB" : "";
    private string dimensions = ""; public string Dimensions
    {
        get => dimensions; set => Set(ref dimensions, value);
    }
    private string status = L.T("Loading"); public string Status
    {
        get => status; set => Set(ref status, value);
    }
    private ImageSource? thumbnail; public ImageSource? Thumbnail
    {
        get => thumbnail; set => Set(ref thumbnail, value);
    }
}
public sealed class MainViewModel : Observable
{
    private readonly SettingsService store = App.SettingsStore;
    private int page;
    public int Page
    {
        get => page; set
        {
            Set(ref page, value);
            Changed(nameof(IsConvert));
            Changed(nameof(IsSettings));
        }
    }
    public bool IsConvert => Page == 0; public bool IsSettings => Page == 1;
    private AppSettings settings; public AppSettings Settings
    {
        get => settings; private set => Set(ref settings, value);
    }
    private ConversionOptions jobOptions; public ConversionOptions JobOptions
    {
        get => jobOptions; private set => Set(ref jobOptions, value);
    }
    public ObservableCollection<FileItem> Files { get; } = [];
    public string? PreviewSource => Files.FirstOrDefault()?.Path;
    public string CountLabel => $"{Files.Count} {L.T("Count")}";
    private string message = ""; public string Message
    {
        get => message; set => Set(ref message, value);
    }
    private bool busy; public bool Busy
    {
        get => busy; set
        {
            Set(ref busy, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }
    private string integration = ""; public string Integration
    {
        get => integration; set => Set(ref integration, value);
    }
    public string IntegrationAction => ExplorerIntegrationService.IsEnabled() ? L.T("DisableExplorer") : L.T("EnableExplorer");
    public bool Recursive
    {
        get; set;
    }
    public int[] ParallelChoices { get; } = [1, 2, 3, 4, 6, 8];
    public int[] ZoomChoices { get; } = [75, 90, 100, 110, 125, 150];
    public ICommand ConvertPage
    {
        get;
    }
    public ICommand SettingsPage
    {
        get;
    }
    public ICommand Browse
    {
        get;
    }
    public ICommand Folder
    {
        get;
    }
    public ICommand Remove
    {
        get;
    }
    public ICommand Clear
    {
        get;
    }
    public ICommand Convert
    {
        get;
    }
    public ICommand Save
    {
        get;
    }
    public ICommand Reset
    {
        get;
    }
    public ICommand ToggleExplorer
    {
        get;
    }
    public ICommand ClearTemp
    {
        get;
    }
    public ICommand TestQuick { get; }
    public MainViewModel()
    {
        Files.CollectionChanged += (_, _) => Changed(nameof(PreviewSource));
        settings = store.Load();
        jobOptions = settings.Defaults with
        {
        };
        if (store.RecoveryMessage is not null)
            Message = L.T("Recovery");
        ConvertPage = new RelayCommand(_ => Page = 0);
        SettingsPage = new RelayCommand(_ => Page = 1);
        Browse = new AsyncCommand(BrowseAsync, () => !Busy);
        Folder = new AsyncCommand(FolderAsync, () => !Busy);
        Remove = new RelayCommand(item => { if (item is FileItem f) Files.Remove(f); Changed(nameof(CountLabel)); }, _ => !Busy);
        Clear = new RelayCommand(_ => { Files.Clear(); Changed(nameof(CountLabel)); }, _ => !Busy && Files.Count > 0);
        Convert = new AsyncCommand(ConvertAsync, () => !Busy && Files.Count > 0);
        Save = new AsyncCommand(() => { SaveSettings(); return Task.CompletedTask; });
        Reset = new AsyncCommand(() => { if (MessageBox.Show(L.T("ResetAsk"), L.T("Settings"), MessageBoxButton.YesNo) == MessageBoxResult.Yes) { ExplorerIntegrationService.SetEnabled(false); Settings = new(); JobOptions = Settings.Defaults with { }; SaveSettings(); UpdateIntegration(); } return Task.CompletedTask; });
        ToggleExplorer = new AsyncCommand(() => { ExplorerIntegrationService.SetEnabled(!ExplorerIntegrationService.IsEnabled()); Settings.ExplorerEnabled = ExplorerIntegrationService.IsEnabled(); store.Save(Settings); UpdateIntegration(); return Task.CompletedTask; });
        ClearTemp = new RelayCommand(_ => { SettingsService.ClearTemporaryFiles(); Message = L.T("TempCleared"); }, _ => !Busy);
        TestQuick = new AsyncCommand(TestQuickAsync, () => !Busy);
        UpdateIntegration();
    }
    private async Task TestQuickAsync()
    {
        var picker = new OpenFileDialog { Multiselect = false, Filter = App.Formats.FileFilter };
        if (picker.ShowDialog() != true) return;
        var saved = store.Load();
        Busy = true;
        try { await App.RunJobAsync([picker.FileName], saved.EffectiveQuick(), saved, false); }
        finally { Busy = false; }
    }
    private void UpdateIntegration()
    {
        try
        {
            Integration = ExplorerIntegrationService.IsEnabled() ? L.T("Enabled") : L.T("Disabled");
            Changed(nameof(IntegrationAction));
        }
        catch (Exception ex) { Integration = L.T("Error") + ": " + ex.Message; }
    }
    private Task BrowseAsync()
    {
        var picker = new OpenFileDialog { Multiselect = true, Filter = App.Formats.FileFilter };
        return picker.ShowDialog() == true ? AddAsync(picker.FileNames) : Task.CompletedTask;
    }
    private Task FolderAsync()
    {
        var picker = new OpenFolderDialog();
        return picker.ShowDialog() == true ? AddAsync([picker.FolderName]) : Task.CompletedTask;
    }
    public async Task AddAsync(IEnumerable<string> paths)
    {
        if (Busy)
            return;
        Busy = true;
        Message = L.T("Loading");
        try
        {
            var incoming = await Task.Run(() => paths.SelectMany(p => Directory.Exists(p) ? Directory.EnumerateFiles(p, "*", new EnumerationOptions { RecurseSubdirectories = Recursive, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(App.Formats.IsLikelyImage) : File.Exists(p) ? [p] : Array.Empty<string>()).Select(System.IO.Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(10000).ToArray());
            var existing = Files.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = incoming.Where(existing.Add).Select(p => new FileItem(p)).ToArray();
            foreach (var item in added)
                Files.Add(item);
            Changed(nameof(CountLabel));
            using var limiter = new SemaphoreSlim(2);
            await Task.WhenAll(added.Select(async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    var data = await Task.Run(() => { var details = App.Engine.Inspect(item.Path); return (details, bytes: App.Engine.Thumbnail(item.Path)); });
                    using var stream = new MemoryStream(data.bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 80;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    item.Thumbnail = bitmap;
                    item.Dimensions = $"{data.details.Width} × {data.details.Height}" + (data.details.Format == "PDF" ? $" · {data.details.Frames} {L.T("Pages")}" : data.details.Frames > 1 ? $" · {data.details.Frames} frames" : "");
                    item.Status = L.T("Ready");
                }
                catch (Exception ex) { item.Status = L.T("Unsupported"); AppLog.Write("inspect", ex.Message); }
                finally { limiter.Release(); }
            }));
            Message = added.Length == 0 ? L.T("NoImages") : L.T("SourceSafe");
        }
        finally { Busy = false; }
    }
    public async Task ConvertAsync()
    {
        FormValidation.EnsureValid();
        JobOptions.Validate();
        _ = App.Formats.GetOutput(JobOptions.Format);
        if (JobOptions.Destination == DestinationMode.Custom && !Directory.Exists(JobOptions.OutputDirectory) && MessageBox.Show(L.T("CreateFolder"), L.T("Destination"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        Busy = true;
        try
        {
            var result = await App.RunJobAsync(Files.Select(x => x.Path).ToArray(), JobOptions with
            {
            }, Settings, false, p => { if (p.Result is not null) { var item = Files.FirstOrDefault(x => x.Path.Equals(p.Result.Source, StringComparison.OrdinalIgnoreCase)); if (item is not null) item.Status = L.T(p.Result.Success ? "Success" : p.Result.Skipped ? "Skipped" : p.Result.Canceled ? "Canceled" : "Failed"); } });
            foreach (var group in result.Files.GroupBy(x => x.Source, StringComparer.OrdinalIgnoreCase))
            {
                var item = Files.FirstOrDefault(x => x.Path.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
                if (item is not null)
                    item.Status = L.T(group.Any(x => x.Error is not null) ? "Failed" : group.Any(x => x.Canceled) ? "Canceled" : group.Any(x => x.Success) ? "Success" : "Skipped");
            }
            if (Settings.OpenOutput && result.OutputDirectories.Count > 0)
                ShellActions.OpenDirectory(result.OutputDirectories[0]);
        }
        finally { Busy = false; }
    }
    private void SaveSettings()
    {
        FormValidation.EnsureValid();
        Settings.Validate();
        var enabled = ExplorerIntegrationService.IsEnabled();
        Settings.ExplorerEnabled = enabled;
        store.Save(Settings);
        ThemeService.Apply(Settings.Theme);
        L.Apply(Settings.Language);
        if (Application.Current.MainWindow is Views.MainWindow main) main.ApplyZoom(Settings.ZoomPercent);
        UpdateIntegration();
        Message = L.T("Saved");
        Changed(nameof(CountLabel));
    }
}
