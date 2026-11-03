using System.Text;
namespace MatinConverter.Windows;

public static class ShellJobFile
{
    public static string[] ReadAndConsume(string path)
    {
        var folder = Path.GetFullPath(Path.Combine(SettingsService.DataDirectory, "Jobs")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full) + Path.DirectorySeparatorChar, folder, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(full), ".job", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid shell job path.");
        try
        {
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length is < 8 or > 4_000_000)
                throw new InvalidDataException("Invalid shell job size.");
            using var reader = new BinaryReader(stream, Encoding.Unicode);
            if (reader.ReadUInt32() != 0x314A434D)
                throw new InvalidDataException("Invalid shell job header.");
            var count = reader.ReadUInt32();
            if (count is 0 or > 10000)
                throw new InvalidDataException("Invalid selection count.");
            var files = new List<string>((int)count);
            for (int i = 0; i < count; i++)
            {
                var length = reader.ReadUInt32();
                if (length is 0 or > 32767 || length * 2 > stream.Length - stream.Position)
                    throw new InvalidDataException("Invalid shell path length.");
                var file = Encoding.Unicode.GetString(reader.ReadBytes(checked((int)length * 2)));
                if (!Path.IsPathFullyQualified(file) || file.Contains('\0'))
                    throw new InvalidDataException("Invalid selected path.");
                files.Add(file);
            }
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Unexpected shell job data.");
            return files.ToArray();
        }
        finally { File.Delete(full); }
    }
}
