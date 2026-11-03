using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ImageMagick;
using MatinConverter.Core;
using MatinConverter.Windows;
using Microsoft.Win32;
using MatinConverter.ShellTests;

internal static class Program
{
    private sealed record RegistrySnapshot(Dictionary<string, (object Value, RegistryValueKind Kind)> Values, Dictionary<string, RegistrySnapshot> Children);
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Pass the full path to the built MatinConverter.exe.");
            return 2;
        }
        string exe = Path.GetFullPath(args[0]);
        var root = Path.Combine(Path.GetTempPath(), "MatinShellTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new SettingsService();
        var originalSettings = File.Exists(store.FilePath) ? File.ReadAllBytes(store.FilePath) : null;
        var originals = ExplorerIntegrationService.RegistrationPaths.ToDictionary(p => p, p => Read(Registry.CurrentUser.OpenSubKey(p)));
        var existing = Process.GetProcessesByName("MatinConverter").Select(p => p.Id).ToHashSet();
        try
        {
            var output = Path.Combine(root, "output");
            store.Save(new()
            {
                Notify = false,
                QuickUsesDefaults = true,
                Defaults = new()
                {
                    Format = "Jpeg",
                    Destination = DestinationMode.Custom,
                    OutputDirectory = output
                }
            });
            Run(exe, "--register");
            CoInitializeEx(IntPtr.Zero, 2);
            uint packageLength = 0;
            var packageStatus = GetCurrentPackageFullName(ref packageLength, IntPtr.Zero);
            using (var check = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + ExplorerIntegrationService.ClassId + @"\InprocServer32"))
                Console.WriteLine($"Diagnostic: x64={Environment.Is64BitProcess}, packageStatus={packageStatus}, merged registration={check?.GetValue(null)}");
            var groups = new[] { 1, 10, 100 };
            int expected = 0;
            foreach (var count in groups)
            {
                var files = Fixtures(root, $"selection-{count}", count);
                Invoke(files);
                expected += count;
                WaitForFiles(output, expected);
                Console.WriteLine($"PASS: COM selection of {count} files; all outputs present; originals preserved.");
                if (Process.GetProcessesByName("MatinConverter").Any(p => !existing.Contains(p.Id) && p.MainWindowTitle == "Matin Converter"))
                    throw new Exception("The main application window opened during quick conversion.");
            }
            var a = Fixtures(root, "concurrent-a", 10);
            var b = Fixtures(root, "concurrent-b", 10);
            Task.WaitAll(Task.Run(() => Invoke(a)), Task.Run(() => Invoke(b)));
            expected += 20;
            WaitForFiles(output, expected);
            Console.WriteLine("PASS: simultaneous COM invocations are queued and all 20 outputs survive.");
            var quickFiles = Fixtures(root, "pipe", 10);
            RunQuick(exe, quickFiles);
            expected += 10;
            WaitForFiles(output, expected);
            Console.WriteLine("PASS: additional --quick invocation reaches the existing worker via the user-only pipe.");
            Run(exe, "--unregister");
            if (ExplorerIntegrationService.RegistrationPaths.Any(p => Registry.CurrentUser.OpenSubKey(p) is not null))
                throw new Exception("Registration keys remained after disabling integration.");
            Console.WriteLine("PASS: disabling removes every owned verb and COM registration.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            foreach (var p in Process.GetProcessesByName("MatinConverter"))
            {
                if (existing.Contains(p.Id))
                    continue;
                try
                {
                    if (string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException) { }
            }
            foreach (var (path, value) in originals)
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, false);
                if (value is not null)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(path);
                    Restore(key, value);
                }
            }
            if (originalSettings is not null)
                File.WriteAllBytes(store.FilePath, originalSettings);
            else if (File.Exists(store.FilePath))
                File.Delete(store.FilePath);
            Directory.Delete(root, true);
        }
    }
    private static string[] Fixtures(string root, string prefix, int count)
    {
        return Enumerable.Range(0, count).Select(i => { var file = Path.Combine(root, $"{prefix}-تصویر {i}.png"); using var image = new MagickImage(MagickColors.Blue, 64, 32); image.Write(file); return file; }).ToArray();
    }
    private static void WaitForFiles(string directory, int expected)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(90))
        {
            if (Directory.Exists(directory) && Directory.GetFiles(directory, "*.jpg").Length == expected)
                return;
            Thread.Sleep(100);
        }
        throw new Exception($"Expected {expected} outputs; found {(Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0)}.");
    }
    private static void Invoke(string[] files)
    {
        var classId = new Guid(ExplorerIntegrationService.ClassId);
        var unknownId = new Guid("00000000-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref classId, IntPtr.Zero, 1, ref unknownId, out var unknown));
        var server = Marshal.GetObjectForIUnknown(unknown);
        Marshal.Release(unknown);
        var pointers = new List<IntPtr>();
        IShellItemArray? array = null;
        try
        {
            foreach (var file in files)
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(file, IntPtr.Zero, out var pidl, 0, out _));
                pointers.Add(pidl);
            }
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pointers.Count, pointers.ToArray(), out array));
            Marshal.ThrowExceptionForHR(((IExplorerCommand)server).Invoke(array, IntPtr.Zero));
        }
        finally { foreach (var pointer in pointers) Marshal.FreeCoTaskMem(pointer); if (array is not null) Marshal.ReleaseComObject(array); Marshal.ReleaseComObject(server); }
    }
    private static void Run(string exe, string arg)
    {
        using var p = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, ArgumentList = { arg } })!;
        if (!p.WaitForExit(20000) || p.ExitCode != 0)
            throw new Exception("Registration command failed.");
    }
    private static void RunQuick(string exe, string[] files)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add("--quick");
        foreach (var f in files)
            start.ArgumentList.Add(f);
        using var p = Process.Start(start)!;
        if (!p.WaitForExit(20000) || p.ExitCode != 0)
            throw new Exception("Quick invocation failed.");
    }
    private static RegistrySnapshot? Read(RegistryKey? key)
    {
        if (key is null)
            return null;
        using (key)
            return new(key.GetValueNames().ToDictionary(n => n, n => (key.GetValue(n, "")!, key.GetValueKind(n))), key.GetSubKeyNames().ToDictionary(n => n, n => Read(key.OpenSubKey(n))!));
    }
    private static void Restore(RegistryKey key, RegistrySnapshot value)
    {
        foreach (var (n, v) in value.Values)
            key.SetValue(n, v.Value, v.Kind);
        foreach (var (n, v) in value.Children)
        {
            using var child = key.CreateSubKey(n);
            Restore(child, v);
        }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string path, IntPtr binding, out IntPtr pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll")] private static extern int SHCreateShellItemArrayFromIDLists(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] items, out IShellItemArray array);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr result);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr pointer, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, IntPtr name);
}
