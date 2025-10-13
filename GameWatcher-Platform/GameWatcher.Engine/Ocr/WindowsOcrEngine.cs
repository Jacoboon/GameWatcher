using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace GameWatcher.Engine.Ocr;

/// <summary>
/// Windows native OCR engine using Windows Runtime API
/// Ported from V1 SimpleLoop with 2x scaling + grayscale preprocessing (proven best for FF1)
/// </summary>
public class WindowsOcrEngine : IOcrEngine
{
    private OcrEngine? _ocrEngine;
    private bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public WindowsOcrEngine()
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

    public async Task<string> ExtractTextAsync(Bitmap image)
    {
        if (!_isAvailable || _ocrEngine == null)
        {
            return "";
        }

        try
        {
            // Use raw image directly - Windows OCR works best without preprocessing
            var extractedText = await ProcessImageAsync(image);
            return extractedText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Windows OCR] Error: {ex.Message}");
            return "";
        }
    }

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

    private async Task<string> ProcessImageAsync(Bitmap image)
    {
        try
        {
            // Convert to SoftwareBitmap and run OCR
            using var stream = new MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
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
            Console.WriteLine($"[Windows OCR] Processing error: {ex.Message}");
            return "";
        }
    }

    public void Dispose()
    {
        _ocrEngine = null;
        _isAvailable = false;
    }
}