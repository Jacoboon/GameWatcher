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
        private static ITextboxDetector? _detector;
        private static SimpleOCR? _ocr;
        private static DialogueCatalog? _catalog;
        private static SpeakerCatalog? _speakerCatalog; // New: Speaker profiles and voice management
        private static string _lastText = "";
        private static readonly object _lockObject = new();
        
        // New: Stable frame detection
        private static bool _waitingForStableFrame = true; // Track if we're looking for stability  
        private static bool _processingStableFrame = false; // Prevent duplicate processing
        
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
            _detector = new SimpleTextboxDetector(); // Proven working blue field detection (23 textboxes found)
            _ocr = new SimpleOCR();
            _catalog = new DialogueCatalog();
            _speakerCatalog = new SpeakerCatalog(); // New: Initialize speaker profiles
            
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
                    // Step 2: Compare with last frame for STABILITY detection (new algorithm)
                    if (_lastFrame != null && ScreenCapture.AreImagesSimilar(_lastFrame, currentFrame, 500))
                    {
                        // Frame is SAME as previous - this indicates STABILITY!
                        if (_waitingForStableFrame && !_processingStableFrame)
                        {
                            // First stable frame detected - process it for textbox!
                            _waitingForStableFrame = false;
                            _processingStableFrame = true;
                            
                            Console.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] STABLE FRAME DETECTED - Processing for textbox");
                            // Continue processing below to check for textbox
                        }
                        else
                        {
                            // Already processed this stable frame or still processing, skip
                            currentFrame.Dispose();
                            return;
                        }
                    }
                    else
                    {
                        // Frame has CHANGED - reset stability tracking and skip textbox detection
                        _waitingForStableFrame = true;
                        _processingStableFrame = false;
                        
                        // Frame has changed, dispose old frame and skip processing (no textbox check)
                        _lastFrame?.Dispose();
                        _lastFrame = new Bitmap(currentFrame);
                        currentFrame.Dispose();
                        return; // Skip textbox detection for changing frames
                    }

                    // Only reach here if we have a STABLE frame that needs processing
                }

                _processedFrames++;

                // Step 3: Check STABLE frame for textbox (new optimized algorithm)
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
                    
                    // Reset processing flag after textbox processing is complete
                    _processingStableFrame = false;
                }
                else
                {
                    // No textbox found in stable frame - reset processing flag
                    _processingStableFrame = false;
                    
                    // Only log occasionally to avoid spam
                    if (_frameCount % 200 == 0)
                    {
                        Console.WriteLine($"⭕ [{DateTime.Now:HH:mm:ss.fff}] No textbox found in stable frame (frame {_frameCount})");
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
            
            // Quality filter: Reject obvious OCR garbage before processing
            if (IsOCRGarbage(text))
            {
                Console.WriteLine($"🚫 [{DateTime.Now:HH:mm:ss.fff}] OCR quality filter: Rejecting garbage text");
                return "[REJECTED: Low Quality OCR]";
            }
            
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
        
        /// <summary>
        /// Detects if OCR text is garbage/low quality and should be rejected
        /// </summary>
        private static bool IsOCRGarbage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            
            // Remove whitespace for analysis
            var cleanText = text.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            
            if (cleanText.Length < 3) return true; // Too short to be meaningful
            if (cleanText.Length > 500) return true; // Suspiciously long for game dialogue
            
            // Count different character types
            int letters = 0;
            int digits = 0;
            int punctuation = 0;
            int symbols = 0;
            int spaces = text.Count(c => char.IsWhiteSpace(c));
            
            foreach (char c in cleanText)
            {
                if (char.IsLetter(c)) letters++;
                else if (char.IsDigit(c)) digits++;
                else if (char.IsPunctuation(c)) punctuation++;
                else symbols++;
            }
            
            int totalChars = cleanText.Length;
            
            // Quality heuristics for FF1 dialogue
            
            // 1. Must have reasonable amount of letters (at least 40% of text)
            if ((double)letters / totalChars < 0.4) return true;
            
            // 2. Too many symbols/garbage characters (>30%)
            if ((double)symbols / totalChars > 0.3) return true;
            
            // 3. Too many digits (FF1 dialogue rarely has many numbers)
            if ((double)digits / totalChars > 0.3) return true;
            
            // 4. Check for common OCR garbage patterns
            string lowerText = text.ToLower();
            
            // Multiple consecutive dots/dashes (OCR artifacts)
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[.\-_]{4,}")) return true;
            
            // Too many single characters separated by spaces (OCR fragmentation)
            if (System.Text.RegularExpressions.Regex.Matches(text, @"\b\w\b").Count > totalChars * 0.4) return true;
            
            // Common OCR garbage character sequences
            string[] garbagePatterns = {
                "brc", "pada", "pel", "sree", "ber", "af", "te", "nies", 
                "tara", "sai", "fares", "ay", "pel brc", "pada", "L-. -"
            };
            
            int garbageMatches = garbagePatterns.Count(pattern => lowerText.Contains(pattern));
            if (garbageMatches >= 2) return true; // Multiple garbage patterns = likely junk
            
            // 5. Check for reasonable English-like patterns
            // Must have some common English words or at least vowels in reasonable proportion
            int vowels = lowerText.Count(c => "aeiou".Contains(c));
            if ((double)vowels / letters < 0.15) return true; // Too few vowels for English
            
            // Common FF1 words that indicate real dialogue
            string[] validWords = {
                "king", "princess", "knight", "warrior", "light", "crystal", "garland",
                "cornelia", "elfheim", "sage", "castle", "kingdom", "power", "evil",
                "help", "save", "thank", "you", "i", "am", "is", "the", "and", "of",
                "to", "in", "for", "with", "by", "from", "up", "about", "into",
                "over", "after", "beneath", "under", "above", "but", "not", "or"
            };
            
            int validWordMatches = validWords.Count(word => 
                System.Text.RegularExpressions.Regex.IsMatch(lowerText, $@"\b{word}\b"));
            
            // If we found valid words, it's probably real dialogue
            if (validWordMatches >= 2) return false;
            
            // If no valid words but reasonable character distribution, be conservative
            if (letters >= 5 && (double)letters / totalChars >= 0.6) return false;
            
            // Default: reject if we can't confidently say it's good
            return true;
        }

        private static void ProcessNewDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"⚪ [{DateTime.Now:HH:mm:ss.fff}] OCR returned empty/whitespace");
                return;
            }

            if (text == "[OCR not available]" || text == "[OCR Error]" || text == "[REJECTED: Low Quality OCR]")
            {
                Console.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] OCR system error or quality rejection: {text}");
                return;
            }

            if (text == _lastText)
            {
                Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss.fff}] Same text as before, ignoring");
                return;
            }
                
            _lastText = text;
            Console.WriteLine($"🎉 >>> NEW DIALOGUE DETECTED: \"{text}\"");
            
            // Step 1: Identify speaker using advanced profile matching
            var speakerProfile = _speakerCatalog?.IdentifySpeaker(text) ?? _speakerCatalog?.GetOrCreateGenericSpeaker("NPC");
            
            // Step 2: Add to dialogue catalog with enhanced speaker info
            var entry = _catalog?.AddOrUpdateDialogue(text);
            if (entry != null && speakerProfile != null)
            {
                // Update dialogue entry with speaker profile information
                entry.Speaker = speakerProfile.Name;
                entry.VoiceProfile = speakerProfile.TtsVoiceId;
                
                Console.WriteLine($"🎭 [{DateTime.Now:HH:mm:ss.fff}] Character: {speakerProfile.Name} ({speakerProfile.CharacterType})");
                Console.WriteLine($"🎤 [{DateTime.Now:HH:mm:ss.fff}] Voice: {speakerProfile.TtsVoiceId} | Effects: {speakerProfile.Effects.EnvironmentPreset}");
                Console.WriteLine($"📤 [{DateTime.Now:HH:mm:ss.fff}] Pipeline: \"{text.Substring(0, Math.Min(60, text.Length))}...\"");
                
                // TODO: Next steps in TTS pipeline:
                // - Generate TTS audio with OpenAI using speakerProfile.TtsVoiceId and speakerProfile.TtsSpeed  
                // - Apply NAudio effects from speakerProfile.Effects (reverb, pitch, EQ)
                // - Save audio to speakerProfile-based filename structure
                // - Update entry.AudioPath and entry.HasAudio = true
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
            
            // Show speaker catalog statistics
            Console.WriteLine();
            _speakerCatalog?.ShowStatistics();
        }
    }
}