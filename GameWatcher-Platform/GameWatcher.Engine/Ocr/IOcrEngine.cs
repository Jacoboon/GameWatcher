using System.Drawing;

namespace GameWatcher.Engine.Ocr;

/// <summary>
/// Interface for OCR engines that extract text from game screenshots
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Gets whether the OCR engine is available and ready to use
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Extract text from an image asynchronously
    /// </summary>
    /// <param name="image">The image to process</param>
    /// <returns>Extracted text or empty string if no text found</returns>
    Task<string> ExtractTextAsync(Bitmap image);

    /// <summary>
    /// Extract text from an image synchronously (convenience method)
    /// </summary>
    /// <param name="image">The image to process</param>
    /// <returns>Extracted text or empty string if no text found</returns>
    string ExtractTextFast(Bitmap image);
}