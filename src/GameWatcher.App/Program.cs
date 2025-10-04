using System.Drawing;
using GameWatcher.App.Ocr;
using GameWatcher.App.Text;
using GameWatcher.App.Vision;
using GameWatcher.App.Mapping;
using GameWatcher.App.Audio;
using GameWatcher.App.Catalog;
using GameWatcher.App.Events;
using GameWatcher.App.Author;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Environment.CurrentDirectory);
    while (dir != null)
    {
        if (dir.EnumerateFiles("GameWatcher.sln").Any() || dir.EnumerateDirectories("assets").Any()) return dir.FullName;
        dir = dir.Parent;
    }
    return Environment.CurrentDirectory;
}

var root = FindRepoRoot();
var templatesDir = Path.Combine(root, "assets", "templates");
var mapsDir = Path.Combine(root, "assets", "maps");
var voicesDir = Path.Combine(root, "assets", "voices");
var dataDir = Path.Combine(root, "data");
var speakersPath = Path.Combine(mapsDir, "speakers.json");
var testImage = Path.Combine(templatesDir, "FF-TextBox-Position.png");

if (!File.Exists(testImage))
{
    Console.Error.WriteLine($"Templates not found at: {templatesDir}");
    Console.Error.WriteLine("Run scripts/sync_artifacts.ps1 once to import template PNGs.");
    Environment.Exit(2);
}

Console.WriteLine("GameWatcher OCR Smoke Test");
Console.WriteLine($"Root: {root}");
Console.WriteLine($"Templates: {templatesDir}");
Console.WriteLine($"Test Image: {testImage}");

Directory.CreateDirectory(Path.Combine(root, "out"));
var outCrop = Path.Combine(root, "out", "crop.png");

using var frame = new Bitmap(testImage);
var detector = new TextboxDetector(templatesDir);
var rect = detector.DetectTextbox(frame) ?? new Rectangle(0, 0, frame.Width, frame.Height);

Console.WriteLine($"Detected textbox: {rect}");

using var crop = new Bitmap(rect.Width, rect.Height);
using (var g = Graphics.FromImage(crop))
{
    g.DrawImage(frame, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
}
crop.Save(outCrop);
Console.WriteLine($"Saved crop: {outCrop}");

var ocr = new TesseractCliOcrEngine();
try
{
    // Preprocess for OCR and catalog
    using var pre = ImagePreprocessor.GrayscaleUpscaleThreshold(crop);
    var raw = await ocr.ReadTextAsync(crop);
    var norm = new SimpleNormalizer().Normalize(raw);
    Console.WriteLine("--- OCR Raw ---\n" + raw);
    Console.WriteLine("--- OCR Normalized ---\n" + norm);

    // Map to audio and play if found
    var mapping = new DialogMapping(mapsDir, voicesDir);
    var catalog = new CatalogService(dataDir);
    var emitter = new EventEmitter(dataDir);
    var id = catalog.ComputeId(norm);
    var speakerResolver = new SpeakerResolver(speakersPath);
    var speaker = speakerResolver.Resolve(norm);
    if (mapping.TryResolve(norm, out var audio))
    {
        Console.WriteLine($"Mapped to: {audio}");
        using var player = new PlaybackAgent();
        await player.PlayAsync(audio);
        Console.WriteLine("Playback complete.");
        catalog.Record(norm, raw, rect, audioFile: Path.GetFileName(audio), crop: crop, preprocessed: pre);
        emitter.Emit(new DialogueEvent
        {
            Type = "dialogue",
            Id = id,
            Normalized = norm,
            Raw = raw,
            Rect = new[] { rect.X, rect.Y, rect.Width, rect.Height },
            Speaker = speaker,
            Audio = Path.GetFileName(audio),
            Timestamp = DateTimeOffset.UtcNow
        });
    }
    else
    {
        Console.WriteLine("No audio mapping found.");
        Console.WriteLine($"Add a mapping to {Path.Combine(mapsDir, "dialogue.en.json")}");
        catalog.Record(norm, raw, rect, audioFile: null, crop: crop, preprocessed: pre);
        emitter.Emit(new DialogueEvent
        {
            Type = "dialogue",
            Id = id,
            Normalized = norm,
            Raw = raw,
            Rect = new[] { rect.X, rect.Y, rect.Width, rect.Height },
            Speaker = speaker,
            Audio = null,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("OCR failed: " + ex.Message);
    Console.Error.WriteLine("If Tesseract is not installed, install it and/or set TESSERACT_EXE to the full path.");
    Environment.ExitCode = 3;
}
