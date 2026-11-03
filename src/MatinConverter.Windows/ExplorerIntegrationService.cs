using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MatinConverter.Windows;

public static class ExplorerIntegrationService
{
    public const string ClassId = "{8B71E20D-9E6A-4936-9065-2D7E63937BF0}";
    private const string Classes = @"Software\Classes\";
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".jfif", ".png", ".webp", ".avif", ".heic", ".heif", ".gif", ".bmp", ".tif", ".tiff", ".ico", ".svg", ".svgz", ".psd", ".tga", ".jp2", ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".raf", ".rw2"];
    public static string BridgePath => Path.Combine(AppContext.BaseDirectory, "MatinConverter.Shell.dll");
    public static IReadOnlyList<string> RegistrationPaths => Extensions.Select(ext => Classes + VerbKey(ext)).Append(Classes + @"CLSID\" + ClassId).ToArray();

    private static string VerbKey(string extension) => @"SystemFileAssociations\" + extension + @"\shell\MatinConverter";

    private static RegistryKey? OpenUserClasses(bool writable)
    {
        // Open the user's class hive explicitly: installer-launched processes can have
        // a different HKCU class-link context from the interactive Explorer process.
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (sid is not null)
        {
            using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
            var hive = users.OpenSubKey(sid + "_Classes", writable);
            if (hive is not null)
                return hive;
        }
        return writable ? Registry.CurrentUser.CreateSubKey(@"Software\Classes") : Registry.CurrentUser.OpenSubKey(@"Software\Classes");
    }

    public static bool IsEnabled()
    {
        using var classes = OpenUserClasses(false);
        using var server = classes?.OpenSubKey(@"CLSID\" + ClassId + @"\InprocServer32");
        using var verb = classes?.OpenSubKey(VerbKey(".jpg"));
        return File.Exists(BridgePath)
            && string.Equals(server?.GetValue(null) as string, BridgePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(verb?.GetValue("ExplorerCommandHandler") as string, ClassId, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var classes = OpenUserClasses(true) ?? throw new IOException("The current user's Shell registry is unavailable.");
        if (enabled)
        {
            try
            {
                if (!File.Exists(BridgePath))
                    throw new FileNotFoundException("The native Shell bridge is missing. Build it before enabling Explorer integration.", BridgePath);
                classes.DeleteSubKeyTree(@"CLSID\" + ClassId, false);
                using (var cls = classes.CreateSubKey(@"CLSID\" + ClassId))
                    cls.SetValue(null, "Matin Converter Shell Command");
                using (var server = classes.CreateSubKey(@"CLSID\" + ClassId + @"\InprocServer32"))
                {
                    server.SetValue(null, BridgePath);
                    server.SetValue("ThreadingModel", "Both");
                }
                foreach (var extension in Extensions)
                {
                    using var verb = classes.CreateSubKey(VerbKey(extension));
                    verb.SetValue(null, "Convert with Matin Converter");
                    verb.SetValue("MUIVerb", "Convert with Matin Converter");
                    verb.SetValue("Icon", "\"" + Environment.ProcessPath + "\",0");
                    verb.SetValue("MultiSelectModel", "Player");
                    verb.DeleteSubKeyTree("command", false);
                    verb.SetValue("ExplorerCommandHandler", ClassId);
                }
                if (!IsEnabled())
                    throw new IOException("Explorer registration could not be verified for this user.");
            }
            catch
            {
                Remove(classes);
                throw;
            }
        }
        else
        {
            Remove(classes);
        }
        AppLog.Write("explorer-integration", $"enabled={enabled}; verified={IsEnabled() == enabled}");
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static void Remove(RegistryKey classes)
    {
        foreach (var extension in Extensions)
            classes.DeleteSubKeyTree(VerbKey(extension), false);
        classes.DeleteSubKeyTree(@"CLSID\" + ClassId, false);
        classes.DeleteSubKeyTree(@"AppID\" + ClassId, false);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
