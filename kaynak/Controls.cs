using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZetxFormatter
{
    public class XControl : Control
    {
        protected bool hover, down;
        public XControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    }

    public class FlatButton : XControl
    {
        public string Glyph;
        public int Style; // 0 = accent, 1 = ghost, 2 = danger, 3 = green
        public float Radius = 8;
        public FlatButton(string text, int style = 0, string glyph = null)
        {
            Text = text; Style = style; Glyph = glyph;
            Font = Theme.FS(10f);
            Height = Theme.S(38);
        }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill, fore = Color.White, border = Color.Empty;
            if (Style == 0) fill = down ? Theme.AccentDark : hover ? Theme.Accent2 : Theme.Accent;
            else if (Style == 2) fill = hover ? Color.FromArgb(255, 100, 115) : Theme.Red;
            else if (Style == 3) fill = hover ? Color.FromArgb(70, 220, 135) : Theme.Green;
            else { fill = hover ? Theme.CardHover : Theme.Card; border = hover ? Theme.Accent : Theme.Border; fore = Theme.Text; }
            if (!Enabled) { fill = Theme.Card; fore = Theme.Dim; border = Theme.Border; }
            using (var p = Theme.Round(r, Theme.S(Radius)))
            {
                if (Style == 0 && Enabled)
                    using (var br = new LinearGradientBrush(r, Theme.Mix(fill, Color.White, 0.12f), fill, 90f)) g.FillPath(br, p);
                else using (var br = new SolidBrush(fill)) g.FillPath(br, p);
                if (border != Color.Empty) using (var pen = new Pen(border, 1f)) g.DrawPath(pen, p);
            }
            if (Glyph != null)
            {
                using (var fi = Theme.Icon(Font.Size * 1.05f))
                {
                    var ts = TextRenderer.MeasureText(Text, Font);
                    int gw = Theme.S(22);
                    int total = gw + ts.Width;
                    int x = (Width - total) / 2;
                    Theme.DrawText(g, Glyph, fi, fore, new Rectangle(x, 0, gw, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    Theme.DrawText(g, Text, Font, fore, new Rectangle(x + gw, 0, ts.Width + 4, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
            else Theme.DrawText(g, Text, Font, fore, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class NavButton : XControl
    {
        public string Glyph; public bool Selected; public string Badge;
        public NavButton(string text, string glyph) { Text = text; Glyph = glyph; Height = Theme.S(46); Font = Theme.FS(10.5f); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(Theme.S(10), Theme.S(3), Width - Theme.S(20), Height - Theme.S(6));
            if (Selected || hover)
                using (var p = Theme.Round(r, Theme.S(8)))
                using (var b = new SolidBrush(Selected ? Color.FromArgb(40, 255, 106, 26) : Theme.Card)) g.FillPath(b, p);
            if (Selected)
                using (var p = Theme.Round(new RectangleF(Theme.S(10), Theme.S(13), Theme.S(4), Height - Theme.S(26)), Theme.S(2)))
                using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
            var c = Selected ? Theme.Text : hover ? Theme.Text : Theme.Sub;
            using (var fi = Theme.Icon(12f))
                Theme.DrawText(g, Glyph, fi, Selected ? Theme.Accent : c, new Rectangle(Theme.S(24), 0, Theme.S(30), Height), TextFormatFlags.VerticalCenter);
            Theme.DrawText(g, Text, Font, c, new Rectangle(Theme.S(56), 0, Width - Theme.S(60), Height), TextFormatFlags.VerticalCenter);
            if (!string.IsNullOrEmpty(Badge))
            {
                var ts = TextRenderer.MeasureText(Badge, Theme.FS(8.5f));
                var br = new RectangleF(Width - Theme.S(22) - ts.Width, (Height - Theme.S(20)) / 2f, ts.Width + Theme.S(8), Theme.S(20));
                using (var p = Theme.Round(br, Theme.S(10))) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                using (var f = Theme.FS(8.5f)) Theme.DrawText(g, Badge, f, Color.White, Rectangle.Round(br), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    public class Pill : XControl
    {
        public bool Selected; public int Count = -1;
        public Pill(string text) { Text = text; Font = Theme.FS(9.5f); Height = Theme.S(32); }
        public void Fit()
        {
            string t = Count >= 0 ? Text + "  " + Count : Text;
            Width = TextRenderer.MeasureText(t, Font).Width + Theme.S(28);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var p = Theme.Round(r, r.Height / 2))
            {
                if (Selected) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                else
                {
                    using (var b = new SolidBrush(hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, p);
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                }
            }
            string t = Count >= 0 ? Text + "  " + Count : Text;
            Theme.DrawText(g, t, Font, Selected ? Color.White : Theme.Sub, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class Toggle : XControl
    {
        bool on; public event EventHandler Changed;
        public bool On { get { return on; } set { on = value; Invalidate(); } }
        public Toggle() { Size = new Size(Theme.S(46), Theme.S(24)); }
        protected override void OnClick(EventArgs e) { On = !On; if (Changed != null) Changed(this, e); base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var p = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Border)) g.FillPath(b, p);
            float d = Height - Theme.S(6);
            float x = on ? Width - d - Theme.S(3) : Theme.S(3);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, Theme.S(3), d, d);
        }
    }

    public class ProgressX : Control
    {
        float v; public float Value { get { return v; } set { v = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public Color Bar = Theme.Accent;
        public ProgressX() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; Height = Theme.S(10); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Round(r, r.Height / 2)) using (var b = new SolidBrush(Theme.Border)) g.FillPath(b, p);
            if (v > 0.005f)
            {
                var fr = new RectangleF(0, 0, Math.Max(Height, (Width - 1) * v), Height - 1);
                using (var p = Theme.Round(fr, fr.Height / 2))
                using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Theme.Mix(Bar, Color.White, 0.25f), Bar, 0f)) g.FillPath(b, p);
            }
        }
    }

    public class AppCard : XControl
    {
        public AppItem App;
        public event EventHandler Toggled;
        public Func<AppItem, bool> GetChecked; public Action<AppItem> Toggle;
        bool Sel { get { return GetChecked != null ? GetChecked(App) : App.Selected; } }
        public AppCard(AppItem a) { App = a; Size = new Size(Theme.S(268), Theme.S(78)); Margin = new Padding(Theme.S(6)); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (Toggle != null) Toggle(App); else App.Selected = !App.Selected;
            Invalidate();
            if (Toggled != null) Toggled(this, e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var cc = Catalog.CatColor(App.Category);
            bool sel = Sel;
            using (var p = Theme.Round(r, Theme.S(10)))
            {
                Color fill = sel ? Theme.Mix(Theme.Card, Theme.Accent, 0.10f) : hover ? Theme.CardHover : Theme.Card;
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                using (var pen = new Pen(sel ? Theme.Accent : hover ? Theme.Mix(Theme.Border, Theme.Accent, 0.4f) : Theme.Border, sel ? 1.6f : 1f)) g.DrawPath(pen, p);
            }
            // icon
            int isz = Theme.S(42);
            var ir = new RectangleF(Theme.S(14), (Height - isz) / 2f, isz, isz);
            if (App.Icon != null)
            {
                using (var p = Theme.Round(ir, Theme.S(10))) using (var b = new SolidBrush(Color.FromArgb(36, 41, 54))) g.FillPath(b, p);
                int pad = Theme.S(7);
                g.DrawImage(App.Icon, new RectangleF(ir.X + pad, ir.Y + pad, ir.Width - pad * 2, ir.Height - pad * 2));
            }
            else
            {
                using (var p = Theme.Round(ir, Theme.S(10)))
                using (var b = new LinearGradientBrush(ir, Theme.Mix(cc, Color.White, 0.15f), Theme.Mix(cc, Color.Black, 0.25f), 45f)) g.FillPath(b, p);
                string ini = Initials(App.Name);
                using (var f = Theme.F(13f, FontStyle.Bold)) Theme.DrawText(g, ini, f, Color.White, Rectangle.Round(ir), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int tx = Theme.S(68), right = Width - Theme.S(42);
            using (var f = Theme.FS(10.5f)) Theme.DrawText(g, App.Name, f, Theme.Text, new Rectangle(tx, Theme.S(13), right - tx, Theme.S(22)), TextFormatFlags.EndEllipsis);
            using (var f = Theme.F(8.5f)) Theme.DrawText(g, App.Desc, f, Theme.Sub, new Rectangle(tx, Theme.S(35), right - tx + Theme.S(28), Theme.S(18)), TextFormatFlags.EndEllipsis);
            if (App.Installed)
            {
                using (var f = Theme.FS(7.5f))
                {
                    var rr = new RectangleF(tx, Theme.S(55), Theme.S(52), Theme.S(15));
                    using (var p = Theme.Round(rr, Theme.S(7))) using (var b = new SolidBrush(Color.FromArgb(40, 46, 204, 113))) g.FillPath(b, p);
                    Theme.DrawText(g, "KURULU", f, Theme.Green, Rectangle.Round(rr), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            else if (App.Source == "custom")
            {
                using (var f = Theme.FS(7.5f))
                {
                    var rr = new RectangleF(tx, Theme.S(55), Theme.S(42), Theme.S(15));
                    using (var p = Theme.Round(rr, Theme.S(7))) using (var b = new SolidBrush(Color.FromArgb(45, 255, 145, 40))) g.FillPath(b, p);
                    Theme.DrawText(g, "ÖZEL", f, Theme.Accent2, Rectangle.Round(rr), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    using (var f2 = Theme.F(7.5f)) Theme.DrawText(g, App.CustomKind == "file" ? "Dosya  •  sağ tık: düzenle" : "Link  •  sağ tık: düzenle", f2, Theme.Dim, new Rectangle(tx + Theme.S(48), Theme.S(55), Theme.S(160), Theme.S(15)), TextFormatFlags.VerticalCenter);
                }
            }
            else if (App.Source == "office")
            {
                using (var f = Theme.F(7.5f)) Theme.DrawText(g, "Microsoft 365  •  lisans/hesap gerekir", f, Theme.Dim, new Rectangle(tx, Theme.S(55), Theme.S(200), Theme.S(15)), TextFormatFlags.VerticalCenter);
            }
            else if (App.Source == "msstore")
            {
                using (var f = Theme.F(7.5f)) Theme.DrawText(g, "Microsoft Store", f, Theme.Dim, new Rectangle(tx, Theme.S(55), Theme.S(120), Theme.S(15)), TextFormatFlags.VerticalCenter);
            }
            // checkbox
            var cr = new RectangleF(Width - Theme.S(34), Theme.S(12), Theme.S(20), Theme.S(20));
            using (var p = Theme.Round(cr, Theme.S(6)))
            {
                if (sel)
                {
                    using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                    using (var pen = new Pen(Color.White, Theme.S(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[] { new PointF(cr.X + cr.Width * 0.25f, cr.Y + cr.Height * 0.52f), new PointF(cr.X + cr.Width * 0.43f, cr.Y + cr.Height * 0.70f), new PointF(cr.X + cr.Width * 0.76f, cr.Y + cr.Height * 0.32f) });
                }
                else using (var pen = new Pen(hover ? Theme.Sub : Theme.Dim, 1.4f)) g.DrawPath(pen, p);
            }
        }
        public static string Initials(string n)
        {
            var parts = n.Replace("(", "").Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }
    }

    public class AddCard : XControl
    {
        public string Sub;
        public AddCard(string text, string sub) { Text = text; Sub = sub; Size = new Size(Theme.S(268), Theme.S(78)); Margin = new Padding(Theme.S(6)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var p = Theme.Round(r, Theme.S(10)))
            {
                if (hover) using (var b = new SolidBrush(Color.FromArgb(22, 255, 106, 26))) g.FillPath(b, p);
                using (var pen = new Pen(hover ? Theme.Accent : Theme.Dim, 1.4f) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, p);
            }
            var c = new RectangleF(Theme.S(14), (Height - Theme.S(42)) / 2f, Theme.S(42), Theme.S(42));
            using (var p = Theme.Round(c, Theme.S(10))) using (var b = new SolidBrush(Color.FromArgb(40, 255, 106, 26))) g.FillPath(b, p);
            using (var pen = new Pen(Theme.Accent, Theme.S(2.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2, m = Theme.S(9);
                g.DrawLine(pen, cx - m, cy, cx + m, cy); g.DrawLine(pen, cx, cy - m, cx, cy + m);
            }
            using (var f = Theme.FS(10.5f)) Theme.DrawText(g, Text, f, hover ? Theme.Accent2 : Theme.Text, new Rectangle(Theme.S(68), Theme.S(16), Width - Theme.S(76), Theme.S(22)), TextFormatFlags.EndEllipsis);
            using (var f = Theme.F(8.5f)) Theme.DrawText(g, Sub, f, Theme.Sub, new Rectangle(Theme.S(68), Theme.S(38), Width - Theme.S(76), Theme.S(30)), TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    public class InstallRow : Control
    {
        public AppItem App; public string Status = "Sırada bekliyor"; public Color StatusColor = Theme.Dim; public int State; // 0 wait 1 run 2 ok 3 err 4 skip
        public float Progress = -1;
        int spin;
        public InstallRow(AppItem a)
        {
            App = a; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel; Height = Theme.S(54); Margin = new Padding(0, 0, 0, Theme.S(6));
        }
        public void Tick() { if (State == 1) { spin = (spin + 30) % 360; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g); g.Clear(BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var p = Theme.Round(r, Theme.S(8)))
            {
                using (var b = new SolidBrush(State == 1 ? Theme.Mix(Theme.Card, Theme.Accent, 0.08f) : Theme.Card)) g.FillPath(b, p);
                using (var pen = new Pen(State == 1 ? Theme.Accent : Theme.Border)) g.DrawPath(pen, p);
            }
            int isz = Theme.S(32);
            var ir = new RectangleF(Theme.S(12), (Height - isz) / 2f, isz, isz);
            if (App.Icon != null)
            {
                using (var p = Theme.Round(ir, Theme.S(8))) using (var b = new SolidBrush(Color.FromArgb(36, 41, 54))) g.FillPath(b, p);
                g.DrawImage(App.Icon, RectangleF.Inflate(ir, -Theme.S(5), -Theme.S(5)));
            }
            else
            {
                using (var p = Theme.Round(ir, Theme.S(8))) using (var b = new SolidBrush(Catalog.CatColor(App.Category))) g.FillPath(b, p);
                using (var f = Theme.F(9.5f, FontStyle.Bold)) Theme.DrawText(g, AppCard.Initials(App.Name), f, Color.White, Rectangle.Round(ir), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using (var f = Theme.FS(10f)) Theme.DrawText(g, App.Name, f, Theme.Text, new Rectangle(Theme.S(56), Theme.S(8), Theme.S(300), Theme.S(20)), TextFormatFlags.EndEllipsis);
            using (var f = Theme.F(8.5f)) Theme.DrawText(g, Status, f, StatusColor, new Rectangle(Theme.S(56), Theme.S(28), Width - Theme.S(300), Theme.S(18)), TextFormatFlags.EndEllipsis);
            // right indicator
            var c = new RectangleF(Width - Theme.S(38), (Height - Theme.S(22)) / 2f, Theme.S(22), Theme.S(22));
            if (State == 1)
            {
                using (var pen = new Pen(Theme.Border, Theme.S(3))) g.DrawEllipse(pen, c);
                using (var pen = new Pen(Theme.Accent, Theme.S(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, c, spin, 100);
                if (Progress >= 0)
                {
                    var pr = new RectangleF(Width - Theme.S(220), (Height - Theme.S(6)) / 2f, Theme.S(160), Theme.S(6));
                    using (var p = Theme.Round(pr, Theme.S(3))) using (var b = new SolidBrush(Theme.Border)) g.FillPath(b, p);
                    var fr = new RectangleF(pr.X, pr.Y, Math.Max(pr.Height, pr.Width * Progress), pr.Height);
                    using (var p = Theme.Round(fr, Theme.S(3))) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                }
            }
            else if (State == 2 || State == 4)
            {
                using (var b = new SolidBrush(State == 2 ? Theme.Green : Theme.Blue)) g.FillEllipse(b, c);
                using (var pen = new Pen(Color.White, Theme.S(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(pen, new[] { new PointF(c.X + c.Width * 0.27f, c.Y + c.Height * 0.52f), new PointF(c.X + c.Width * 0.44f, c.Y + c.Height * 0.68f), new PointF(c.X + c.Width * 0.74f, c.Y + c.Height * 0.34f) });
            }
            else if (State == 3)
            {
                using (var b = new SolidBrush(Theme.Red)) g.FillEllipse(b, c);
                using (var pen = new Pen(Color.White, Theme.S(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float m = c.Width * 0.32f;
                    g.DrawLine(pen, c.X + m, c.Y + m, c.Right - m, c.Bottom - m);
                    g.DrawLine(pen, c.Right - m, c.Y + m, c.X + m, c.Bottom - m);
                }
            }
            else using (var pen = new Pen(Theme.Dim, Theme.S(2)) { DashStyle = DashStyle.Dot }) g.DrawEllipse(pen, c);
        }
    }

    public class RoundPanel : Panel
    {
        public float Radius = 12; public Color Fill = Theme.Card; public Color Stroke = Theme.Border;
        public RoundPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); BackColor = Theme.Bg; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g); g.Clear(BackColor);
            using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.S(Radius)))
            {
                using (var b = new SolidBrush(Fill)) g.FillPath(b, p);
                if (Stroke != Color.Empty) using (var pen = new Pen(Stroke)) g.DrawPath(pen, p);
            }
            base.OnPaint(e);
        }
    }

    public class Lbl : Label
    {
        public Lbl(string t, Font f, Color c) { Text = Theme.Glyph(t); Font = f; ForeColor = c; AutoSize = true; BackColor = Color.Transparent; UseMnemonic = false; }
    }

    public class DFlow : FlowLayoutPanel
    {
        public DFlow() { DoubleBuffered = true; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.DarkScroll(this); }
    }
}
