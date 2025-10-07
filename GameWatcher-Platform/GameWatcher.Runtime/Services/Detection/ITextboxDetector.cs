using System.Drawing;

namespace GameWatcher.Runtime.Services.Detection
{
    public interface ITextboxDetector
    {
        Rectangle? DetectTextbox(Bitmap screenshot);
    }
}