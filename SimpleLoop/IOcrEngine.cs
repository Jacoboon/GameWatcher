using System;
using System.Drawing;

namespace SimpleLoop
{
    /// <summary>
    /// Common interface for OCR engines
    /// </summary>
    public interface IOcrEngine : IDisposable
    {
        /// <summary>
        /// Extract text from an image quickly
        /// </summary>
        /// <param name="image">The image to process</param>
        /// <returns>Extracted text</returns>
        string ExtractTextFast(Bitmap image);
    }
}