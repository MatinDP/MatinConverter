using ImageMagick;

namespace MatinConverter.Core;

internal static class ImageResize
{
    public static void Apply(IMagickImage<byte> image, ConversionOptions options)
    {
        if (options.Resize == ResizeMode.Original || (options.Width == 0 && options.Height == 0))
            return;
        if (options.Resize == ResizeMode.Cover)
        {
            var limit = options.AllowEnlargement ? 1d : Math.Min(1, Math.Min((double)image.Width / options.Width, (double)image.Height / options.Height));
            var width = (uint)Math.Max(1, Math.Round(options.Width * limit));
            var height = (uint)Math.Max(1, Math.Round(options.Height * limit));
            var scale = Math.Max((double)width / image.Width, (double)height / image.Height);
            image.Resize(new MagickGeometry((uint)Math.Ceiling(image.Width * scale), (uint)Math.Ceiling(image.Height * scale)) { IgnoreAspectRatio = true });
            image.Crop(width, height, Gravity.Center);
            image.ResetPage();
            return;
        }
        if (options.Resize == ResizeMode.Exact || !options.PreserveAspect)
        {
            var width = options.Width > 0 ? (uint)options.Width : image.Width;
            var height = options.Height > 0 ? (uint)options.Height : image.Height;
            if (!options.AllowEnlargement) { width = Math.Min(width, image.Width); height = Math.Min(height, image.Height); }
            image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
            return;
        }
        var fit = options.Width > 0 && options.Height > 0
            ? Math.Min((double)options.Width / image.Width, (double)options.Height / image.Height)
            : options.Width > 0 ? (double)options.Width / image.Width : (double)options.Height / image.Height;
        if (!options.AllowEnlargement) fit = Math.Min(1, fit);
        image.Resize(new MagickGeometry((uint)Math.Max(1, Math.Round(image.Width * fit)), (uint)Math.Max(1, Math.Round(image.Height * fit))) { IgnoreAspectRatio = true });
    }
}
