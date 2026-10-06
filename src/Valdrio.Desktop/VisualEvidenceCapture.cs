using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Valdrio.Desktop;

public static class VisualEvidenceCapture
{
    public static void Capture(FrameworkElement element, string path)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        element.UpdateLayout();

        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));

        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);

        bitmap.Render(element);

        // A laid out control can still be clipped outside a ScrollViewer's viewport.
        // Reject transparent or uniform captures instead of publishing empty evidence.
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        bitmap.CopyPixels(pixels, stride, 0);
        var visiblePixels = 0;
        int? firstColor = null;
        var hasDifferentColors = false;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] == 0)
                continue;
            visiblePixels++;
            var color = pixels[offset] | pixels[offset + 1] << 8 | pixels[offset + 2] << 16;
            firstColor ??= color;
            hasDifferentColors |= color != firstColor.Value;
        }
        if (visiblePixels < (long)width * height / 100 || !hasDifferentColors)
            throw new InvalidOperationException("Visual evidence has no rendered content: " + path);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
