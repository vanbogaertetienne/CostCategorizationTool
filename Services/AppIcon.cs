using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CostCategorizationTool.Services;

/// <summary>
/// Generates the application icon at runtime — a dark navy rounded square
/// with four coloured horizontal bars representing expense categories.
/// </summary>
internal static class AppIcon
{
    private static Icon? _cache;
    public static Icon Get() => _cache ??= Build();

    // ── Build a multi-resolution Icon ────────────────────────────────────────

    private static Icon Build()
    {
        using var bmp32 = Draw(32);
        using var bmp16 = Draw(16);

        using var ms = new MemoryStream();
        WriteIco(ms, bmp32, bmp16);
        ms.Position = 0;
        return new Icon(ms);
    }

    // ── Draw at a given pixel size ────────────────────────────────────────────

    private static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size / 32f;   // scale factor relative to 32-px master

        // ── Background: dark navy rounded square ──────────────────────────────
        var bg = new RectangleF(0f, 0f, size, size);
        using (var path = RoundRectF(bg, 5f * s))
        using (var brush = new SolidBrush(Color.FromArgb(14, 42, 71)))
            g.FillPath(brush, path);

        // ── Four horizontal bars (a tiny spending-breakdown chart) ────────────
        // Coordinates are in the 32-px grid and scaled by s.
        //   (x, y, w, h) — all in 32-px units
        (float x, float y, float w, float h, Color c)[] bars =
        {
            (3f, 5f,  22f, 5f, Color.FromArgb(76,  175, 80)),   // green  – largest slice
            (3f, 12f, 16f, 5f, Color.FromArgb(255, 152,  0)),   // amber  – medium
            (3f, 19f, 19f, 5f, Color.FromArgb(33,  150, 243)),  // blue   – medium-large
            (3f, 26f, 11f, 5f, Color.FromArgb(233,  30,  99)),  // pink   – smallest
        };

        foreach (var (x, y, w, h, c) in bars)
        {
            var rect = new RectangleF(x * s, y * s, w * s, h * s);
            using var path  = RoundRectF(rect, 1.5f * s);
            using var brush = new SolidBrush(c);
            g.FillPath(brush, path);
        }

        // ── Small white "€" coin in the top-right corner ──────────────────────
        if (size >= 24)   // skip at 16 px — too tiny to be legible
        {
            float fs   = Math.Max(6f, 7f * s);
            float cx   = 25f * s;
            float cy   = 6.5f * s;
            float r    = 5f * s;

            // Filled white circle
            using var circleBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
            g.FillEllipse(circleBrush, cx - r, cy - r, r * 2, r * 2);

            // "€" glyph in navy
            using var font = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sb   = new SolidBrush(Color.FromArgb(14, 42, 71));
            var fmt = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("€", font, sb, cx, cy + 0.5f * s, fmt);
        }

        return bmp;
    }

    // ── Rounded-rectangle helper ──────────────────────────────────────────────

    private static GraphicsPath RoundRectF(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X,             r.Y,              d, d, 180, 90);
        path.AddArc(r.Right - d,     r.Y,              d, d, 270, 90);
        path.AddArc(r.Right - d,     r.Bottom - d,     d, d,   0, 90);
        path.AddArc(r.X,             r.Bottom - d,     d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    // ── ICO file writer (PNG-inside-ICO, Vista+ format) ──────────────────────

    private static void WriteIco(Stream stream, params Bitmap[] frames)
    {
        // Encode each frame as PNG
        var pngs = frames.Select(f =>
        {
            using var ms = new MemoryStream();
            f.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToArray();

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICO header
        w.Write((short)0);               // reserved
        w.Write((short)1);               // type = icon
        w.Write((short)pngs.Length);     // image count

        // Directory entries
        int dataOffset = 6 + 16 * pngs.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            int sz = frames[i].Width;
            w.Write((byte)(sz >= 256 ? 0 : sz));   // width  (0 = 256)
            w.Write((byte)(sz >= 256 ? 0 : sz));   // height
            w.Write((byte)0);                       // colour count (0 = >8 bpp)
            w.Write((byte)0);                       // reserved
            w.Write((short)1);                      // planes
            w.Write((short)32);                     // bit depth
            w.Write(pngs[i].Length);                // data size
            w.Write(dataOffset);                    // data offset
            dataOffset += pngs[i].Length;
        }

        // Image data
        foreach (var png in pngs)
            w.Write(png);
    }
}
