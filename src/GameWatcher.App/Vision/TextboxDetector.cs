using System;
using System.Drawing;
using System.Globalization;
using System.Drawing.Imaging;
using GameWatcher.Core;

namespace GameWatcher.App.Vision;

internal sealed class TextboxDetector : ITextboxDetector
{
    private readonly string _templateDir;
    private readonly TemplateMatcher _matcher = new();
    private readonly int _inset;
    private readonly double[] _scales;
    private readonly double _minScore;
    private readonly string? _debugDir;
    private readonly (double Xpct, double Ypct, double Wpct, double Hpct) _roiPct;

    public TextboxDetector(string templateDir)
    {
        _templateDir = templateDir;
        // Remove border + a little breathing room for OCR
        if (!int.TryParse(Environment.GetEnvironmentVariable("GW_TEXTBOX_INSET"), out _inset))
            _inset = 19; // default inset matches 19x19 corner templates

        _scales = ParseScales(Environment.GetEnvironmentVariable("GW_DETECT_SCALES"))
                  ?? new[] { 1.0 }; // Just use scale 1.0 - it got perfect scores!
        _minScore = double.TryParse(Environment.GetEnvironmentVariable("GW_DETECT_MINSCORE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
            ? Math.Clamp(ms, 0.0, 1.0)
            : 0.93; // High default to avoid false positives on scenery

        _debugDir = Environment.GetEnvironmentVariable("GW_DETECT_DEBUG_DIR");
        if (string.IsNullOrWhiteSpace(_debugDir) && Environment.GetEnvironmentVariable("GW_DETECT_SAVE_DEBUG") == "1")
        {
            // default to repo out/ directory
            var root = new DirectoryInfo(Environment.CurrentDirectory);
            while (root != null && !root.EnumerateFiles("GameWatcher.sln").Any()) root = root.Parent;
            _debugDir = Path.Combine(root?.FullName ?? Environment.CurrentDirectory, "out");
        }

        _roiPct = ParseRoiPercent(Environment.GetEnvironmentVariable("GW_DETECT_ROI"))
                  ?? (0.0, 0.0, 1.0, 1.0);
    }

    public Rectangle? DetectTextbox(Bitmap frame)
    {
        try
        {
            Console.WriteLine($"[TEXTBOX] Starting detection on {frame.Width}x{frame.Height} frame");
            
            var tlPath = Path.Combine(_templateDir, "FF-TextBox-TL.png");
            var trPath = Path.Combine(_templateDir, "FF-TextBox-TR.png");
            var blPath = Path.Combine(_templateDir, "FF-TextBox-BL.png");
            var brPath = Path.Combine(_templateDir, "FF-TextBox-BR.png");

            Console.WriteLine($"[TEXTBOX] Template directory: {_templateDir}");
            Console.WriteLine($"[TEXTBOX] Checking templates - TL: {File.Exists(tlPath)}, TR: {File.Exists(trPath)}, BL: {File.Exists(blPath)}, BR: {File.Exists(brPath)}");

            if (!(File.Exists(tlPath) && File.Exists(trPath) && File.Exists(blPath) && File.Exists(brPath)))
            {
                Console.WriteLine("[TEXTBOX] Templates missing");
                return null;
            }

            using var tl0 = new Bitmap(tlPath);
            using var tr0 = new Bitmap(trPath);
            using var bl0 = new Bitmap(blPath);
            using var br0 = new Bitmap(brPath);

            // Limit search to ROI to keep NCC fast on 1080p+
            var roi = ToRect(frame.Width, frame.Height, _roiPct);
            roi = Rectangle.Intersect(roi, new Rectangle(0, 0, frame.Width, frame.Height));
            Console.WriteLine($"[TEXTBOX] Search ROI: {roi} (from percentages {_roiPct})");
            using var search = frame.Clone(roi, PixelFormat.Format32bppArgb);

            Rectangle? bestRect = null;
            double bestScore = double.NegativeInfinity;
            (Point pTL, Point pTR, Point pBL, Point pBR, double score, Size tplSize) bestMatch = default;

            Console.WriteLine($"[TEXTBOX] Testing {_scales.Length} scales with min score {_minScore:F3}");
            
            foreach (var scale in _scales)
            {
                try
                {
                    Console.WriteLine($"[TEXTBOX] Testing scale {scale:F3}...");
                    using var tl = ScaleBitmap(tl0, scale);
                    using var tr = ScaleBitmap(tr0, scale);
                    using var bl = ScaleBitmap(bl0, scale);
                    using var br = ScaleBitmap(br0, scale);

                var (pTL, sTL) = _matcher.MatchBest(search, tl);
                var (pTR, sTR) = _matcher.MatchBest(search, tr);
                var (pBL, sBL) = _matcher.MatchBest(search, bl);
                var (pBR, sBR) = _matcher.MatchBest(search, br);

                double minLocal = _minScore;
                Console.WriteLine($"[TEXTBOX] Scale {scale:F3} scores - TL:{sTL:F3} TR:{sTR:F3} BL:{sBL:F3} BR:{sBR:F3}");
                if (sTL < minLocal || sTR < minLocal || sBL < minLocal || sBR < minLocal)
                {
                    Console.WriteLine($"[TEXTBOX] Scale {scale:F3} rejected - scores below {minLocal:F3}");
                    continue;
                }

                int topY = roi.Y + pTL.Y;
                int leftX = roi.X + pTL.X;
                int rightX = roi.X + pTR.X + tr.Width - 1;
                int bottomY = roi.Y + pBL.Y + bl.Height - 1;

                int W = frame.Width, H = frame.Height;
                int topDY = Math.Abs(pTL.Y - pTR.Y);
                int botDY = Math.Abs(pBL.Y - pBR.Y);
                int leftDX = Math.Abs(pTL.X - pBL.X);
                int rightDX = Math.Abs(pTR.X - pBR.X);
                bool geometryOk = topDY < H * 0.02 && botDY < H * 0.02 && leftDX < W * 0.02 && rightDX < W * 0.02;
                if (!geometryOk) continue;

                var rect = Rectangle.FromLTRB(leftX, topY, rightX, bottomY).Inset(_inset, _inset);
                rect = Rectangle.Intersect(rect, new Rectangle(0, 0, frame.Width, frame.Height));
                if (rect.Width < 10 || rect.Height < 10) continue;

                double avg = (sTL + sTR + sBL + sBR) / 4.0;
                
                // Additional geometric constraints for FF1 textbox
                if (!IsValidFF1TextboxGeometry(rect, frame.Width, frame.Height))
                {
                    Console.WriteLine($"[TEXTBOX] Scale {scale:F3} rejected - invalid geometry");
                    continue;
                }
                
                if (!LooksLikeBlueTextbox(frame, rect) || !HasWhiteTextStrokes(frame, rect))
                {
                    Console.WriteLine($"[TEXTBOX] Scale {scale:F3} rejected - failed appearance validation");
                    continue;
                }
                
                // Only accept if significantly better than current best (avoid micro-variations)
                if (avg > bestScore + 0.01) // Require meaningful improvement
                {
                    bestScore = avg;
                    bestRect = rect;
                    bestMatch = (pTL, pTR, pBL, pBR, avg, tr.Size);
                }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TEXTBOX] Scale {scale:F3} failed: {ex.Message}");
                    continue;
                }
            }

            if (bestRect.HasValue)
            {
                Console.WriteLine($"[TEXTBOX] Template detection SUCCESS: {bestRect.Value} (score: {bestScore:F3})");
                SaveDebugIfRequested(frame, bestRect.Value, bestMatch);
                return bestRect.Value;
            }

            // If no confident match
            Console.WriteLine("[TEXTBOX] No match; returning null (no textbox)");
            return null;
        }
        catch
        {
            Console.WriteLine("[TEXTBOX] Exception; returning null (no textbox)");
            return null;
        }
    }

    private static bool LooksLikeBlueTextbox(Bitmap frame, Rectangle rect)
    {
        try
        {
            // Basic size/aspect sanity - stricter constraints
            double w = rect.Width, h = rect.Height;
            double ar = w / Math.Max(1.0, h);
            double wFrac = w / frame.Width;
            double hFrac = h / frame.Height;
            if (ar < 4.0 || ar > 5.5) return false;        // stricter aspect ratio for FF1 textbox
            if (wFrac < 0.50 || wFrac > 0.80) return false; // more constrained width
            if (hFrac < 0.18 || hFrac > 0.26) return false; // more constrained height

            // Color check: inside should be predominately FF1 blue (#4169E1 or similar)
            var inner = rect.Inset(Math.Max(2, (int)Math.Round(Math.Min(rect.Width, rect.Height) * 0.08)),
                                   Math.Max(2, (int)Math.Round(Math.Min(rect.Width, rect.Height) * 0.08)));
            if (inner.Width <= 0 || inner.Height <= 0) return false;

            int stepsX = Math.Max(12, inner.Width / 60); // More sampling points
            int stepsY = Math.Max(6, inner.Height / 40);
            int strongBlue = 0, blueish = 0, nonBlue = 0, total = 0;
            double totalBrightness = 0;
            
            for (int yi = 0; yi < stepsY; yi++)
            {
                int y = inner.Y + (int)Math.Round((yi + 0.5) * inner.Height / stepsY);
                for (int xi = 0; xi < stepsX; xi++)
                {
                    int x = inner.X + (int)Math.Round((xi + 0.5) * inner.Width / stepsX);
                    var c = frame.GetPixel(x, y);
                    
                    // Strong FF1 blue check: Blue dominant and in right range
                    if (c.B >= 180 && c.B > c.R + 40 && c.B > c.G + 40) 
                    {
                        strongBlue++;
                    }
                    // Weaker blue check
                    else if (c.B > c.R + 20 && c.B > c.G + 20 && c.B >= 120) 
                    {
                        blueish++;
                    }
                    else
                    {
                        nonBlue++;
                    }
                    
                    totalBrightness += (c.R + c.G + c.B) / 3.0;
                    total++;
                }
            }
            
            if (total == 0) return false;
            
            double strongBlueRatio = strongBlue / (double)total;
            double totalBlueRatio = (strongBlue + blueish) / (double)total;
            double avgBrightness = totalBrightness / total;
            
            // Must have strong FF1 blue presence
            if (strongBlueRatio < 0.40) return false;   // At least 40% strong blue
            if (totalBlueRatio < 0.75) return false;    // At least 75% blue-ish total
            if (avgBrightness < 80 || avgBrightness > 200) return false; // Right brightness range
            
            // Check for consistent blue pattern (not random blue pixels in scenery)
            if (HasConsistentBluePattern(frame, inner) == false) return false;
            
            return true;
        }
        catch { return false; }
    }
    
    private static bool HasConsistentBluePattern(Bitmap frame, Rectangle rect)
    {
        try
        {
            // Check that blue pixels form a consistent filled region, not scattered
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;
            int quarterWidth = rect.Width / 4;
            int quarterHeight = rect.Height / 4;
            
            // Sample a cross pattern through the center
            int blueInCross = 0, totalInCross = 0;
            
            // Horizontal line through center
            for (int x = rect.X + quarterWidth; x < rect.Right - quarterWidth; x += 4)
            {
                var c = frame.GetPixel(x, centerY);
                if (c.B > c.R + 20 && c.B > c.G + 20 && c.B >= 120) blueInCross++;
                totalInCross++;
            }
            
            // Vertical line through center  
            for (int y = rect.Y + quarterHeight; y < rect.Bottom - quarterHeight; y += 4)
            {
                var c = frame.GetPixel(centerX, y);
                if (c.B > c.R + 20 && c.B > c.G + 20 && c.B >= 120) blueInCross++;
                totalInCross++;
            }
            
            if (totalInCross == 0) return false;
            double crossBlueRatio = blueInCross / (double)totalInCross;
            
            // Center cross should be predominantly blue for a real textbox
            return crossBlueRatio >= 0.80;
        }
        catch { return false; }
    }
    
    private static bool IsValidFF1TextboxGeometry(Rectangle rect, int frameWidth, int frameHeight)
    {
        try
        {
            // FF1 textboxes have very specific positioning and size characteristics
            double wRatio = rect.Width / (double)frameWidth;
            double hRatio = rect.Height / (double)frameHeight;
            double xRatio = rect.X / (double)frameWidth;
            double yRatio = rect.Y / (double)frameHeight;
            
            // FF1 textbox typically appears in specific screen regions
            // Top area (0.05-0.15 from top) or bottom area (0.65-0.85 from top) 
            bool validVerticalPosition = (yRatio >= 0.05 && yRatio <= 0.20) || 
                                        (yRatio >= 0.65 && yRatio <= 0.85);
            
            // Horizontally centered with some tolerance
            double centerXRatio = (rect.X + rect.Width / 2.0) / frameWidth;
            bool reasonablyCentered = centerXRatio >= 0.35 && centerXRatio <= 0.65;
            
            // Size constraints - FF1 textboxes are substantial but not huge
            bool validSize = wRatio >= 0.45 && wRatio <= 0.75 && 
                           hRatio >= 0.15 && hRatio <= 0.30;
            
            // Minimum absolute size (avoid tiny false positives)
            bool minimumSize = rect.Width >= 400 && rect.Height >= 120;
            
            return validVerticalPosition && reasonablyCentered && validSize && minimumSize;
        }
        catch { return false; }
    }

    private static bool HasWhiteTextStrokes(Bitmap frame, Rectangle rect)
    {
        try
        {
            // More focused sampling on text regions (avoid extreme edges)
            var inner = rect.Inset(Math.Max(4, rect.Width / 25), Math.Max(4, rect.Height / 8));
            if (inner.Width <= 0 || inner.Height <= 0) return false;
            
            int stepX = Math.Max(1, inner.Width / 400); // Higher resolution sampling
            int stepY = Math.Max(1, inner.Height / 150);
            int brightWhite = 0, nearWhite = 0, total = 0;
            int textLikeStructures = 0;
            
            for (int y = inner.Y; y < inner.Bottom; y += stepY)
            {
                int rowWhites = 0, rowTotal = 0;
                for (int x = inner.X; x < inner.Right; x += stepX)
                {
                    var c = frame.GetPixel(x, y);
                    
                    // Pure white text pixels
                    if (c.R >= 240 && c.G >= 240 && c.B >= 240) 
                    {
                        brightWhite++;
                        rowWhites++;
                    }
                    // Light gray/anti-aliased text
                    else if (c.R >= 200 && c.G >= 200 && c.B >= 200 && 
                             Math.Abs(c.R - c.G) < 20 && Math.Abs(c.G - c.B) < 20)
                    {
                        nearWhite++;
                        rowWhites++;
                    }
                    
                    rowTotal++;
                    total++;
                }
                
                // Count rows that have reasonable text density (not too sparse, not too dense)
                if (rowTotal > 0)
                {
                    double rowWhiteRatio = rowWhites / (double)rowTotal;
                    if (rowWhiteRatio >= 0.05 && rowWhiteRatio <= 0.60) // 5-60% white in row
                    {
                        textLikeStructures++;
                    }
                }
            }
            
            if (total == 0) return false;
            
            double whiteRatio = (brightWhite + nearWhite) / (double)total;
            int totalRows = (inner.Height + stepY - 1) / stepY;
            double textStructureRatio = totalRows > 0 ? textLikeStructures / (double)totalRows : 0;
            
            // Must have reasonable white text content and text-like row structure
            return whiteRatio >= 0.02 && whiteRatio <= 0.35 &&    // 2-35% white pixels
                   textStructureRatio >= 0.10;                     // At least 10% of rows have text-like density
        }
        catch { return false; }
    }

    private static Bitmap ScaleBitmap(Bitmap src, double scale)
    {
        if (Math.Abs(scale - 1.0) < 1e-6) return (Bitmap)src.Clone();
        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
        return bmp;
    }

    private static double[]? ParseScales(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).Where(v => v > 0).ToArray();
        }
        catch { return null; }
    }

    private static (double, double, double, double)? ParseRoiPercent(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 4) return null;
            var vals = parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture) / 100.0).ToArray();
            return (vals[0], vals[1], vals[2], vals[3]);
        }
        catch { return null; }
    }

    private static Rectangle ToRect(int width, int height, (double Xpct, double Ypct, double Wpct, double Hpct) pct)
    {
        int x = (int)Math.Round(width * pct.Xpct);
        int y = (int)Math.Round(height * pct.Ypct);
        int w = (int)Math.Round(width * pct.Wpct);
        int h = (int)Math.Round(height * pct.Hpct);
        return new Rectangle(x, y, Math.Min(w, width - x), Math.Min(h, height - y));
    }

    private void SaveDebugIfRequested(Bitmap frame, Rectangle rect, (Point pTL, Point pTR, Point pBL, Point pBR, double score, Size tplSize) match)
    {
        if (string.IsNullOrWhiteSpace(_debugDir))
        {
            Console.WriteLine("[TEXTBOX] Debug directory not set, skipping debug save");
            return;
        }
        Console.WriteLine($"[TEXTBOX] Saving debug image to: {_debugDir}");
        try
        {
            Directory.CreateDirectory(_debugDir!);
            using var copy = new Bitmap(frame.Width, frame.Height);
            using (var g = Graphics.FromImage(copy))
            {
                g.DrawImage(frame, new Rectangle(0, 0, copy.Width, copy.Height));

                // draw corners if provided
                if (match.tplSize.Width > 0 && match.tplSize.Height > 0)
                {
                    using var penC = new Pen(Color.Red, 2);
                    g.DrawRectangle(penC, new Rectangle(match.pTL, match.tplSize));
                    g.DrawRectangle(penC, new Rectangle(match.pTR, match.tplSize));
                    g.DrawRectangle(penC, new Rectangle(match.pBL, match.tplSize));
                    g.DrawRectangle(penC, new Rectangle(match.pBR, match.tplSize));
                }

                using var pen = new Pen(Color.Lime, 3);
                g.DrawRectangle(pen, rect);
            }
            var name = $"detect_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
            var path = Path.Combine(_debugDir!, name);
            copy.Save(path);
            Console.WriteLine($"[TEXTBOX] Debug image saved: {name}");
        }
        catch { }
    }
}
