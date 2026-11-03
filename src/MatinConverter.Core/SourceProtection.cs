using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MatinConverter.Core;

internal sealed class SourceProtection
{
    private readonly HashSet<string> paths;
    private readonly HashSet<string> identities;

    public SourceProtection(IEnumerable<string> sources, bool compareFileIdentities)
    {
        paths = sources.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        identities = compareFileIdentities
            ? paths.Select(path => Identity(path, false)).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : [];
    }

    public bool Contains(string path, bool requireIdentity)
    {
        if (paths.Contains(Path.GetFullPath(path)))
            return true;
        return identities.Count > 0 && Identity(path, requireIdentity) is { } identity && identities.Contains(identity);
    }

    private static string? Identity(string path, bool strict)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandleEx(file, 18, out var identity, (uint)Marshal.SizeOf<FileIdentity>()))
            {
                if (strict)
                    throw new IOException("Cannot verify that the existing destination is not an original image.", new Win32Exception(Marshal.GetLastWin32Error()));
                return null;
            }
            return $"{identity.Volume:X16}:{identity.Low:X16}:{identity.High:X16}";
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception ex) when (!strict && ex is IOException or UnauthorizedAccessException) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdentity
    {
        public ulong Volume; public ulong Low; public ulong High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int informationClass, out FileIdentity information, uint size);
}
