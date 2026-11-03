using MatinConverter.Windows.Views;
using System.Windows.Threading;

namespace MatinConverter.Windows;

public sealed class App : Application
{
    public static SettingsService SettingsStore { get; } = new();
    public static ImageFormatService Formats { get; } = new();
    public static ImageConversionService Engine { get; } = new(Formats);
    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/MatinConverter;component/Themes/Controls.xaml", UriKind.Relative) });
        var settings = SettingsStore.Load();
        ThemeService.Apply(settings.Theme);
        L.Apply(settings.Language);
        DispatcherUnhandledException += (_, e) => { AppLog.Write("unhandled", e.Exception.ToString()); MessageBox.Show(e.Exception.Message, L.T("Error")); e.Handled = true; };
    }
    public static async Task<BatchResult> RunJobAsync(string[] files, ConversionOptions options, AppSettings settings, bool quick, Action<ConversionProgress>? report = null)
    {
        using var cancellation = new CancellationTokenSource();
        var window = new ProgressWindow(files.Distinct(StringComparer.OrdinalIgnoreCase).Count(), cancellation);
        if (quick)
        {
            window.Title = "Matin Converter · Quick Convert";
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        if (!quick && Current.MainWindow is MainWindow main)
        {
            window.Owner = main;
            main.IsEnabled = false;
        }
        BatchResult result;
        try
        {
            window.Show();
            result = await Engine.ConvertAsync(files, options, settings.Parallelism, new Progress<ConversionProgress>(p => { window.Model.Report(p); report?.Invoke(p); }), cancellation.Token);
        }
        catch (Exception ex) { result = new(files.Select(f => new ConversionResult(f, null, ex.Message)).ToArray(), false); AppLog.Write("job", ex.ToString()); }
        finally { if (window.Owner is MainWindow owner) owner.IsEnabled = true; }
        window.Model.Finish(result);
        Task? notification = settings.Notify ? ShellActions.NotifyAsync(window.Model.Summary) : null;
        if (quick && settings.QuickAutoClose && !result.Canceled && result.Failed == 0)
        {
            await Task.Delay(1800);
            window.Close();
        }
        else if (quick)
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            if (window.IsVisible)
                await closed.Task;
        }
        if (notification is not null)
            await notification;
        return result;
    }
}

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--register") || args.Contains("--unregister"))
            {
                var enabled = args.Contains("--register");
                ExplorerIntegrationService.SetEnabled(enabled);
                var store = new SettingsService();
                var settings = store.Load();
                settings.ExplorerEnabled = enabled;
                store.Save(settings);
                return 0;
            }
            var app = new App();
            if (args.Contains("--job-file") || args.Contains("--quick"))
            {
                var manifest = Array.IndexOf(args, "--job-file");
                var paths = manifest >= 0 && manifest + 1 < args.Length ? ShellJobFile.ReadAndConsume(args[manifest + 1]) : args.Where(x => !x.StartsWith('-') && Path.IsPathFullyQualified(x) && File.Exists(x)).ToArray();
                using var broker = new QuickConversionBroker(app);
                if (!broker.Start(paths))
                    return 0;
                app.Run();
                return 0;
            }
            var main = new MainWindow();
            app.MainWindow = main;
            main.Closed += (_, _) => app.Shutdown();
            var initialFiles = args.Where(x => Path.IsPathFullyQualified(x) && File.Exists(x)).ToArray();
            if (initialFiles.Length > 0)
                main.Loaded += async (_, _) => { try { await ((MainViewModel)main.DataContext).AddAsync(initialFiles); } catch (Exception ex) { AppLog.Write("open-files", ex.Message); MessageBox.Show(ex.Message, L.T("Error")); } };
            main.Show();
            app.Run();
            return 0;
        }
        catch (Exception ex) { AppLog.Write("startup", ex.ToString()); MessageBox.Show(ex.Message, "Matin Converter", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
    }
}
