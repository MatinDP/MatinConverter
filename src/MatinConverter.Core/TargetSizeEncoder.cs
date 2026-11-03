using ImageMagick;

namespace MatinConverter.Core;

internal static class TargetSizeEncoder
{
    private const int MaximumAttempts = 24;

    public static void Write(MagickImageCollection images, string destination, OutputFormat format, ConversionOptions options, CancellationToken token)
    {
        var encoder = Enum.Parse<MagickFormat>(format.EncoderId);
        if (options.TargetSizeKiB == 0) { images.Write(destination, encoder); return; }
        if (!format.Quality)
            throw new NotSupportedException("Target file size requires a lossy output such as JPEG, WEBP or AVIF. Disable the target or choose a supported format.");

        long limit = options.TargetSizeKiB * 1024L;
        var trial = destination + ".trial";
        int attempts = 0;
        try
        {
            bool Encode(int quality)
            {
                token.ThrowIfCancellationRequested();
                foreach (var image in images) image.Quality = (uint)quality;
                images.Write(trial, encoder);
                token.ThrowIfCancellationRequested();
                attempts++;
                if (new FileInfo(trial).Length > limit) return false;
                File.Copy(trial, destination, true);
                return true;
            }

            for (int sizeStep = 0; sizeStep <= (options.AllowTargetDownsize ? 3 : 0) && attempts < MaximumAttempts; sizeStep++)
            {
                if (Encode(options.Quality)) return;
                if (options.MinimumQuality < options.Quality && Encode(options.MinimumQuality))
                {
                    int low = options.MinimumQuality + 1, high = options.Quality - 1;
                    while (low <= high && attempts < MaximumAttempts)
                    {
                        int quality = low + (high - low) / 2;
                        if (Encode(quality)) low = quality + 1;
                        else high = quality - 1;
                    }
                    return;
                }
                if (!options.AllowTargetDownsize || sizeStep == 3) break;
                foreach (var image in images)
                    image.Resize(new MagickGeometry(Math.Max(1, (uint)Math.Floor(image.Width * 0.7)), Math.Max(1, (uint)Math.Floor(image.Height * 0.7))) { IgnoreAspectRatio = true });
            }
            throw new InvalidDataException($"The image cannot fit within {options.TargetSizeKiB} KiB using the allowed quality and dimensions. Increase the target, lower minimum quality, or permit dimension reduction.");
        }
        finally { if (File.Exists(trial)) File.Delete(trial); }
    }
}
