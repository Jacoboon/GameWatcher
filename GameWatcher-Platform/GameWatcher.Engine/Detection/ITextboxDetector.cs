using System.Drawing;

namespace GameWatcher.Engine.Detection;

/// <summary>
/// Interface for detecting game dialogue textboxes in screenshots
/// </summary>
public interface ITextboxDetector
{
    /// <summary>
    /// Detect the dialogue textbox in a screenshot
    /// </summary>
    /// <param name="screenshot">The screenshot to analyze</param>
    /// <returns>Rectangle bounds of the detected textbox, or null if not found</returns>
    Rectangle? DetectTextbox(Bitmap screenshot);
}