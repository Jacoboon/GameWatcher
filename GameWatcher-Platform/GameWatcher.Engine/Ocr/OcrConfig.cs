namespace GameWatcher.Engine.Ocr;

/// <summary>
/// Configuration for OCR processing
/// </summary>
public class OcrConfig
{
    public string Language { get; set; } = "en-US";
    public OcrPreprocessing Preprocessing { get; set; } = new();
    public double ScaleFactor { get; set; } = 2.0;
    public bool EnableThresholding { get; set; } = true;
}

/// <summary>
/// OCR preprocessing configuration
/// </summary>
public class OcrPreprocessing
{
    public bool ConvertToGrayscale { get; set; } = true;
    public bool ApplyThreshold { get; set; } = true;
    public bool ReduceNoise { get; set; } = true;
    public bool EnhanceContrast { get; set; } = false;
}