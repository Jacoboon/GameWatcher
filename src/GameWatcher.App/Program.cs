using System.Drawing;
using System.Globalization;
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
    string? lastOcrHash = null; // guard to OCR once per stable crop
    int stableCount = 0;
    int stability = int.TryParse(Environment.GetEnvironmentVariable("GW_STABILITY"), out var sVal) ? Math.Clamp(sVal, 1, 5) : 2;
    using var player = new PlaybackAgent();
    int frameIndex = 0;
    int redetectEvery = int.TryParse(Environment.GetEnvironmentVariable("GW_REDETECT_FRAMES"), out var r)
        ? Math.Max(1, r)
        : Math.Max(1, fps * 3); // by default re-detect every 3s

    var frameDelay = TimeSpan.FromMilliseconds(1000.0 / fps);
    Console.WriteLine("Press Q to quit.");
    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q) break;
        }

        using var frame = CaptureService.CaptureWindow(hwnd);
        if (frame == null) { await Task.Delay(frameDelay); continue; }

        if (rectCache is null || (frameIndex % redetectEvery == 0))
        {
            var forcedRect = Environment.GetEnvironmentVariable("GW_TEXTBOX_RECT");
            if (!string.IsNullOrWhiteSpace(forcedRect) && TryParseRect(forcedRect, out var forced))
            {
                rectCache = Rectangle.Intersect(forced, new Rectangle(0, 0, frame.Width, frame.Height));
            }
            else
            {
                var detected = detector.DetectTextbox(frame);
                // if rect changed, reset lastOcrHash so we can OCR again
                var before = rectCache;
                rectCache = detected; // allow null when no textbox is present
                if (before != rectCache) lastOcrHash = null;
        }
        }

        if (!rectCache.HasValue) { await Task.Delay(frameDelay); continue; }
        var rect = rectCache.Value;
        if (rect.Width <= 0 || rect.Height <= 0) { rectCache = null; await Task.Delay(frameDelay); continue; }
        using var crop = new Bitmap(rect.Width, rect.Height);
        using (var g = Graphics.FromImage(crop))
        {
            g.DrawImage(frame, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
        }

        try
        {
            // Stability: require N identical hashes before OCR
            // Hash a slightly in-set region to avoid flicker artifacts (e.g., arrow)
            double insetPct = double.TryParse(Environment.GetEnvironmentVariable("GW_HASH_INSET_PCT"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ip)
                ? Math.Clamp(ip, 0.0, 0.45)
                : 0.08; // default 8% inset on all sides
            var hr = new Rectangle(
                (int)Math.Round(crop.Width * insetPct),
                (int)Math.Round(crop.Height * insetPct),
                (int)Math.Round(crop.Width * (1 - 2 * insetPct)),
                (int)Math.Round(crop.Height * (1 - 2 * insetPct))
            );
            var cropHash = ImageHasher.ComputeSHA1(crop, hr);
            if (cropHash == prevHash) stableCount++; else { prevHash = cropHash; stableCount = 1; }
            if (stableCount < stability) { await Task.Delay(frameDelay); continue; }
            if (cropHash == lastOcrHash) { await Task.Delay(frameDelay); continue; }

            var raw = await ocr.ReadTextAsync(crop);
            var norm = normalizer.Normalize(raw);
            if (string.IsNullOrWhiteSpace(norm)) { await Task.Delay(frameDelay); continue; }

            var id = catalog.ComputeId(norm);
            if (id == lastId) { await Task.Delay(frameDelay); continue; }
            lastId = id;

            using var pre = ImagePreprocessor.GrayscaleUpscaleThreshold(crop);
            // optional: save detection overlay into data/detect for this line
            SaveDetectOverlay(dataDir, frame, rect, id);
            if (Environment.GetEnvironmentVariable("GW_SAVE_CROP") == "1")
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(root, "out"));
                    crop.Save(Path.Combine(root, "out", "crop_latest.png"));
                    pre.Save(Path.Combine(root, "out", "pre_latest.png"));
                }
                catch { }
            }
            // hot-reload maps so authoring updates are picked up live
            mapping.RefreshIfChanged();
            speakerResolver.RefreshIfChanged();
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
        frameIndex++;
        lastOcrHash = prevHash; // the hash that passed stability and was processed
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

static bool TryParseRect(string s, out Rectangle rect)
{
    rect = Rectangle.Empty;
    try
    {
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4) return false;
        int x = int.Parse(parts[0]);
        int y = int.Parse(parts[1]);
        int w = int.Parse(parts[2]);
        int h = int.Parse(parts[3]);
        if (w <= 0 || h <= 0) return false;
        rect = new Rectangle(x, y, w, h);
        return true;
    }
    catch { return false; }
}

static void SaveDetectOverlay(string dataRoot, Bitmap frame, Rectangle rect, string id)
{
    try
    {
        if (Environment.GetEnvironmentVariable("GW_SAVE_DETECT") != "1") return;
        var dir = Path.Combine(dataRoot, "detect");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, id + ".png");
        if (File.Exists(path)) return; // keep first example
        using var copy = new Bitmap(frame.Width, frame.Height);
        using (var g = Graphics.FromImage(copy))
        {
            g.DrawImage(frame, new Rectangle(0, 0, copy.Width, copy.Height));
            using var pen = new Pen(Color.Lime, 3);
            g.DrawRectangle(pen, rect);
        }
        copy.Save(path);
    }
    catch { }
}
