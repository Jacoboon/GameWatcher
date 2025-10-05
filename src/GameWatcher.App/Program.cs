using System.Drawing;
using GameWatcher.App.Ocr;
using GameWatcher.App.Text;
using GameWatcher.App.Vision;
using GameWatcher.App.Mapping;
using GameWatcher.App.Audio;
using GameWatcher.App.Catalog;
using GameWatcher.App.Events;
using GameWatcher.App.Author;
using GameWatcher.App.Capture;

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

// CLI options
string? watchTitle = GetArg("--watch");
int fps = int.TryParse(GetArg("--fps"), out var f) ? Math.Clamp(f, 1, 60) : 20; // default 20fps, aggressive but safe
var testImage = Path.Combine(templatesDir, "FF-TextBox-Position.png");

if (watchTitle is null)
{
    if (!File.Exists(testImage))
    {
        Console.Error.WriteLine($"Templates not found at: {templatesDir}");
        Console.Error.WriteLine("Run scripts/sync_artifacts.ps1 once to import template PNGs.");
        Environment.Exit(2);
    }
}

Console.WriteLine("GameWatcher OCR Smoke Test");
Console.WriteLine($"Root: {root}");
Console.WriteLine($"Templates: {templatesDir}");
if (watchTitle is null)
    Console.WriteLine($"Test Image: {testImage}");
else
    Console.WriteLine($"Watching window: {watchTitle} at {fps} fps");

if (watchTitle is null)
{
    RunStaticSmoke();
}
else
{
    await RunCaptureAsync(watchTitle, fps);
}

return;

async Task RunCaptureAsync(string title, int fps)
{
    var hwnd = WindowFinder.FindByTitleSubstring(title);
    if (hwnd == IntPtr.Zero)
    {
        Console.Error.WriteLine($"Window not found for title substring: {title}");
        Environment.Exit(4);
        return;
    }

    var detector = new TextboxDetector(templatesDir);
    var mapping = new DialogMapping(mapsDir, voicesDir);
    var catalog = new CatalogService(dataDir);
    var emitter = new EventEmitter(dataDir);
    var speakerResolver = new SpeakerResolver(speakersPath);
    var ocr = new TesseractCliOcrEngine();
    var normalizer = new SimpleNormalizer();
    Rectangle? rectCache = null;
    string? lastId = null;
    string? prevHash = null;
    int stableCount = 0;
    int stability = int.TryParse(Environment.GetEnvironmentVariable("GW_STABILITY"), out var sVal) ? Math.Clamp(sVal, 1, 5) : 2;
    using var player = new PlaybackAgent();

    var frameDelay = TimeSpan.FromMilliseconds(1000.0 / fps);
    Console.WriteLine("Press Q to quit.");
    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q) break;
        }

        using var frame = Win32Capture.CaptureClient(hwnd);
        if (frame == null) { await Task.Delay(frameDelay); continue; }

        if (rectCache is null)
        {
            rectCache = detector.DetectTextbox(frame) ?? new Rectangle(0, 0, frame.Width, frame.Height);
        }

        var rect = rectCache.Value;
        using var crop = new Bitmap(rect.Width, rect.Height);
        using (var g = Graphics.FromImage(crop))
        {
            g.DrawImage(frame, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
        }

        try
        {
            // Stability: require N identical hashes before OCR
            var cropHash = ImageHasher.ComputeSHA1(crop);
            if (cropHash == prevHash) stableCount++; else { prevHash = cropHash; stableCount = 1; }
            if (stableCount < stability) { await Task.Delay(frameDelay); continue; }

            var raw = await ocr.ReadTextAsync(crop);
            var norm = normalizer.Normalize(raw);
            if (string.IsNullOrWhiteSpace(norm)) { await Task.Delay(frameDelay); continue; }

            var id = catalog.ComputeId(norm);
            if (id == lastId) { await Task.Delay(frameDelay); continue; }
            lastId = id;

            using var pre = ImagePreprocessor.GrayscaleUpscaleThreshold(crop);
            var speaker = speakerResolver.Resolve(norm);

            if (mapping.TryResolve(norm, out var audio))
            {
                await player.PlayAsync(audio);
                catalog.Record(norm, raw, rect, Path.GetFileName(audio), crop, pre);
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
                catalog.Record(norm, raw, rect, null, crop, pre);
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
            Console.Error.WriteLine("Frame OCR failed: " + ex.Message);
        }

        await Task.Delay(frameDelay);
    }
}

void RunStaticSmoke()
{
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
        using var pre = ImagePreprocessor.GrayscaleUpscaleThreshold(crop);
        var raw = ocr.ReadTextAsync(crop).GetAwaiter().GetResult();
        var norm = new SimpleNormalizer().Normalize(raw);
        Console.WriteLine("--- OCR Raw ---\n" + raw);
        Console.WriteLine("--- OCR Normalized ---\n" + norm);

        var mapping = new DialogMapping(mapsDir, voicesDir);
        var catalog = new CatalogService(dataDir);
        var emitter = new EventEmitter(dataDir);
        var id = catalog.ComputeId(norm);
        var speakerResolver = new SpeakerResolver(speakersPath);
        var speaker = speakerResolver.Resolve(norm);
        if (mapping.TryResolve(norm, out var audio))
        {
            using var player = new PlaybackAgent();
            player.PlayAsync(audio).GetAwaiter().GetResult();
            catalog.Record(norm, raw, rect, Path.GetFileName(audio), crop, pre);
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
            catalog.Record(norm, raw, rect, null, crop, pre);
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
            Console.WriteLine("No audio mapping found.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("OCR failed: " + ex.Message);
        Console.Error.WriteLine("If Tesseract is not installed, install it and/or set TESSERACT_EXE to the full path.");
        Environment.ExitCode = 3;
    }
}

string? GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name) return args[i + 1];
    }
    return null;
}
