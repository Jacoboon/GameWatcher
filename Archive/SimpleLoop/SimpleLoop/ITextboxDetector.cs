using System.Drawing;

namespace SimpleLoop
{
    public interface ITextboxDetector
    {
        Rectangle? DetectTextbox(Bitmap screenshot);
    }
}