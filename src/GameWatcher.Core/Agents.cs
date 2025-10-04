using System.Drawing;

namespace GameWatcher.Core;

public interface ITextboxDetector
{
    /// Attempts to locate the textbox region in the given image.
    /// Returns null if not found.
    Rectangle? DetectTextbox(Bitmap frame);
}

public interface IOcrEngine
{
    /// Performs OCR on the given bitmap and returns raw text.
    /// Implementations should not dispose the input.
    Task<string> ReadTextAsync(Bitmap image, CancellationToken ct = default);
}

public interface INormalizer
{
    string Normalize(string text);
}

public static class RectExtensions
{
    public static Rectangle Inset(this Rectangle r, int dx, int dy)
        => new Rectangle(r.X + dx, r.Y + dy, Math.Max(0, r.Width - 2 * dx), Math.Max(0, r.Height - 2 * dy));
}

