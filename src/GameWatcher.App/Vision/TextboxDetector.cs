using System;
using System.Drawing;
using GameWatcher.Core;

namespace GameWatcher.App.Vision;

internal sealed class TextboxDetector : ITextboxDetector
{
    private readonly string _templateDir;
    private readonly TemplateMatcher _matcher = new();
    private readonly int _inset;

    public TextboxDetector(string templateDir)
    {
        _templateDir = templateDir;
        // Remove border + a little breathing room for OCR
        if (!int.TryParse(Environment.GetEnvironmentVariable("GW_TEXTBOX_INSET"), out _inset))
            _inset = 19; // default inset matches 19x19 corner templates
    }

    public Rectangle? DetectTextbox(Bitmap frame)
    {
        try
        {
            var tlPath = Path.Combine(_templateDir, "FF-TextBox-TL.png");
            var trPath = Path.Combine(_templateDir, "FF-TextBox-TR.png");
            var blPath = Path.Combine(_templateDir, "FF-TextBox-BL.png");
            var brPath = Path.Combine(_templateDir, "FF-TextBox-BR.png");

            if (!(File.Exists(tlPath) && File.Exists(trPath) && File.Exists(blPath) && File.Exists(brPath)))
                return StaticFallback(frame);

            using var tl = new Bitmap(tlPath);
            using var tr = new Bitmap(trPath);
            using var bl = new Bitmap(blPath);
            using var br = new Bitmap(brPath);

            var (pTL, sTL) = _matcher.MatchBest(frame, tl);
            var (pTR, sTR) = _matcher.MatchBest(frame, tr);
            var (pBL, sBL) = _matcher.MatchBest(frame, bl);
            var (pBR, sBR) = _matcher.MatchBest(frame, br);

            // Quick sanity checks: scores and geometry
            double minScore = 0.6; // conservative NCC threshold
            if (sTL < minScore || sTR < minScore || sBL < minScore || sBR < minScore)
                return StaticFallback(frame);

            // Normalize points to the inner text region by offsetting template sizes
            var topY = pTL.Y; // assume templates include the border; we'll inset later
            var leftX = pTL.X;
            var rightX = pTR.X + tr.Width - 1;
            var bottomY = pBL.Y + bl.Height - 1;

            // Validate approx parallel edges
            int topDY = Math.Abs(pTL.Y - pTR.Y);
            int botDY = Math.Abs(pBL.Y - pBR.Y);
            int leftDX = Math.Abs(pTL.X - pBL.X);
            int rightDX = Math.Abs(pTR.X - pBR.X);

            int W = frame.Width, H = frame.Height;
            bool geometryOk = topDY < H * 0.02 && botDY < H * 0.02 && leftDX < W * 0.02 && rightDX < W * 0.02;
            if (!geometryOk)
                return StaticFallback(frame);

            var rect = Rectangle.FromLTRB(leftX, topY, rightX, bottomY).Inset(_inset, _inset);
            // Clamp to image
            rect = Rectangle.Intersect(rect, new Rectangle(0, 0, frame.Width, frame.Height));
            if (rect.Width < 10 || rect.Height < 10)
                return StaticFallback(frame);

            return rect;
        }
        catch
        {
            return StaticFallback(frame);
        }
    }

    private Rectangle StaticFallback(Bitmap frame)
    {
        // Bottom 35% height, 80% width, centered
        int w = (int)(frame.Width * 0.8);
        int h = (int)(frame.Height * 0.32);
        int x = (frame.Width - w) / 2;
        int y = frame.Height - h - (int)(frame.Height * 0.06);
        var rect = new Rectangle(x, y, w, h).Inset(_inset, _inset);
        return Rectangle.Intersect(rect, new Rectangle(0, 0, frame.Width, frame.Height));
    }
}
