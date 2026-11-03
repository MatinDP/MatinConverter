using Microsoft.Win32;
using System.Windows.Media;

namespace MatinConverter.Windows;

public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark || (theme == AppTheme.System && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0);
        var values = new Dictionary<string, string>
        {
            ["BackgroundBrush"] = dark ? "#111827" : "#F8FAFC",
            ["SurfaceBrush"] = dark ? "#1F2937" : "#FFFFFF",
            ["TextBrush"] = dark ? "#F1F5F9" : "#111827",
            ["MutedBrush"] = dark ? "#BAC5D5" : "#475569",
            ["BorderBrush"] = dark ? "#46566C" : "#E2E8F0",
            ["AccentBrush"] = dark ? "#60A5FA" : "#2563EB",
            ["AccentTextBrush"] = dark ? "#10213A" : "#FFFFFF",
            ["HoverBrush"] = dark ? "#334155" : "#EFF6FF",
            ["SoftBrush"] = dark ? "#263449" : "#F1F5F9",
            ["SuccessBrush"] = dark ? "#86EFAC" : "#15803D",
            ["ErrorBrush"] = dark ? "#FCA5A5" : "#B91C1C"
        };
        foreach (var (key, color) in values)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }
    }
}
