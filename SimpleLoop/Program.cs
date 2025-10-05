using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleLoop
{
    class Program
    {
        private static Bitmap? _lastFrame;
        private static Bitmap? _lastTextbox;
        private static SimpleTextboxDetector? _detector;
        private static SimpleOCR? _ocr;
        private static DialogueCatalog? _catalog;
        private static string _lastText = "";
        private static readonly object _lockObject = new();
        
        // Performance tracking
        private static int _frameCount = 0;
        private static int _processedFrames = 0;
        private static int _textboxesFound = 0;
        private static DateTime _startTime = DateTime.Now;
        private static long _totalProcessingTime = 0;

        // Performance optimization - crop to game area
        private static Rectangle _gameArea = new Rectangle(0, 0, 1920, 1080); // Adjust as needed

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SimpleLoop Game Text Capture v4 ===");
            Console.WriteLine("DIALOGUE CATALOG & SPEAKER DETECTION");
            Console.WriteLine("Starting 15fps capture loop (66ms budget)...");
            Console.WriteLine("Press Ctrl+C to stop");
            Console.WriteLine("Press 'S' + Enter to show dialogue stats\n");

            // Initialize components
            _detector = new SimpleTextboxDetector(); // Check EVERY unique frame for textbox
            _ocr = new SimpleOCR();
            _catalog = new DialogueCatalog();
            
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                PrintStats();
                Environment.Exit(0);
            };

            // Main capture loop - 15 FPS = 67ms intervals (more breathing room)
            var timer = new System.Threading.Timer(CaptureAndProcess, null, 0, 67);
            
            // Keep the program running
            await Task.Delay(Timeout.Infinite);
        }

        private static void CaptureAndProcess(object? state)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Step 1: Capture game window (targets specific game)
                var currentFrame = ScreenCapture.CaptureGameWindow();
                _frameCount++;

                lock (_lockObject)
                {
                    // Step 2: Compare with last frame (optimized comparison)
                    if (_lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 500))
                    {
                        // Frame is the same, skip processing
                        currentFrame.Dispose();
                        return;
                    }

                    // Frame has changed, dispose old frame and continue processing
                    _lastFrame?.Dispose();
                    _lastFrame = new Bitmap(currentFrame);
                }

                _processedFrames++;

                // Step 3: Check EVERY unique frame for textbox (no caching BS)
                var textboxRect = _detector?.DetectTextbox(currentFrame);
                
                // Debug screenshots disabled for performance
                
                if (textboxRect.HasValue)
                {
                    _textboxesFound++;
                    Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] TEXTBOX DETECTED at {textboxRect.Value}");

                    // Step 4: Crop textbox area  
                    var textboxImage = CropImage(currentFrame, textboxRect.Value);
                    
                    // Step 5: Check if this is a unique textbox (avoid OCR spam on same text)
                    if (IsUniqueTextbox(textboxImage))
                    {
                        Console.WriteLine($"🆕 [{DateTime.Now:HH:mm:ss.fff}] Unique textbox detected, processing OCR");
                        
                        // Create copies for async processing to avoid threading issues
                        var textboxCopy = new Bitmap(textboxImage);
                        var enhancedImage = EnhanceForOCR(textboxCopy);
                        
                        // Step 6: OCR the text (async to not block the loop)
                        Task.Run(() => {
                            try 
                            {
                                Console.WriteLine($" [{DateTime.Now:HH:mm:ss.fff}] Running OCR on textbox...");
                                var rawText = _ocr?.ExtractTextFast(enhancedImage) ?? "";
                                Console.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] Raw OCR: \"{rawText}\"");
                                
                                var cleanedText = CleanOCRText(rawText);
                                Console.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] Cleaned: \"{cleanedText}\"");
                                ProcessNewDialogue(cleanedText);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ OCR Error: {ex.Message}");
                            }
                            finally
                            {
                                textboxCopy?.Dispose();
                                enhancedImage?.Dispose();
                            }
                        });
                    }
                    else
                    {
                        Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss.fff}] Same textbox as before, skipping OCR");
                    }
                    
                    textboxImage?.Dispose();
                }
                else
                {
                    // Only log occasionally to avoid spam
                    if (_frameCount % 200 == 0)
                    {
                        Console.WriteLine($"⭕ [{DateTime.Now:HH:mm:ss.fff}] No textbox found (frame {_frameCount})");
                    }
                }

                currentFrame.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in capture loop: {ex.Message}");
            }

            stopwatch.Stop();
            _totalProcessingTime += stopwatch.ElapsedMilliseconds;
            
            // Only warn if consistently slow (15fps = 67ms budget)
            if (stopwatch.ElapsedMilliseconds > 60)
            {
                Console.WriteLine($"SLOW: Processing took {stopwatch.ElapsedMilliseconds}ms (target: <60ms)");
            }
            
            // Log performance every 100 frames
            if (_frameCount % 100 == 0)
            {
                var avgMs = _totalProcessingTime / (double)_frameCount;
                Console.WriteLine($"[{_frameCount}] Avg: {avgMs:F1}ms | Processed: {_processedFrames} | Found: {_textboxesFound}");
            }
        }

        private static Bitmap CropImage(Bitmap source, Rectangle cropRect)
        {
            // Ensure crop rectangle is within bounds
            var actualRect = Rectangle.Intersect(cropRect, new Rectangle(0, 0, source.Width, source.Height));
            
            if (actualRect.IsEmpty)
                return new Bitmap(1, 1); // Return minimal bitmap if no intersection

            var croppedImage = new Bitmap(actualRect.Width, actualRect.Height);
            using (var g = Graphics.FromImage(croppedImage))
            {
                g.DrawImage(source, 0, 0, actualRect, GraphicsUnit.Pixel);
            }
            return croppedImage;
        }

        private static Bitmap EnhanceForOCR(Bitmap source)
        {
            // Step 1: Convert to grayscale for better contrast
            var grayscale = ConvertToGrayscale(source);
            
            // Step 2: Scale up 4x (higher than before) 
            var scaledWidth = grayscale.Width * 4;
            var scaledHeight = grayscale.Height * 4;
            var scaled = new Bitmap(scaledWidth, scaledHeight);
            
            using (var g = Graphics.FromImage(scaled))
            {
                // Use nearest neighbor to preserve pixel art sharpness
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                g.DrawImage(grayscale, 0, 0, scaledWidth, scaledHeight);
            }
            
            grayscale.Dispose();
            
            // Step 3: Apply threshold to make text pure black/white
            var enhanced = ApplyThreshold(scaled, 128);
            scaled.Dispose();
            
            return enhanced;
        }
        
        private static Bitmap ConvertToGrayscale(Bitmap source)
        {
            var grayscale = new Bitmap(source.Width, source.Height);
            
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    var grayColor = Color.FromArgb(gray, gray, gray);
                    grayscale.SetPixel(x, y, grayColor);
                }
            }
            
            return grayscale;
        }
        
        private static Bitmap ApplyThreshold(Bitmap source, int threshold)
        {
            var result = new Bitmap(source.Width, source.Height);
            
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var brightness = pixel.R; // Already grayscale
                    var newColor = brightness > threshold ? Color.White : Color.Black;
                    result.SetPixel(x, y, newColor);
                }
            }
            
            return result;
        }

        private static bool IsUniqueTextbox(Bitmap textbox)
        {
            if (_lastTextbox == null)
            {
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            // Fast comparison - if dimensions different, it's unique
            if (textbox.Width != _lastTextbox.Width || textbox.Height != _lastTextbox.Height)
            {
                _lastTextbox?.Dispose();
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            // Sample-based comparison for speed (check every 10th pixel)
            bool isDifferent = false;
            for (int y = 0; y < textbox.Height && !isDifferent; y += 10)
            {
                for (int x = 0; x < textbox.Width && !isDifferent; x += 10)
                {
                    var pixel1 = textbox.GetPixel(x, y);
                    var pixel2 = _lastTextbox.GetPixel(x, y);
                    
                    // Allow small tolerance for compression artifacts
                    if (Math.Abs(pixel1.R - pixel2.R) > 30 ||
                        Math.Abs(pixel1.G - pixel2.G) > 30 ||
                        Math.Abs(pixel1.B - pixel2.B) > 30)
                    {
                        isDifferent = true;
                    }
                }
            }
            
            if (isDifferent)
            {
                _lastTextbox?.Dispose();
                _lastTextbox = new Bitmap(textbox);
                return true;
            }
            
            return false; // Same textbox, skip OCR
        }

        private static string CleanOCRText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            var cleaned = text;
            
            // Fix common OCR character errors
            cleaned = cleaned.Replace("Il ", "I ");     // "Il am" → "I am"
            cleaned = cleaned.Replace(" Il ", " I ");   // Middle of sentence
            cleaned = cleaned.Replace("15", "is");      // "15" → "is"
            cleaned = cleaned.Replace("1s", "is");      // "1s" → "is" 
            cleaned = cleaned.Replace("0", "o");        // Zero to O
            cleaned = cleaned.Replace("5", "s");        // 5 to S in context
            cleaned = cleaned.Replace("3", "e");        // 3 to E
            cleaned = cleaned.Replace("1", "i");        // 1 to i in context
            
            // Fix mixed case issues
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bTUtUrE\b", "future", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\btruth\b", "truth", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix word boundary issues  
            cleaned = cleaned.Replace("patient iy", "patiently");
            cleaned = cleaned.Replace("patient ly", "patiently");
            
            // Fix King/king capitalization
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bKIM\b", "King", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bKING\b", "King", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "AStoS" → "Astos" 
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bASIoS\b", "Astos", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bAStoS\b", "Astos", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "fou" → "You"
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bfou\b", "You", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "IT" → "I" at start of sentences
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^IT\b", "I", 
                System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Fix "mow" → "now"
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bmow\b", "now", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "powertul" → "powerful"
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bpowertul\b", "powerful", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix smart quotes and apostrophes
            cleaned = cleaned.Replace("don?t", "don't");
            cleaned = cleaned.Replace("can?t", "can't");
            cleaned = cleaned.Replace("won?t", "won't");
            cleaned = cleaned.Replace("?", "'"); // General smart quote fix
            
            // Fix "You be" → "You'll be" 
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bYou be\b", "You'll be", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "quer" → "our" and "aur" → "our"
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bquer\b", "our", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\baur\b", "our", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "helo" → "help"
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bhelo\b", "help", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix "elts" → "elfs" 
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\belts\b", "elfs", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Fix additional "powverTul" → "powerful" variant
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bpowverTul\b", "powerful", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            return cleaned.Trim();
        }

        private static void ProcessNewDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"⚪ [{DateTime.Now:HH:mm:ss.fff}] OCR returned empty/whitespace");
                return;
            }

            if (text == "[OCR not available]" || text == "[OCR Error]")
            {
                Console.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] OCR system error: {text}");
                return;
            }

            if (text == _lastText)
            {
                Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss.fff}] Same text as before, ignoring");
                return;
            }
                
            _lastText = text;
            Console.WriteLine($"🎉 >>> NEW DIALOGUE DETECTED: \"{text}\"");
            
            // Add to dialogue catalog with speaker detection
            var entry = _catalog?.AddOrUpdateDialogue(text);
            
            if (entry != null)
            {
                Console.WriteLine($"📤 [{DateTime.Now:HH:mm:ss.fff}] Pipeline: {entry.Speaker} speaks - \"{text.Substring(0, Math.Min(50, text.Length))}...\"");
                
                // TODO: Next steps in pipeline:
                // - Generate TTS audio with OpenAI for this speaker
                // - Play with NAudio with voice profile
                // - Emit events for Twitch integration
                // - Update overlays
            }
        }

        private static void PrintStats()
        {
            var elapsed = DateTime.Now - _startTime;
            var actualFps = _frameCount / elapsed.TotalSeconds;
            var processingRate = _processedFrames / elapsed.TotalSeconds;
            var avgProcessingTime = _totalProcessingTime / (double)_frameCount;
            
            Console.WriteLine("\n=== PERFORMANCE STATS ===");
            Console.WriteLine($"Runtime: {elapsed:mm\\:ss}");
            Console.WriteLine($"Frames captured: {_frameCount} ({actualFps:F1} fps)");
            Console.WriteLine($"Frames processed: {_processedFrames} ({processingRate:F1} fps)");
            Console.WriteLine($"Textboxes found: {_textboxesFound}");
            Console.WriteLine($"Avg processing time: {avgProcessingTime:F1}ms");
            Console.WriteLine($"Efficiency: {(double)_processedFrames/_frameCount*100:F1}% (lower = more duplicate frames skipped)");
            
            if (avgProcessingTime < 60)
            {
                Console.WriteLine("✅ PERFORMANCE TARGET MET - Loop can sustain 15fps with headroom!");
            }
            else
            {
                Console.WriteLine("⚠️ Performance needs optimization");
            }
        }
    }
}