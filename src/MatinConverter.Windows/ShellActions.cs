using System.Diagnostics;
using System.Windows.Controls;

namespace MatinConverter.Windows;

public static class ShellActions
{
    public static void OpenDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, L.T("Error")); }
    }
    public static void OpenDirectories(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            OpenDirectory(paths[0]);
            return;
        }
        var panel = new StackPanel { Margin = new Thickness(20) };
        foreach (var path in paths)
        {
            var button = new Button { Content = path, Margin = new Thickness(0, 4, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Left };
            button.Click += (_, _) => OpenDirectory(path);
            panel.Children.Add(button);
        }
        new Window { Title = L.T("Locations"), Width = 600, MaxHeight = 500, SizeToContent = SizeToContent.Height, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, WindowStartupLocation = WindowStartupLocation.CenterScreen }.Show();
    }
    public static async Task NotifyAsync(string message)
    {
        using var icon = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Text = "Matin Converter", Visible = true };
        icon.ShowBalloonTip(4000, "Matin Converter", message, System.Windows.Forms.ToolTipIcon.Info);
        await Task.Delay(5000);
    }
}
