using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZetxFormatter
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(11, 13, 18);
        public static readonly Color Side = Color.FromArgb(15, 17, 24);
        public static readonly Color Panel = Color.FromArgb(18, 21, 28);
        public static readonly Color Card = Color.FromArgb(24, 28, 37);
        public static readonly Color CardHover = Color.FromArgb(31, 36, 48);
        public static readonly Color Border = Color.FromArgb(38, 43, 56);
        public static readonly Color Text = Color.FromArgb(235, 238, 244);
        public static readonly Color Sub = Color.FromArgb(138, 147, 166);
        public static readonly Color Dim = Color.FromArgb(92, 100, 118);
        public static readonly Color Accent = Color.FromArgb(255, 106, 26);
        public static readonly Color Accent2 = Color.FromArgb(255, 160, 60);
        public static readonly Color AccentDark = Color.FromArgb(204, 79, 10);
        public static readonly Color Green = Color.FromArgb(46, 204, 113);
        public static readonly Color Red = Color.FromArgb(255, 77, 94);
        public static readonly Color Yellow = Color.FromArgb(245, 184, 61);
        public static readonly Color Blue = Color.FromArgb(61, 139, 255);

        public static float K = 1f;
        public static int S(float v) { return (int)Math.Round(v * K); }

        public static string FontName = "Segoe UI";
        public static string IconFontName = "Segoe MDL2 Assets";
        public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(FontName, size, st, GraphicsUnit.Point); }
        public static Font FS(float size) { return new Font(FontName + " Semibold", size, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font Icon(float size) { return new Font(IconFontName, size, FontStyle.Regular, GraphicsUnit.Point); }

        public static bool Shot = Environment.GetEnvironmentVariable("ZF_SHOT") == "1";
        static readonly System.Collections.Generic.Dictionary<int, string> gmap = new System.Collections.Generic.Dictionary<int, string> { { 0xE71D, "▦" }, { 0xE8F1, "★" }, { 0xE896, "⇩" }, { 0xE895, "⟳" }, { 0xE770, "⚙" }, { 0xE946, "ⓘ" }, { 0xE8BB, "✕" }, { 0xE922, "☐" }, { 0xE923, "❐" }, { 0xE921, "—" }, { 0xE721, "⌕" }, { 0xE80F, "⌂" }, { 0xE7FC, "♞" }, { 0xE714, "▶" }, { 0xE8A5, "▤" }, { 0xE943, "‹›" }, { 0xE74E, "⬇" }, { 0xE710, "+" }, { 0xE894, "✕" }, { 0xE90F, "✚" }, { 0xE711, "✕" }, { 0xE72B, "←" }, { 0xE7F8, "▣" }, { 0xE950, "▩" }, { 0xE7F4, "▭" }, { 0xE964, "▥" }, { 0xEDA2, "◎" }, { 0xE977, "▢" }, { 0xE81C, "↺" }, { 0xE772, "⚙" }, { 0xE8D7, "⚿" }, { 0xE73E, "✓" }, { 0xE72C, "⟳" }, { 0xE777, "⟳" }, { 0xE77B, "☺" }, { 0xE8F2, "◆" }, { 0xE715, "✉" }, { 0xE8C8, "⧉" }, { 0xE8B5, "⇩" }, { 0xE71E, "⊕" }, { 0xE8FD, "☰" }, { 0xE8AC, "✎" }, { 0xEDE1, "⇧" }, { 0xE74D, "✕" }, { 0xE70F, "✎" }, { 0xE71C, "⚲" }, { 0xE8E5, "▸" } };
        public static string Glyph(string s)
        {
            if (!Shot || string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) { if (c >= 0xE000 && c <= 0xF8FF) { string r; sb.Append(gmap.TryGetValue(c, out r) ? r : "•"); } else sb.Append(c); }
            return sb.ToString();
        }
        public static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) K = g.DpiX / 96f;
            if (K < 1f) K = 1f;
            using (var test = new Font(IconFontName, 10f))
                if (test.Name != IconFontName) IconFontName = "Segoe Fluent Icons";
            if (Shot) IconFontName = "DejaVu Sans";
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags fl)
        {
            TextRenderer.DrawText(g, Glyph(s), f, r, c, fl | TextFormatFlags.NoPrefix);
        }

        public static void DrawLogo(Graphics g, RectangleF r)
        {
            Hq(g);
            using (var p = Round(r, r.Width * 0.24f))
            using (var br = new LinearGradientBrush(r, Accent2, AccentDark, 45f))
                g.FillPath(br, p);
            float w = r.Width, h = r.Height;
            var pts = new PointF[] {
                new PointF(r.X + w*0.24f, r.Y + h*0.24f), new PointF(r.X + w*0.76f, r.Y + h*0.24f),
                new PointF(r.X + w*0.76f, r.Y + h*0.36f), new PointF(r.X + w*0.42f, r.Y + h*0.64f),
                new PointF(r.X + w*0.76f, r.Y + h*0.64f), new PointF(r.X + w*0.76f, r.Y + h*0.76f),
                new PointF(r.X + w*0.24f, r.Y + h*0.76f), new PointF(r.X + w*0.24f, r.Y + h*0.64f),
                new PointF(r.X + w*0.58f, r.Y + h*0.36f), new PointF(r.X + w*0.24f, r.Y + h*0.36f) };
            using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, pts);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr h, string app, string id);
        public static void DarkScroll(Control c)
        {
            try { if (Environment.OSVersion.Platform == PlatformID.Win32NT) SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        }
    }
}
