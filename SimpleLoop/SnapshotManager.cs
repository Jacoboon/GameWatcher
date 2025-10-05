using System;
using System.Drawing;
using System.IO;

namespace SimpleLoop
{
    /// <summary>
    /// Debug snapshot manager for capture debugging and verification
    /// </summary>
    public class SnapshotManager
    {
        private readonly string _snapshotDirectory;
        private readonly string _sessionDirectory;
        
        public SnapshotManager(string baseDirectory = "debug_snapshots")
        {
            _snapshotDirectory = baseDirectory;
            
            // Create session-specific directory
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _sessionDirectory = Path.Combine(_snapshotDirectory, $"session_{timestamp}");
            
            Directory.CreateDirectory(_sessionDirectory);
            Directory.CreateDirectory(Path.Combine(_sessionDirectory, "fullscreen"));
            Directory.CreateDirectory(Path.Combine(_sessionDirectory, "textbox_crops"));
            Directory.CreateDirectory(Path.Combine(_sessionDirectory, "enhanced_ocr"));
            Directory.CreateDirectory(Path.Combine(_sessionDirectory, "detection_debug"));
            
            Console.WriteLine($"📸 Snapshot manager initialized: {_sessionDirectory}");
        }
        
        /// <summary>
        /// Save fullscreen capture with detection overlay
        /// </summary>
        public void SaveFullscreenWithDetection(Bitmap fullscreen, Rectangle? detectedTextbox, int frameNumber)
        {
            try
            {
                // Create a copy to draw on
                using var debugImage = new Bitmap(fullscreen);
                using var graphics = Graphics.FromImage(debugImage);
                
                // Draw detection overlay
                if (detectedTextbox.HasValue)
                {
                    var rect = detectedTextbox.Value;
                    using var pen = new Pen(Color.Red, 3);
                    graphics.DrawRectangle(pen, rect);
                    
                    // Add detection info text
                    using var brush = new SolidBrush(Color.Yellow);
                    using var font = new Font("Arial", 16, FontStyle.Bold);
                    graphics.DrawString($"TEXTBOX: {rect.Width}x{rect.Height} at ({rect.X},{rect.Y})", 
                                      font, brush, 10, 10);
                }
                else
                {
                    // Mark as no detection
                    using var brush = new SolidBrush(Color.Red);
                    using var font = new Font("Arial", 16, FontStyle.Bold);
                    graphics.DrawString($"NO TEXTBOX DETECTED - Frame {frameNumber}", 
                                      font, brush, 10, 10);
                }
                
                var filename = Path.Combine(_sessionDirectory, "detection_debug", $"frame_{frameNumber:D4}.png");
                debugImage.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving debug snapshot: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save textbox crop for OCR debugging
        /// </summary>
        public void SaveTextboxCrop(Bitmap crop, string dialogueText, int cropNumber)
        {
            try
            {
                var safeText = SanitizeFilename(dialogueText);
                var filename = Path.Combine(_sessionDirectory, "textbox_crops", 
                    $"crop_{cropNumber:D3}_{safeText}.png");
                crop.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving textbox crop: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save enhanced OCR image for preprocessing debugging
        /// </summary>
        public void SaveEnhancedOcrImage(Bitmap enhanced, string dialogueText, int cropNumber)
        {
            try
            {
                var safeText = SanitizeFilename(dialogueText);
                var filename = Path.Combine(_sessionDirectory, "enhanced_ocr", 
                    $"enhanced_{cropNumber:D3}_{safeText}.png");
                enhanced.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving enhanced OCR image: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save periodic fullscreen captures for monitoring
        /// </summary>
        public void SavePeriodicFullscreen(Bitmap fullscreen, int frameNumber)
        {
            try
            {
                // Save every 100th frame for monitoring
                if (frameNumber % 100 == 0)
                {
                    var filename = Path.Combine(_sessionDirectory, "fullscreen", $"fullscreen_{frameNumber:D4}.png");
                    fullscreen.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving periodic fullscreen: {ex.Message}");
            }
        }
        
        private string SanitizeFilename(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "empty";
            
            // Take first 50 characters and remove invalid characters
            var safe = text.Length > 50 ? text.Substring(0, 50) : text;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }
            return safe.Replace(' ', '_').Replace('\n', '_').Replace('\r', '_');
        }
        
        public string SessionDirectory => _sessionDirectory;
    }
}