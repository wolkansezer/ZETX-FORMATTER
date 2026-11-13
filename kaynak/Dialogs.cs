using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZetxFormatter
{
    public class DarkDialog : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        protected Panel Body, Footer;
        public DarkDialog(string title, int w, int h)
        {
            Text = title; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel; ForeColor = Theme.Text; Font = Theme.F(9.5f); ShowInTaskbar = false;
            Size = new Size(Theme.S(w), Theme.S(h)); DoubleBuffered = true; KeyPreview = true;
            Padding = new Padding(1);
            Body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(Theme.S(24), Theme.S(8), Theme.S(24), Theme.S(8)) };
            Footer = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(66), BackColor = Theme.Panel, Padding = new Padding(Theme.S(24), Theme.S(12), Theme.S(24), Theme.S(14)) };
            var top = new Panel { Dock = DockStyle.Top, Height = Theme.S(54), BackColor = Theme.Panel };
            var tl = new Lbl(title, Theme.FS(13f), Theme.Text) { Location = new Point(Theme.S(24), Theme.S(16)), BackColor = Theme.Panel };
            var close = new FlatButton("", 1, "") { Size = new Size(Theme.S(34), Theme.S(34)), Radius = 8 };
            close.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            top.Controls.Add(tl); top.Controls.Add(close);
            top.Resize += (s, e) => close.Location = new Point(top.Width - close.Width - Theme.S(12), Theme.S(10));
            MouseEventHandler drag = (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } };
            top.MouseDown += drag; tl.MouseDown += drag;
            Controls.Add(Body); Controls.Add(Footer); Controls.Add(top);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        protected FlatButton AddFooterButton(string text, int style, string glyph, int width, DialogResult dr)
        {
            var b = new FlatButton(text, style, glyph) { Width = Theme.S(width), Height = Theme.S(40), Dock = DockStyle.Right };
            if (dr != DialogResult.None) b.Click += (s, e) => { DialogResult = dr; Close(); };
            if (Footer.Controls.Count > 0) Footer.Controls.Add(new Panel { Width = Theme.S(10), Dock = DockStyle.Right, BackColor = Theme.Panel });
            Footer.Controls.Add(b);
            return b;
        }

        public static RoundPanel Input(out TextBox tb, int width, string placeholder = null)
        {
            var host = new RoundPanel { Size = new Size(Theme.S(width), Theme.S(40)), Fill = Theme.Card, Radius = 9, BackColor = Theme.Panel };
            var t = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.F(10.5f), Location = new Point(Theme.S(12), Theme.S(11)), Width = host.Width - Theme.S(24) };
            host.Controls.Add(t);
            host.Resize += (s, e) => t.Width = host.Width - Theme.S(24);
            host.Click += (s, e) => t.Focus();
            if (placeholder != null)
            {
                var ph = new Lbl(placeholder, Theme.F(10.5f), Theme.Dim) { BackColor = Theme.Card, Location = new Point(Theme.S(10), Theme.S(10)) };
                ph.Click += (s, e) => t.Focus();
                host.Controls.Add(ph); ph.BringToFront();
                t.TextChanged += (s, e) => ph.Visible = t.Text.Length == 0;
            }
            tb = t; return host;
        }
        public static Lbl Caption(string text) { return new Lbl(text, Theme.FS(9f), Theme.Sub) { BackColor = Theme.Panel }; }
    }

    public class InputDialog : DarkDialog
    {
        public string Value { get { return tb.Text.Trim(); } }
        TextBox tb;
        public InputDialog(string title, string label, string initial, string okText = "Kaydet") : base(title, 460, 230)
        {
            var cap = Caption(label); cap.Location = new Point(Theme.S(24), Theme.S(8));
            var inp = Input(out tb, 410); inp.Location = new Point(Theme.S(24), Theme.S(34));
            tb.Text = initial ?? ""; tb.MaxLength = 60;
            Body.Controls.Add(cap); Body.Controls.Add(inp);
            AddFooterButton(okText, 0, "", 140, DialogResult.None).Click += (s, e) => { if (Value.Length > 0) { DialogResult = DialogResult.OK; Close(); } else tb.Focus(); };
            AddFooterButton("İptal", 1, null, 100, DialogResult.Cancel);
            tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && Value.Length > 0) { e.SuppressKeyPress = true; DialogResult = DialogResult.OK; Close(); } };
            Shown += (s, e) => { tb.Focus(); tb.SelectAll(); };
        }
    }

    public class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = Theme.Card; ForeColor = Theme.Text; Font = Theme.F(10f); ItemHeight = Theme.S(28);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(sel ? Theme.Mix(Theme.Card, Theme.Accent, 0.35f) : Theme.Card)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + Theme.S(8), e.Bounds.Y, e.Bounds.Width - Theme.S(8), e.Bounds.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// Add / edit a program that is not in the list
    public class CustomAppDialog : DarkDialog
    {
        public AppItem Result;
        TextBox nameTb, srcTb, argsTb, descTb; Pill urlPill, filePill; FlatButton browse; DarkCombo silent; Lbl srcCap, hint; RoundPanel argsHost, srcHost;
        string kind = "url";

        public CustomAppDialog(AppItem edit) : base(edit == null ? "Listede Olmayan Program Ekle" : "Programı Düzenle", 600, 640)
        {
            int x = Theme.S(24), y = Theme.S(4), w = 550;
            Func<Control, int, Control> place = (c, gap) => { c.Location = new Point(x, y); Body.Controls.Add(c); y += c.Height + Theme.S(gap); return c; };

            place(Caption("Program adı"), 6);
            place(Input(out nameTb, w, "Örn: Photoshop, Valorant, Sürücü Paketim..."), 16);
            nameTb.MaxLength = 50;

            place(Caption("Kaynak"), 6);
            var pillRow = new Panel { Size = new Size(Theme.S(w), Theme.S(34)), BackColor = Theme.Panel };
            urlPill = new Pill("İndirme linki") { Location = new Point(0, 0) }; urlPill.Fit();
            filePill = new Pill("Bilgisayardaki dosya (.exe / .msi)") { Location = new Point(urlPill.Width + Theme.S(8), 0) }; filePill.Fit();
            urlPill.Click += (s, e) => SetKind("url"); filePill.Click += (s, e) => SetKind("file");
            pillRow.Controls.Add(urlPill); pillRow.Controls.Add(filePill);
            place(pillRow, 14);

            srcCap = (Lbl)place(Caption("İndirme linki"), 6);
            srcHost = Input(out srcTb, w - 130, "https://...");
            srcHost.Location = new Point(x, y); Body.Controls.Add(srcHost);
            browse = new FlatButton("Gözat...", 1, "") { Size = new Size(Theme.S(118), Theme.S(40)), Location = new Point(x + Theme.S(w - 118), y) };
            browse.Click += (s, e) => Browse();
            Body.Controls.Add(browse);
            y += Theme.S(40) + Theme.S(6);
            hint = (Lbl)place(new Lbl("", Theme.F(8.5f), Theme.Dim) { BackColor = Theme.Panel, AutoSize = false, Size = new Size(Theme.S(w), Theme.S(34)) }, 10);

            place(Caption("Kurulum şekli"), 6);
            silent = new DarkCombo { Width = Theme.S(w) };
            silent.Items.AddRange(Custom.SilentNames);
            silent.SelectedIndex = 0;
            place(silent, 10);
            argsHost = Input(out argsTb, w, "Örn: /S  veya  /silent /norestart");
            place(argsHost, 14);
            silent.SelectedIndexChanged += (s, e) => argsHost.Visible = Custom.SilentModes[silent.SelectedIndex] == "custom";
            argsHost.Visible = false;

            place(Caption("Açıklama (isteğe bağlı)"), 6);
            place(Input(out descTb, w, "Kısa not..."), 10);
            descTb.MaxLength = 70;

            var info = new Lbl("Otomatik algılama; MSI, Inno Setup ve NSIS kurulumlarını sessiz kurar. Algılanamazsa normal kurulum penceresi açılır, kurulumu sen tamamlarsın.", Theme.F(8.5f), Theme.Sub) { BackColor = Theme.Panel, AutoSize = false, Size = new Size(Theme.S(w), Theme.S(40)) };
            place(info, 0);

            AddFooterButton(edit == null ? "Programı Ekle" : "Kaydet", 0, "", 170, DialogResult.None).Click += (s, e) => Save(edit);
            AddFooterButton("İptal", 1, null, 100, DialogResult.Cancel);

            if (edit != null)
            {
                nameTb.Text = edit.Name; srcTb.Text = edit.CustomSrc; argsTb.Text = edit.CustomArgs;
                descTb.Text = edit.Desc == Custom.DefaultDesc(edit) ? "" : edit.Desc;
                int si = Array.IndexOf(Custom.SilentModes, edit.SilentMode); silent.SelectedIndex = si < 0 ? 0 : si;
                SetKind(edit.CustomKind);
            }
            else SetKind("url");
            Shown += (s, e) => nameTb.Focus();
        }

        void SetKind(string k)
        {
            kind = k;
            urlPill.Selected = k == "url"; filePill.Selected = k == "file"; urlPill.Invalidate(); filePill.Invalidate();
            srcCap.Text = k == "url" ? "İndirme linki (doğrudan .exe / .msi linki)" : "Kurulum dosyası";
            browse.Visible = k == "file";
            srcHost.Width = k == "file" ? Theme.S(550 - 128) : Theme.S(550);
            hint.Text = k == "url"
                ? "İpucu: Linke tıklayınca direkt indirme başlamalı. Link profil ile birlikte taşınır, format sonrası da çalışır."
                : "Not: Format sonrası da kullanmak için dosyayı USB bellekte ya da C: dışındaki bir diskte tut.";
        }

        void Browse()
        {
            using (var d = new OpenFileDialog { Filter = "Kurulum dosyaları (*.exe;*.msi)|*.exe;*.msi|Tüm dosyalar|*.*", Title = "Kurulum dosyasını seç" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    srcTb.Text = d.FileName;
                    if (nameTb.Text.Trim().Length == 0)
                    {
                        string n = null;
                        try { var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(d.FileName); n = vi.ProductName; } catch { }
                        if (string.IsNullOrWhiteSpace(n)) n = Path.GetFileNameWithoutExtension(d.FileName);
                        nameTb.Text = n.Trim();
                    }
                }
        }

        void Save(AppItem edit)
        {
            string name = nameTb.Text.Trim(), src = srcTb.Text.Trim();
            if (name.Length == 0) { Warn("Program adını yaz."); nameTb.Focus(); return; }
            if (kind == "url")
            {
                Uri u;
                if (!Uri.TryCreate(src, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) { Warn("Geçerli bir indirme linki yaz (https:// ile başlamalı)."); srcTb.Focus(); return; }
            }
            else if (!File.Exists(src)) { Warn("Seçilen dosya bulunamadı."); return; }
            string mode = Custom.SilentModes[silent.SelectedIndex];
            if (mode == "custom" && argsTb.Text.Trim().Length == 0) { Warn("Özel parametreyi yaz ya da başka bir kurulum şekli seç."); argsTb.Focus(); return; }
            var a = edit ?? new AppItem { Id = Custom.NewId(), Source = "custom", Category = Custom.Category };
            a.Name = name; a.CustomKind = kind; a.CustomSrc = src; a.SilentMode = mode; a.CustomArgs = argsTb.Text.Trim();
            a.Desc = descTb.Text.Trim().Length > 0 ? descTb.Text.Trim() : Custom.DefaultDesc(a);
            a.Domain = kind == "url" ? Custom.HostOf(src) : "";
            a.Icon = null;
            Result = a; DialogResult = DialogResult.OK; Close();
        }
        void Warn(string m) { MessageBox.Show(this, m, "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    /// Pick programs for a profile
    public class PickerDialog : DarkDialog
    {
        public HashSet<string> Picked;
        DFlow flow; TextBox search; readonly List<AppCard> cards = new List<AppCard>(); Lbl count;
        static readonly CultureInfo TR = new CultureInfo("tr-TR");

        public PickerDialog(string profileName, IEnumerable<string> current) : base("Programları Seç — " + profileName, 940, 680)
        {
            Picked = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            var top = new Panel { Dock = DockStyle.Top, Height = Theme.S(52), BackColor = Theme.Panel };
            var inp = Input(out search, 360, "Program ara..."); inp.Location = new Point(0, Theme.S(2));
            count = new Lbl("", Theme.FS(10f), Theme.Accent) { BackColor = Theme.Panel };
            top.Controls.Add(inp); top.Controls.Add(count);
            top.Resize += (s, e) => count.Location = new Point(top.Width - count.Width, Theme.S(12));
            flow = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Panel };
            Body.Controls.Add(flow); Body.Controls.Add(top);
            flow.Resize += (s, e) =>
            {
                int avail = flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Theme.S(4);
                int per = Math.Max(1, avail / Theme.S(262));
                int w = avail / per - Theme.S(12) - 1;
                foreach (var c in cards) c.Width = w;
            };
            foreach (var cat in Catalog.Categories)
                foreach (var a in Catalog.All.Where(x => x.Category == cat))
                {
                    var c = new AppCard(a) { Width = Theme.S(268), GetChecked = x => Picked.Contains(x.Id), Toggle = x => { if (!Picked.Remove(x.Id)) Picked.Add(x.Id); UpdateCount(); }, BackColor = Theme.Panel };
                    cards.Add(c);
                }
            search.TextChanged += (s, e) => Filter();
            AddFooterButton("Tamam", 0, "", 140, DialogResult.OK);
            AddFooterButton("İptal", 1, null, 100, DialogResult.Cancel);
            var onlySel = new FlatButton("Sadece seçilenler", 1, "") { Width = Theme.S(180), Height = Theme.S(40), Dock = DockStyle.Left };
            bool only = false;
            onlySel.Click += (s, e) => { only = !only; onlySel.Style = only ? 0 : 1; onlySel.Invalidate(); Filter(only); };
            Footer.Controls.Add(onlySel);
            Filter(); UpdateCount();
            Shown += (s, e) => search.Focus();
        }
        void UpdateCount() { count.Text = Picked.Count + " program seçili"; count.Location = new Point(count.Parent.Width - count.Width, Theme.S(12)); }
        bool onlyMode;
        void Filter(bool? only = null)
        {
            if (only.HasValue) onlyMode = only.Value;
            string q = search.Text.Trim().ToLower(TR);
            flow.SuspendLayout(); flow.Controls.Clear();
            flow.Controls.AddRange(cards.Where(c => (q.Length == 0 || c.App.Name.ToLower(TR).Contains(q) || c.App.Category.ToLower(TR).Contains(q)) && (!onlyMode || Picked.Contains(c.App.Id))).Cast<Control>().ToArray());
            flow.ResumeLayout();
        }
    }

    public class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuItemSelected { get { return Theme.Mix(Theme.Card, Theme.Accent, 0.3f); } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }
}
