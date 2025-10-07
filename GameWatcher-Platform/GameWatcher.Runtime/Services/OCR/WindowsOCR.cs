using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using GameWatcher.Runtime.Services.OCR;

namespace GameWatcher.Runtime.Services.OCR
{
    /// <summary>
    /// Windows native OCR engine using Windows.Media.Ocr API
    /// </summary>
    public class WindowsOCR : IOcrEngine
    {
        private OcrEngine? _ocrEngine;
        private bool _isAvailable;

        public bool IsAvailable => _isAvailable;

        public WindowsOCR()
        {
            InitializeEngine();
        }

        private void InitializeEngine()
        {
            try
            {
                // Check if Windows OCR is available
                var availableLanguages = OcrEngine.AvailableRecognizerLanguages;
                Console.WriteLine($"[Windows OCR] Available languages: {availableLanguages.Count}");
                
                if (availableLanguages.Count == 0)
                {
                    Console.WriteLine("[Windows OCR] No OCR languages available on this system");
                    _isAvailable = false;
                    return;
                }

                // Try to get English OCR engine first
                var englishLanguage = availableLanguages.FirstOrDefault(lang => 
                    lang.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));

                if (englishLanguage != null)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(englishLanguage);
                    Console.WriteLine($"[Windows OCR] Using English OCR engine: {englishLanguage.DisplayName}");
                }
                else
                {
                    // Fall back to first available language
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(availableLanguages.First());
                    Console.WriteLine($"[Windows OCR] Using fallback OCR engine: {availableLanguages.First().DisplayName}");
                }

                _isAvailable = _ocrEngine != null;
                Console.WriteLine($"[Windows OCR] Engine initialized: {(_isAvailable ? "✅ Success" : "❌ Failed")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Windows OCR] Initialization error: {ex.Message}");
                _isAvailable = false;
            }
        }

        /// <summary>
        /// Extract text using Windows native OCR with experimental preprocessing
        /// </summary>
        public async Task<string> ExtractTextAsync(Bitmap image)
        {
            if (!_isAvailable || _ocrEngine == null)
            {
                Console.WriteLine("[Windows OCR] Engine not available");
                return "";
            }

            try
            {
                Console.WriteLine($"[Windows OCR] Processing {image.Width}x{image.Height} image");

                // Use raw image directly - Windows OCR works best without preprocessing
                var extractedText = await ProcessRawImage(image);
                
                // Apply post-processing corrections for common FF1 OCR errors
                var correctedText = ApplyFF1Corrections(extractedText);
                
                Console.WriteLine($"[Windows OCR] Raw result: '{extractedText}'");
                Console.WriteLine($"[Windows OCR] Corrected result: '{correctedText}'");
                
                return correctedText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Windows OCR] Error: {ex.Message}");
                return "";
            }
        }
        
        private async Task<string> ProcessRawImage(Bitmap image)
        {
            try
            {
                // Convert to SoftwareBitmap and run OCR
                using var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                var randomAccessStream = stream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                var ocrResult = await _ocrEngine!.RecognizeAsync(softwareBitmap);
                var extractedText = string.Join(" ", ocrResult.Lines.Select(line => line.Text)).Trim();
                
                softwareBitmap.Dispose();
                
                return extractedText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Windows OCR] Raw processing error: {ex.Message}");
                return "";
            }
        }
        
        private string ApplyFF1Corrections(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Common FF1 OCR error corrections based on observed patterns
            var corrections = new Dictionary<string, string>
            {
                {"Ijhen", "When"},
                {"lJhen", "When"}, 
                {"VVhen", "When"},
                {"theri", "then"},
                {"thern", "then"},
                {"Clur", "Our"},
                {"princ:e", "prince"},
                {"bec:ame", "became"},
                {"became", "become"},  // "meant to became" -> "meant to become"
                {"naw", "now"},
                {"ta", "to"},
                {"Cin", "On"},  // "Cin a journey" -> "On a journey"
                {"tor-Ik", "took"},  // "once tor-Ik" -> "once took"
                {"cast le", "castle"},  // "ancient cast le" -> "ancient castle"
                {"Nat", "Not"},  // "Nat a soul" -> "Not a soul"
                {"elf", "elf"}, // Keep as is - this is correct
                {"awaken", "awaken"} // Keep as is - this is correct
            };
            
            var result = text;
            foreach (var correction in corrections)
            {
                result = result.Replace(correction.Key, correction.Value);
            }
            
            return result;
        }

        /// <summary>
        /// Synchronous wrapper for ExtractTextAsync
        /// </summary>
        public string ExtractTextFast(Bitmap image)
        {
            try
            {
                return ExtractTextAsync(image).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Windows OCR] Sync extraction error: {ex.Message}");
                return "";
            }
        }

        public void Dispose()
        {
            _ocrEngine = null;
            _isAvailable = false;
        }
    }
}