using ImageMagick;
using PDFiumCore;
using System.Runtime.InteropServices;

namespace MatinConverter.Core;

// PDFium is not thread safe, including operations on separate documents.
internal sealed class PdfDocument : IDisposable
{
    private static readonly object Gate = new();
    private static bool initialized;
    private FpdfDocumentT? document;
    private GCHandle data;
    private bool disposed;
    public int PageCount { get; }
    public static bool IsPdf(string path) => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public PdfDocument(string path)
    {
        Monitor.Enter(Gate);
        try
        {
            if (!initialized) { fpdfview.FPDF_InitLibrary(); initialized = true; }
            // Loading from memory also supports paths containing Persian characters.
            if (new FileInfo(path).Length > 512L * 1024 * 1024)
                throw new InvalidDataException("PDF exceeds the 512 MB input limit.");
            var bytes = File.ReadAllBytes(path);
            data = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            document = fpdfview.FPDF_LoadMemDocument64(data.AddrOfPinnedObject(), (ulong)bytes.LongLength, null);
            if (document is null)
                throw new InvalidDataException("Cannot open PDF. It may be damaged or password protected.");
            PageCount = fpdfview.FPDF_GetPageCount(document);
            if (PageCount <= 0) throw new InvalidDataException("This PDF has no pages.");
            if (PageCount > 10000) throw new InvalidDataException("PDF exceeds the 10000 page limit.");
        }
        catch { Dispose(); throw; }
    }

    public (uint Width, uint Height) Size(int index, double dpi = 150)
    {
        double width = 0, height = 0;
        if (fpdfview.FPDF_GetPageSizeByIndex(document!, index, ref width, ref height) == 0)
            throw new InvalidDataException($"Cannot read PDF page {index + 1}.");
        width = Math.Ceiling(width * dpi / 72);
        height = Math.Ceiling(height * dpi / 72);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1 || width > 32768 || height > 32768 || width * height > 100_000_000)
            throw new InvalidDataException($"PDF page {index + 1} exceeds the 100 megapixel / 32768 pixel limit.");
        return ((uint)width, (uint)height);
    }

    public MagickImage Render(int index, CancellationToken token, bool thumbnail = false)
    {
        token.ThrowIfCancellationRequested();
        var size = Size(index);
        if (thumbnail)
        {
            var scale = Math.Min(80d / size.Width, 80d / size.Height);
            size = ((uint)Math.Max(1, Math.Round(size.Width * scale)), (uint)Math.Max(1, Math.Round(size.Height * scale)));
        }
        var page = fpdfview.FPDF_LoadPage(document!, index);
        if (page is null) throw new InvalidDataException($"Cannot render PDF page {index + 1}.");
        try
        {
            var bitmap = fpdfview.FPDFBitmapCreateEx((int)size.Width, (int)size.Height, (int)FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);
            if (bitmap is null) throw new InvalidDataException("Cannot allocate PDF page bitmap.");
            try
            {
                fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, (int)size.Width, (int)size.Height, uint.MaxValue);
                fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, (int)size.Width, (int)size.Height, 0, (int)RenderFlags.RenderAnnotations);
                token.ThrowIfCancellationRequested();
                var stride = fpdfview.FPDFBitmapGetStride(bitmap);
                var pixels = new byte[checked((int)(size.Width * size.Height * 4))];
                var buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                for (int y = 0; y < size.Height; y++)
                    Marshal.Copy(buffer + y * stride, pixels, checked(y * (int)size.Width * 4), (int)size.Width * 4);
                var image = new MagickImage();
                try
                {
                    image.ReadPixels(pixels, new PixelReadSettings(size.Width, size.Height, StorageType.Char, PixelMapping.BGRA));
                    image.Density = new Density(150);
                    return image;
                }
                catch { image.Dispose(); throw; }
            }
            finally { fpdfview.FPDFBitmapDestroy(bitmap); }
        }
        finally { fpdfview.FPDF_ClosePage(page); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (document is not null) { fpdfview.FPDF_CloseDocument(document); document = null; }
        if (data.IsAllocated) data.Free();
        Monitor.Exit(Gate);
    }
}
