using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ImageMagick;
using MatinConverter.Core;
using MatinConverter.Windows;
using MatinConverter.Windows.Views;

internal static class Program
{
    private sealed class BindingErrors : TraceListener
    {
        public readonly List<string> Errors = [];
        public override void Write(string? message)
        {
            if (message?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true)
                Errors.Add(message);
        }
        public override void WriteLine(string? message) => Write(message);
    }
    [STAThread]
    private static int Main()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/MatinConverter;component/Themes/Controls.xaml", UriKind.Relative) });
        var errors = new List<Exception>();
        application.DispatcherUnhandledException += (_, e) => { errors.Add(e.Exception); e.Handled = true; };
        var trace = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var root = Path.Combine(Path.GetTempPath(), "MatinUiTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int exit = 0;
        application.Startup += async (_, _) =>
        {
            try
            {
                L.Apply("fa");
                ThemeService.Apply(AppTheme.Light);
                var window = new MainWindow();
                application.MainWindow = window;
                window.Show();
                var model = (MainViewModel)window.DataContext;
                var fixtures = Enumerable.Range(0, 100).Select(i => { var file = Path.Combine(root, $"تصویر {i}.png"); using var image = new MagickImage(MagickColors.Blue, 80, 40); image.Write(file); return file; }).ToArray();
                await model.AddAsync(fixtures);
                await Idle();
                window.UpdateLayout();
                Check(model.Files.Count == 100, "100 files were not added.");
                Check(model.Files.All(f => f.Thumbnail is not null), "Thumbnail generation failed.");
                var list = Descendants(window).OfType<ListBox>().First();
                Check(list.ItemContainerGenerator.ContainerFromIndex(99) is null, "The file list is not virtualized.");
                model.Remove.Execute(model.Files[0]);
                Check(model.Files.Count == 99, "Remove failed.");
                model.Clear.Execute(null);
                Check(model.Files.Count == 0, "Clear failed.");
                await model.AddAsync([fixtures[0]]);
                foreach (var language in new[] { "fa", "en" })
                    foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
                    {
                        L.Apply(language);
                        ThemeService.Apply(theme);
                        window.Width = language == "fa" ? 900 : 1180;
                        window.Height = language == "fa" ? 650 : 820;
                        model.Page = 1;
                        await Idle();
                        foreach (var expander in Descendants(window).OfType<Expander>().ToArray())
                            expander.IsExpanded = true;
                        await Idle();
                        window.UpdateLayout();
                        model.Page = 0;
                        await Idle();
                        window.UpdateLayout();
                        Check(window.FlowDirection == (language == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight), "Language direction failed.");
                    }
                model.Settings.Notify = false;
                model.Settings.OpenOutput = false;
                model.JobOptions.Format = "Jpeg";
                model.JobOptions.Destination = DestinationMode.Custom;
                model.JobOptions.OutputDirectory = Path.Combine(root, "out");
                Directory.CreateDirectory(model.JobOptions.OutputDirectory);
                await model.ConvertAsync();
                await Idle();
                Check(File.Exists(Path.Combine(root, "out", "تصویر 0.jpg")), "Manual conversion produced no output.");
                Check(window.IsEnabled, "The main window stayed disabled after completion.");
                foreach (var progress in application.Windows.OfType<ProgressWindow>().ToArray())
                {
                    Check(progress.Model.Finished, "Progress did not finish.");
                    progress.Close();
                }
                if (errors.Count > 0)
                    throw new AggregateException(errors);
                if (trace.Errors.Count > 0)
                    throw new Exception(string.Join(Environment.NewLine, trace.Errors));
                Console.WriteLine("PASS: real WPF view, 100 thumbnails with virtualization, remove/clear, both languages/themes, minimum window and manual conversion/progress.");
                window.Close();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
            finally { foreach (Window window in application.Windows.Cast<Window>().ToArray()) window.Close(); application.Shutdown(exit); }
        };
        var result = application.Run();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(trace);
        Directory.Delete(root, true);
        return result;
    }
    private static async Task Idle() => await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
