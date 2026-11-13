using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ZetxFormatter
{
    public partial class MainForm : Form
    {
        public const string Version = "1.2.0";
        const string Discord = "https://discord.gg/nvqc84AHZf";
        const string Mail = "volkan.sezer@outlook.com.tr";
        static readonly CultureInfo TR = new CultureInfo("tr-TR");

        Panel titleBar, side, content;
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly List<NavButton> navs = new List<NavButton>();
        NavButton navInstall;

        // programs page
        DFlow cardFlow; readonly List<AppCard> cards = new List<AppCard>(); readonly List<Control> headers = new List<Control>();
        readonly List<Pill> pills = new List<Pill>(); string filter = "Tümü"; TextBox search;
        Label selLabel, wingetLabel; FlatButton startBtn, wingetFix; Toggle skipToggle; RoundPanel wingetBanner; AddCard addCard; Label selSubLbl;

        // install page
        DFlow rowFlow; RichTextBox log; ProgressX overall; Label ovTitle, ovPercent, ovSub; FlatButton cancelBtn, backBtn; Toggle rebootToggle;
        readonly List<InstallRow> rows = new List<InstallRow>();
        volatile bool running, cancel; DateTime started; System.Windows.Forms.Timer ticker;

        // updates page
        RichTextBox updLog; FlatButton scanBtn, updBtn;
        // system page
        readonly List<KeyValuePair<Tweak, Toggle>> tweakToggles = new List<KeyValuePair<Tweak, Toggle>>();
        Label gpuValue;

        public MainForm()
        {
            Text = "ZETX FORMATTER";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg; ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Theme.S(1260), wa.Width - 40), Math.Min(Theme.S(800), wa.Height - 40));
            MinimumSize = new Size(Theme.S(980), Theme.S(640));
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            Controls.Add(content);
            BuildSide();
            BuildTitle();

            Custom.LoadStore();
            BuildPrograms();
            BuildProfiles();
            BuildInstall();
            BuildUpdates();
            BuildSystem();
            BuildAbout();
            ShowPage("Programlar");

            ticker = new System.Windows.Forms.Timer { Interval = 90 };
            ticker.Tick += (s, e) => OnTick();
            ticker.Start();

            Shown += (s, e) => { StartupScan(); var dp = Theme.Shot ? Environment.GetEnvironmentVariable("ZF_PAGE") : null; if (!string.IsNullOrEmpty(dp)) ShowPage(dp);
                if (!Theme.Shot) return;
                var sel = Environment.GetEnvironmentVariable("ZF_SEL"); if (sel == "1") { ApplyPreset(Catalog.Presets[1]); filter = "Popüler"; RefreshCards(); }
                var dg = Environment.GetEnvironmentVariable("ZF_DLG");
                if (dg == "custom") BeginInvoke(new Action(() => AddCustomApp(null)));
                if (dg == "picker") BeginInvoke(new Action(() => PickForProfile()));
                if (dg == "install") { var tt = new System.Windows.Forms.Timer { Interval = 5000 }; tt.Tick += (a1, b1) => { tt.Stop(); RefreshProfiles("Oyun PC"); StartInstall(curProfile.Ids.Select(Catalog.Find).Where(x => x != null).ToList(), curProfile.Name); }; tt.Start(); }
            };
            FormClosing += (s, e) =>
            {
                if (running && MessageBox.Show(this, "Kurulum devam ediyor. Çıkmak istediğine emin misin?", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
                cancel = true; Engine.KillCurrent(); SaveLast();
            };
        }

        // ================= Window chrome =================
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        [DllImport("user32.dll")] static extern bool FlashWindow(IntPtr h, bool invert);

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.Style |= 0x00020000; return cp; } // drop shadow + minimize box
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); int d = 1; DwmSetWindowAttribute(Handle, 20, ref d, 4); } catch { }
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                var p = PointToClient(new Point(m.LParam.ToInt32() & 0xffff, (short)(m.LParam.ToInt32() >> 16)));
                int b = Theme.S(7);
                bool l = p.X < b, r = p.X > Width - b, t = p.Y < b, bo = p.Y > Height - b;
                if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
                else if (bo && l) m.Result = (IntPtr)16; else if (bo && r) m.Result = (IntPtr)17;
                else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
                else if (t) m.Result = (IntPtr)12; else if (bo) m.Result = (IntPtr)15;
                return;
            }
            base.WndProc(ref m);
        }
        void ToggleMax()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else { MaximizedBounds = Screen.FromHandle(Handle).WorkingArea; WindowState = FormWindowState.Maximized; }
        }

        class WinBtn : XControl
        {
            public string Glyph; public bool Close;
            public WinBtn(string g, bool close = false) { Glyph = g; Close = close; Size = new Size(Theme.S(46), Theme.S(44)); }
            protected override void OnPaint(PaintEventArgs e)
            {
                if (hover) using (var b = new SolidBrush(Close ? Color.FromArgb(232, 17, 35) : Theme.Card)) e.Graphics.FillRectangle(b, ClientRectangle);
                using (var f = Theme.Icon(8f)) Theme.DrawText(e.Graphics, Glyph, f, hover && Close ? Color.White : Theme.Sub, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        void BuildTitle()
        {
            titleBar = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = Theme.S(44), BackColor = Theme.Side };
            Controls.Add(titleBar);
            var close = new WinBtn("", true); var max = new WinBtn(""); var min = new WinBtn("");
            close.Click += (s, e) => Close();
            max.Click += (s, e) => { ToggleMax(); max.Glyph = WindowState == FormWindowState.Maximized ? "" : ""; max.Invalidate(); };
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            foreach (var b in new Control[] { min, max, close }) { b.Dock = DockStyle.Right; titleBar.Controls.Add(b); }
            titleBar.Paint += (s, e) =>
            {
                var g = e.Graphics; Theme.Hq(g);
                Theme.DrawLogo(g, new RectangleF(Theme.S(16), Theme.S(10), Theme.S(24), Theme.S(24)));
                using (var f = Theme.F(10.5f, FontStyle.Bold)) Theme.DrawText(g, "ZETX", f, Theme.Text, new Rectangle(Theme.S(48), 0, Theme.S(60), titleBar.Height), TextFormatFlags.VerticalCenter);
                using (var f = Theme.F(10.5f, FontStyle.Bold)) Theme.DrawText(g, "FORMATTER", f, Theme.Accent, new Rectangle(Theme.S(95), 0, Theme.S(110), titleBar.Height), TextFormatFlags.VerticalCenter);
                using (var f = Theme.F(8.5f)) Theme.DrawText(g, "v" + Version + (Program.IsAdmin ? "   •   Yönetici modu" : "   •   Yönetici değil"), f, Theme.Dim, new Rectangle(Theme.S(200), 0, Theme.S(260), titleBar.Height), TextFormatFlags.VerticalCenter);
            };
            MouseEventHandler drag = (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (e.Clicks == 2) { ToggleMax(); return; }
                ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0);
            };
            titleBar.MouseDown += drag;
        }

        void BuildSide()
        {
            side = new DoubleBufferedPanel { Dock = DockStyle.Left, Width = Theme.S(232), BackColor = Theme.Side, Padding = new Padding(0, Theme.S(14), 0, 0) };
            Controls.Add(side);
            side.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, side.Width - 1, 0, side.Width - 1, side.Height);
                var g = e.Graphics; Theme.Hq(g);
                var r = new RectangleF(Theme.S(16), side.Height - Theme.S(92), side.Width - Theme.S(32), Theme.S(76));
                using (var p = Theme.Round(r, Theme.S(10))) using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                using (var f = Theme.FS(9f)) Theme.DrawText(g, "Format sonrası tek tık kurulum", f, Theme.Text, new Rectangle((int)r.X + Theme.S(12), (int)r.Y + Theme.S(10), (int)r.Width - Theme.S(20), Theme.S(18)), TextFormatFlags.EndEllipsis);
                using (var f = Theme.F(8f)) Theme.DrawText(g, Catalog.All.Count + " program  •  " + Catalog.Presets.Length + " hazır paket", f, Theme.Sub, new Rectangle((int)r.X + Theme.S(12), (int)r.Y + Theme.S(30), (int)r.Width - Theme.S(20), Theme.S(18)), TextFormatFlags.EndEllipsis);
                using (var f = Theme.F(8f)) Theme.DrawText(g, "by Volkan Sezer", f, Theme.Accent, new Rectangle((int)r.X + Theme.S(12), (int)r.Y + Theme.S(50), (int)r.Width - Theme.S(20), Theme.S(18)), TextFormatFlags.EndEllipsis);
            };
            side.Resize += (s, e) => side.Invalidate();
            var items = new[] {
                new[] { "Programlar", "" }, new[] { "Profillerim", "" }, new[] { "Kurulum", "" }, new[] { "Güncellemeler", "" },
                new[] { "Sistem & Ayarlar", "" }, new[] { "Hakkında", "" } };
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var nb = new NavButton(items[i][0], items[i][1]) { Dock = DockStyle.Top };
                string key = items[i][0];
                nb.Click += (s, e) => ShowPage(key);
                side.Controls.Add(nb); navs.Add(nb);
                if (key == "Kurulum") navInstall = nb;
            }
            var menuLbl = new Lbl("   MENÜ", Theme.FS(8f), Theme.Dim) { Dock = DockStyle.Top, AutoSize = false, Height = Theme.S(28), Padding = new Padding(Theme.S(14), 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
            side.Controls.Add(menuLbl);
        }

        void ShowPage(string key)
        {
            foreach (var kv in pages) kv.Value.Visible = kv.Key == key;
            foreach (var n in navs) { n.Selected = n.Text == key; n.Invalidate(); }
            if (key == "Sistem & Ayarlar") RefreshTweaks();
            if (key == "Profillerim") RefreshProfiles();
        }

        Panel NewPage(string key)
        {
            var p = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(Theme.S(26), Theme.S(18), Theme.S(26), Theme.S(12)), Visible = false };
            content.Controls.Add(p); pages[key] = p; return p;
        }

        Panel Header(string title, string sub)
        {
            var h = new Panel { Dock = DockStyle.Top, Height = Theme.S(66), BackColor = Theme.Bg };
            h.Controls.Add(new Lbl(title, Theme.F(19f, FontStyle.Bold), Theme.Text) { Location = new Point(0, 0) });
            h.Controls.Add(new Lbl(sub, Theme.F(9.5f), Theme.Sub) { Location = new Point(Theme.S(2), Theme.S(38)) });
            return h;
        }

        RichTextBox NewLog()
        {
            var rt = new RichTextBox { BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(14, 16, 22), ForeColor = Theme.Sub, Font = new Font("Consolas", 9f), ReadOnly = true, Dock = DockStyle.Fill, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical };
            rt.HandleCreated += (s, e) => Theme.DarkScroll(rt);
            return rt;
        }
        RoundPanel Wrap(Control c, int pad = 12)
        {
            var rp = new RoundPanel { Fill = Color.FromArgb(14, 16, 22), Padding = new Padding(Theme.S(pad)) };
            rp.Controls.Add(c); return rp;
        }
        void Append(RichTextBox rt, string text, Color c)
        {
            if (rt.IsDisposed) return;
            Action a = () =>
            {
                rt.SelectionStart = rt.TextLength; rt.SelectionLength = 0; rt.SelectionColor = c;
                rt.AppendText(text + Environment.NewLine);
                if (rt.TextLength > 400000) { rt.Select(0, 100000); rt.SelectedText = ""; }
                rt.SelectionStart = rt.TextLength; rt.ScrollToCaret();
            };
            if (rt.InvokeRequired) rt.BeginInvoke(a); else a();
        }
        void UI(Action a) { if (IsDisposed) return; if (InvokeRequired) { try { BeginInvoke(a); } catch { } } else a(); }

        // ================= Programs page =================
        void BuildPrograms()
        {
            var page = NewPage("Programlar");

            cardFlow = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(6), 0, Theme.S(10)) };
            page.Controls.Add(cardFlow);

            // bottom bar
            var bottom = new DoubleBufferedPanel { Dock = DockStyle.Bottom, Height = Theme.S(74), BackColor = Theme.Bg };
            page.Controls.Add(bottom);
            var bar = new RoundPanel { Dock = DockStyle.Fill, Fill = Theme.Panel, Radius = 12 };
            bottom.Padding = new Padding(0, Theme.S(10), 0, 0);
            bottom.Controls.Add(bar);
            selLabel = new Lbl("0 program seçildi", Theme.FS(11f), Theme.Text) { BackColor = Theme.Panel };
            var selSub = new Lbl("Kartlara tıklayarak seç, sonra kurulumu başlat", Theme.F(8.5f), Theme.Sub) { BackColor = Theme.Panel };
            selSubLbl = selSub;
            startBtn = new FlatButton("KURULUMU BAŞLAT", 0, "") { Width = Theme.S(210), Height = Theme.S(42), Font = Theme.F(10.5f, FontStyle.Bold) };
            startBtn.Click += (s, e) => StartInstall();
            var saveBtn = new FlatButton("Profil Olarak Kaydet", 1, "") { Width = Theme.S(196), Height = Theme.S(42) };
            var loadBtn = new FlatButton("Program Ekle", 1, "") { Width = Theme.S(150), Height = Theme.S(42) };
            var clearBtn = new FlatButton("Temizle", 1, "") { Width = Theme.S(110), Height = Theme.S(42) };
            saveBtn.Click += (s, e) => SaveSelectionAsProfile(); loadBtn.Click += (s, e) => AddCustomApp(null);
            clearBtn.Click += (s, e) => { foreach (var a in Catalog.All) a.Selected = false; RefreshCards(); };
            skipToggle = new Toggle { On = true };
            var skipLbl = new Lbl("Kurulu olanları atla", Theme.F(9f), Theme.Sub) { BackColor = Theme.Panel };
            foreach (Control c in new Control[] { selLabel, selSub, startBtn, saveBtn, loadBtn, clearBtn, skipToggle, skipLbl }) bar.Controls.Add(c);
            bar.Resize += (s, e) =>
            {
                int cy = bar.Height / 2;
                selLabel.Location = new Point(Theme.S(18), cy - Theme.S(20));
                selSub.Location = new Point(Theme.S(19), cy + Theme.S(2));
                startBtn.Location = new Point(bar.Width - startBtn.Width - Theme.S(12), cy - startBtn.Height / 2);
                loadBtn.Location = new Point(startBtn.Left - loadBtn.Width - Theme.S(8), startBtn.Top);
                saveBtn.Location = new Point(loadBtn.Left - saveBtn.Width - Theme.S(8), startBtn.Top);
                clearBtn.Location = new Point(saveBtn.Left - clearBtn.Width - Theme.S(8), startBtn.Top);
                skipLbl.Location = new Point(clearBtn.Left - skipLbl.Width - Theme.S(16), cy - skipLbl.Height / 2);
                skipToggle.Location = new Point(skipLbl.Left - skipToggle.Width - Theme.S(8), cy - skipToggle.Height / 2);
                bool small = skipToggle.Left < Theme.S(250);
                skipToggle.Visible = skipLbl.Visible = !small;
                selSub.Visible = clearBtn.Left > Theme.S(330);
                selLabel.Visible = clearBtn.Left > Theme.S(200);
            };

            // category pills
            var pillFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(4), 0, Theme.S(6)) };
            page.Controls.Add(pillFlow);
            var cats = new List<string> { "Tümü", "Seçilenler", "Popüler" }; cats.AddRange(Catalog.Categories);
            foreach (var c in cats)
            {
                var p = new Pill(c) { Margin = new Padding(0, 0, Theme.S(6), Theme.S(6)) };
                string key = c;
                p.Click += (s, e) => { filter = key; RefreshCards(); };
                pills.Add(p); pillFlow.Controls.Add(p);
            }

            // presets row
            var presetRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Theme.S(52), WrapContents = false, BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(2), 0, 0) };
            page.Controls.Add(presetRow);
            presetRow.Controls.Add(new Lbl("Hazır paketler:", Theme.FS(9.5f), Theme.Sub) { Margin = new Padding(0, Theme.S(12), Theme.S(8), 0) });
            foreach (var pr in Catalog.Presets)
            {
                var b = new FlatButton(pr.Name, 1, pr.Glyph) { Height = Theme.S(38), Font = Theme.FS(9.5f) };
                b.Width = TextRenderer.MeasureText(pr.Name, b.Font).Width + Theme.S(54);
                b.Margin = new Padding(0, 0, Theme.S(8), 0);
                var preset = pr;
                b.Click += (s, e) => ApplyPreset(preset);
                presetRow.Controls.Add(b);
            }

            // winget banner
            wingetBanner = new RoundPanel { Dock = DockStyle.Top, Height = Theme.S(46), Fill = Theme.Panel, Radius = 10 };
            var bannerWrap = new Panel { Dock = DockStyle.Top, Height = Theme.S(56), BackColor = Theme.Bg, Padding = new Padding(0, 0, 0, Theme.S(10)) };
            bannerWrap.Controls.Add(wingetBanner);
            page.Controls.Add(bannerWrap);
            wingetLabel = new Lbl("Paket yöneticisi (winget) kontrol ediliyor...", Theme.F(9f), Theme.Sub) { BackColor = Theme.Panel, Location = new Point(Theme.S(40), Theme.S(14)) };
            wingetBanner.Controls.Add(wingetLabel);
            var dot = new Panel { Size = new Size(Theme.S(10), Theme.S(10)), Location = new Point(Theme.S(18), Theme.S(18)), BackColor = Theme.Yellow, Name = "dot" };
            wingetBanner.Controls.Add(dot);
            wingetFix = new FlatButton("winget'i Kur / Onar", 0, "") { Width = Theme.S(190), Height = Theme.S(32), Visible = false, Font = Theme.FS(9f) };
            wingetFix.Click += (s, e) => FixWinget();
            wingetBanner.Controls.Add(wingetFix);
            wingetLabel.AutoSize = false; wingetLabel.AutoEllipsis = true; wingetLabel.Height = Theme.S(20);
            wingetBanner.Resize += (s, e) => LayoutBanner();

            // header + search
            var head = Header("Programlar", "Format sonrası ihtiyacın olan programları seç, ZETX FORMATTER hepsini sessizce kursun.");
            page.Controls.Add(head);
            var sbox = new RoundPanel { Size = new Size(Theme.S(300), Theme.S(40)), Fill = Theme.Card, Radius = 10 };
            search = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.F(10.5f) };
            var sIcon = new Lbl("", Theme.Icon(11f), Theme.Dim) { BackColor = Theme.Card, Location = new Point(Theme.S(12), Theme.S(11)) };
            var ph = new Lbl("Program ara...", Theme.F(10.5f), Theme.Dim) { BackColor = Theme.Card, Location = new Point(Theme.S(38), Theme.S(10)) };
            ph.Click += (s, e) => search.Focus();
            search.Location = new Point(Theme.S(40), Theme.S(11)); search.Width = sbox.Width - Theme.S(52);
            search.TextChanged += (s, e) => { ph.Visible = search.Text.Length == 0; if (filter == "Seçilenler") filter = "Tümü"; RefreshCards(); };
            sbox.Controls.Add(search); sbox.Controls.Add(sIcon); sbox.Controls.Add(ph); ph.BringToFront();
            head.Controls.Add(sbox);
            head.Resize += (s, e) => sbox.Location = new Point(head.Width - sbox.Width, Theme.S(8));

            // cards
            foreach (var a in Catalog.All) MakeCard(a);
            addCard = new AddCard("Listede olmayan program ekle", "Kurulum dosyası (.exe/.msi) veya indirme linki");
            addCard.Click += (s, e) => AddCustomApp(null);
            foreach (var cat in Catalog.Categories)
            {
                var hdr = new CatHeader(cat) { Tag = cat };
                headers.Add(hdr);
            }
            cardFlow.Resize += (s, e) => LayoutHeaders();
            LoadLast();
            RefreshCards();
        }

        class CatHeader : Control
        {
            public CatHeader(string t) { Text = t; Height = Theme.S(40); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); BackColor = Theme.Bg; Margin = new Padding(Theme.S(6), Theme.S(10), Theme.S(6), 0); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Hq(g);
                var c = Catalog.CatColor(Text);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, 0, Height / 2 - Theme.S(4), Theme.S(8), Theme.S(8));
                using (var f = Theme.FS(11f))
                {
                    var sz = TextRenderer.MeasureText(Text, f);
                    Theme.DrawText(g, Text, f, Theme.Text, new Rectangle(Theme.S(16), 0, sz.Width + 4, Height), TextFormatFlags.VerticalCenter);
                    int n = Catalog.All.Count(a => a.Category == Text);
                    if (Text == Custom.Category && n == 0) { using (var f2 = Theme.F(8.5f)) Theme.DrawText(g, "kendi programlarını buraya ekle", f2, Theme.Dim, new Rectangle(Theme.S(22) + sz.Width, 0, Theme.S(220), Height), TextFormatFlags.VerticalCenter); using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Theme.S(250) + sz.Width, Height / 2, Width, Height / 2); return; }
                    using (var f2 = Theme.F(8.5f)) Theme.DrawText(g, n + " program", f2, Theme.Dim, new Rectangle(Theme.S(22) + sz.Width, 0, Theme.S(100), Height), TextFormatFlags.VerticalCenter);
                    using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Theme.S(110) + sz.Width, Height / 2, Width, Height / 2);
                }
            }
        }

        void LayoutHeaders()
        {
            int avail = cardFlow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Theme.S(6);
            if (cardFlow.VerticalScroll.Visible && !Theme.Shot) avail = cardFlow.ClientSize.Width - Theme.S(6);
            foreach (var h in headers) h.Width = Math.Max(Theme.S(200), avail - Theme.S(12));
            int cw = Theme.S(250) + Theme.S(12);
            int perRow = Math.Max(1, avail / cw);
            int width = avail / perRow - Theme.S(12) - 1;
            width = Math.Max(Theme.S(230), Math.Min(Theme.S(380), width));
            foreach (var c in cards) if (c.Width != width) c.Width = width;
            if (addCard != null) addCard.Width = width;
        }

        void RefreshCards()
        {
            string q = (search != null ? search.Text : "").Trim().ToLower(TR);
            Func<AppItem, bool> match = a =>
            {
                if (q.Length > 0 && !(a.Name.ToLower(TR).Contains(q) || a.Desc.ToLower(TR).Contains(q) || a.Category.ToLower(TR).Contains(q))) return false;
                if (filter == "Tümü") return true;
                if (filter == "Seçilenler") return a.Selected;
                if (filter == "Popüler") return a.Popular;
                return a.Category == filter;
            };
            cardFlow.SuspendLayout();
            cardFlow.Controls.Clear();
            bool grouped = filter == "Tümü" || filter == "Popüler" || filter == "Seçilenler";
            var list = new List<Control>();
            foreach (var cat in Catalog.Categories)
            {
                var inCat = cards.Where(c => c.App.Category == cat && match(c.App)).ToList();
                bool showAdd = cat == Custom.Category && q.Length == 0 && (filter == "Tümü" || filter == Custom.Category);
                if (inCat.Count == 0 && !showAdd) continue;
                if (grouped) list.Add(headers.First(h => (string)h.Tag == cat));
                list.AddRange(inCat.Cast<Control>());
                if (showAdd) list.Add(addCard);
            }
            if (list.Count == 0)
            {
                var empty = new Lbl(filter == "Seçilenler" ? "Henüz program seçmedin." : "Aramana uygun program bulunamadı.", Theme.F(11f), Theme.Dim) { Margin = new Padding(Theme.S(8), Theme.S(30), 0, 0) };
                list.Add(empty);
            }
            cardFlow.Controls.AddRange(list.ToArray());
            LayoutHeaders();
            cardFlow.ResumeLayout();
            cardFlow.AutoScrollPosition = new Point(0, 0);
            foreach (var p in pills)
            {
                p.Selected = p.Text == filter;
                if (p.Text == "Seçilenler") p.Count = Catalog.All.Count(a => a.Selected);
                else if (p.Text == "Tümü") p.Count = Catalog.All.Count;
                else if (p.Text == "Popüler") p.Count = -1;
                else p.Count = -1;
                p.Fit(); p.Invalidate();
            }
            UpdateSelCount();
        }

        void UpdateSelCount()
        {
            int n = Catalog.All.Count(a => a.Selected);
            int skip = skipToggle != null && skipToggle.On ? Catalog.All.Count(a => a.Selected && a.Installed) : 0;
            selLabel.Text = n == 0 ? "Program seçilmedi" : n + " program seçildi";
            if (selSubLbl != null) selSubLbl.Text = skip > 0 ? skip + " tanesi zaten kurulu, atlanacak" : "Kartlara tıklayarak seç, sonra kurulumu başlat";
            startBtn.Enabled = n > 0 && !running;
            var sp = pills.FirstOrDefault(p => p.Text == "Seçilenler");
            if (sp != null) { sp.Count = n; sp.Fit(); sp.Invalidate(); }
        }

        void ApplyPreset(Preset p)
        {
            foreach (var id in p.Ids) { var a = Catalog.Find(id); if (a != null) a.Selected = true; }
            UpdateSelCount();
            filter = "Seçilenler"; if (search.Text.Length > 0) search.Text = "";
            RefreshCards();
        }

        string LastFile { get { return Path.Combine(Engine.DataDir, "son_secim.zetx"); } }
        void SaveLast()
        {
            try { Directory.CreateDirectory(Engine.DataDir); Engine.SaveProfile(LastFile, Catalog.All.Where(a => a.Selected).Select(a => a.Id)); } catch { }
        }
        void LoadLast()
        {
            try { if (File.Exists(LastFile)) foreach (var id in Engine.LoadProfile(LastFile)) { var a = Catalog.Find(id); if (a != null) a.Selected = true; } } catch { }
        }

        void LayoutBanner()
        {
            wingetFix.Location = new Point(wingetBanner.Width - wingetFix.Width - Theme.S(8), Theme.S(7));
            wingetLabel.Width = (wingetFix.Visible ? wingetFix.Left : wingetBanner.Width) - wingetLabel.Left - Theme.S(12);
        }
        void SetWingetBanner(int state, string text)
        {
            UI(() =>
            {
                wingetLabel.Text = text;
                var dot = wingetBanner.Controls["dot"];
                dot.BackColor = state == 0 ? Theme.Yellow : state == 1 ? Theme.Green : Theme.Red;
                wingetFix.Visible = state == 2;
                wingetFix.Enabled = true;
                LayoutBanner();
            });
        }

        void StartupScan()
        {
            foreach (var c in cards) LoadCardIcon(c);
            Engine.Detecting = true;
            new Thread(() =>
            {
                bool wg = false;
                try { wg = Engine.DetectWinget(); } catch { }
                if (wg)
                {
                    SetWingetBanner(0, "winget hazır (" + Engine.WingetVersion + ")  •  Kurulu programlar taranıyor...");
                    var set = Engine.GetInstalled(null);
                    foreach (var a in Catalog.All) a.Installed = a.Source == "office" ? Office.IsInstalled(Office.KeyOf(a)) : a.Source != "custom" && (set.Contains(a.Id) || a.AltIds.Any(set.Contains));
                    int n = Catalog.All.Count(a => a.Installed);
                    SetWingetBanner(1, "winget hazır (" + Engine.WingetVersion + ")  •  Listede " + n + " program bu bilgisayarda zaten kurulu.");
                }
                else
                {
                    var set = Engine.GetInstalled(null);
                    foreach (var a in Catalog.All) a.Installed = a.Source == "office" ? Office.IsInstalled(Office.KeyOf(a)) : set.Contains(a.Id);
                    SetWingetBanner(2, "winget bulunamadı. Tüm programları kurabilmek için sağdaki butonla winget'i kur.");
                }
                Engine.Detecting = false;
                UI(() => { foreach (var c in cards) c.Invalidate(); UpdateSelCount(); });
            }) { IsBackground = true }.Start();
        }

        void FixWinget()
        {
            wingetFix.Enabled = false;
            SetWingetBanner(0, "winget kuruluyor, lütfen bekle (internet hızına göre 1-5 dakika)...");
            UI(() => wingetFix.Visible = false);
            ShowPage("Kurulum");
            Append(log, "=== winget (App Installer) kurulumu başladı ===", Theme.Accent);
            new Thread(() =>
            {
                bool ok = false;
                try { ok = Engine.BootstrapWinget(l => Append(log, l, Theme.Sub)); } catch (Exception ex) { Append(log, "Hata: " + ex.Message, Theme.Red); }
                Append(log, ok ? "winget hazır: " + Engine.WingetVersion : "winget kurulamadı. Microsoft Store'dan 'Uygulama Yükleyici' (App Installer) güncellemesini deneyebilirsin.", ok ? Theme.Green : Theme.Red);
                if (ok) StartupScan();
                else SetWingetBanner(2, "winget kurulamadı. Microsoft Store > 'Uygulama Yükleyici' güncellemesini yapıp tekrar dene.");
                UI(() => ShowPage("Programlar"));
            }) { IsBackground = true }.Start();
        }

        // ================= Install page =================
        void BuildInstall()
        {
            var page = NewPage("Kurulum");

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            page.Controls.Add(body);
            log = NewLog();
            var logWrap = Wrap(log);
            var logHost = new Panel { Dock = DockStyle.Right, Width = Theme.S(430), BackColor = Theme.Bg, Padding = new Padding(Theme.S(14), 0, 0, 0) };
            var logTitle = new Lbl("Ayrıntılı kayıt", Theme.FS(9.5f), Theme.Sub) { Dock = DockStyle.Top, AutoSize = false, Height = Theme.S(26) };
            logWrap.Dock = DockStyle.Fill;
            logHost.Controls.Add(logWrap); logHost.Controls.Add(logTitle);
            rowFlow = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg };
            rowFlow.Resize += (s, e) => { foreach (var r in rows) r.Width = RowWidth(); };
            var rowTitle = new Lbl("Kurulum sırası", Theme.FS(9.5f), Theme.Sub) { Dock = DockStyle.Top, AutoSize = false, Height = Theme.S(26) };
            body.Controls.Add(rowFlow); body.Controls.Add(rowTitle); body.Controls.Add(logHost);

            // bottom
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(60), BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(12), 0, 0) };
            page.Controls.Add(bottom);
            cancelBtn = new FlatButton("İptal Et", 2, "") { Width = Theme.S(130), Height = Theme.S(40), Enabled = false };
            cancelBtn.Click += (s, e) =>
            {
                if (!running) return;
                if (MessageBox.Show(this, "Kurulum iptal edilsin mi? (Şu an kurulan program yarıda kalabilir)", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { cancel = true; Engine.KillCurrent(); }
            };
            backBtn = new FlatButton("Programlara Dön", 1, "") { Width = Theme.S(170), Height = Theme.S(40) };
            backBtn.Click += (s, e) => ShowPage("Programlar");
            rebootToggle = new Toggle();
            var rl = new Lbl("Bitince bilgisayarı yeniden başlat", Theme.F(9.5f), Theme.Sub);
            bottom.Controls.Add(cancelBtn); bottom.Controls.Add(backBtn); bottom.Controls.Add(rebootToggle); bottom.Controls.Add(rl);
            bottom.Resize += (s, e) =>
            {
                int y = Theme.S(12);
                cancelBtn.Location = new Point(bottom.Width - cancelBtn.Width, y);
                backBtn.Location = new Point(cancelBtn.Left - backBtn.Width - Theme.S(8), y);
                rebootToggle.Location = new Point(0, y + (Theme.S(40) - rebootToggle.Height) / 2);
                rl.Location = new Point(rebootToggle.Right + Theme.S(10), y + (Theme.S(40) - rl.Height) / 2);
            };

            // overall card
            var ovWrap = new Panel { Dock = DockStyle.Top, Height = Theme.S(124), BackColor = Theme.Bg, Padding = new Padding(0, 0, 0, Theme.S(14)) };
            var ov = new RoundPanel { Dock = DockStyle.Fill, Fill = Theme.Panel };
            ovWrap.Controls.Add(ov);
            page.Controls.Add(ovWrap);
            ovTitle = new Lbl("Henüz kurulum başlatılmadı", Theme.FS(12.5f), Theme.Text) { BackColor = Theme.Panel, Location = new Point(Theme.S(20), Theme.S(16)) };
            ovSub = new Lbl("Programlar sekmesinden seçim yapıp 'Kurulumu Başlat'a bas.", Theme.F(9f), Theme.Sub) { BackColor = Theme.Panel, Location = new Point(Theme.S(21), Theme.S(44)) };
            ovPercent = new Lbl("0%", Theme.F(24f, FontStyle.Bold), Theme.Accent) { BackColor = Theme.Panel, AutoSize = false, Size = new Size(Theme.S(150), Theme.S(48)), TextAlign = ContentAlignment.MiddleRight };
            overall = new ProgressX { Height = Theme.S(10) };
            ov.Controls.Add(ovTitle); ov.Controls.Add(ovSub); ov.Controls.Add(ovPercent); ov.Controls.Add(overall);
            ov.Resize += (s, e) =>
            {
                ovPercent.Location = new Point(ov.Width - ovPercent.Width - Theme.S(20), Theme.S(12));
                overall.Location = new Point(Theme.S(20), ov.Height - Theme.S(28));
                overall.Width = ov.Width - Theme.S(40);
            };

            page.Controls.Add(Header("Kurulum", "Programlar sırayla, sessiz modda ve otomatik olarak kurulur. Bu sırada bilgisayarı kullanmaya devam edebilirsin."));
        }

        int RowWidth() { return Math.Max(Theme.S(300), rowFlow.ClientSize.Width - (rowFlow.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth) - Theme.S(6)); }

        void StartInstall() { StartInstall(Catalog.All.Where(a => a.Selected).ToList(), null); }
        void StartInstall(List<AppItem> sel, string profileName)
        {
            if (running) { MessageBox.Show(this, "Şu an bir kurulum zaten çalışıyor.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (sel.Count == 0) return;
            if (Engine.Detecting) { MessageBox.Show(this, "Kurulu programlar hâlâ taranıyor. Birkaç saniye sonra tekrar bas.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            bool skip = skipToggle.On;
            // runtimes first
            sel = sel.OrderBy(a => a.Category == "Sistem Bileşenleri" ? 0 : a.Source == "custom" ? 2 : 1).ThenBy(a => Array.IndexOf(Catalog.Categories, a.Category)).ToList();
            if (!Engine.WingetOk)
            {
                int can = sel.Count(a => a.FallbackUrl != null || a.Source == "dism" || a.Source == "custom" || a.Source == "office");
                if (MessageBox.Show(this, "winget bulunamadı. Şu an seçtiğin " + sel.Count + " programdan sadece " + can + " tanesi doğrudan indirilerek kurulabilir.\n\nÖnce üstteki 'winget'i Kur / Onar' butonunu kullanman önerilir. Yine de devam edilsin mi?", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }
            SaveLast();
            running = true; cancel = false; started = DateTime.Now;
            ShowPage("Kurulum");
            rows.Clear(); rowFlow.Controls.Clear(); log.Clear();
            foreach (var a in sel) { var r = new InstallRow(a) { Width = RowWidth() }; rows.Add(r); }
            rowFlow.Controls.AddRange(rows.Cast<Control>().ToArray());
            cancelBtn.Enabled = true; startBtn.Enabled = false;
            overall.Value = 0; overall.Bar = Theme.Accent; ovPercent.ForeColor = Theme.Accent; ovPercent.Text = "0%";
            Append(log, "ZETX FORMATTER v" + Version + "  •  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), Theme.Accent);
            if (profileName != null) Append(log, "Profil: " + profileName, Theme.Accent2);
            Append(log, sel.Count + " program kurulacak. Yöntem: " + (Engine.WingetOk ? "winget " + Engine.WingetVersion : "doğrudan indirme"), Theme.Sub);

            new Thread(() => InstallWorker(skip)) { IsBackground = true }.Start();
        }

        int okCount, errCount, skipCount; bool needReboot; volatile float curFrac; volatile int doneCount; string curName = "";
        void InstallWorker(bool skip)
        {
            okCount = errCount = skipCount = 0; needReboot = false; doneCount = 0;
            InstallResult? officeResult = null;
            int total = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; var a = row.App;
                if (cancel) { UI(() => { row.State = 3; row.Status = "İptal edildi"; row.StatusColor = Theme.Dim; row.Invalidate(); }); continue; }
                if (skip && a.Installed)
                {
                    skipCount++; doneCount++;
                    UI(() => { row.State = 4; row.Status = "Zaten kurulu — atlandı"; row.StatusColor = Theme.Blue; row.Invalidate(); });
                    Append(log, "• " + a.Name + ": zaten kurulu, atlandı.", Theme.Blue);
                    continue;
                }
                curName = a.Name; curFrac = 0;
                UI(() => { row.State = 1; row.Status = "Başlatılıyor..."; row.StatusColor = Theme.Accent2; row.Invalidate(); rowFlow.ScrollControlIntoView(row); });
                Append(log, "", Theme.Sub);
                Append(log, "▶ " + a.Name + "  (" + a.Id + ")", Theme.Text);
                InstallResult res;
                if (a.Source == "office" && officeResult.HasValue)
                {
                    res = officeResult.Value;
                    Append(log, "  Office paketiyle birlikte kuruldu.", Theme.Dim);
                }
                else if (a.Source == "office")
                {
                    var keys = rows.Where(x => x.App.Source == "office" && !(skip && x.App.Installed)).Select(x => Office.KeyOf(x.App)).ToList();
                    var officeRows = rows.Where(x => x.App.Source == "office" && x != row && x.State == 0).ToList();
                    UI(() => { foreach (var orow in officeRows) { orow.Status = "Office ile birlikte kurulacak..."; orow.StatusColor = Theme.Accent2; orow.Invalidate(); } });
                    try
                    {
                        res = Office.Install(keys, l => Append(log, l, Theme.Dim), (st, p) =>
                        {
                            if (p >= 0) curFrac = p;
                            UI(() => { row.Status = st; row.Progress = p; row.Invalidate(); });
                        }, () => cancel);
                    }
                    catch (Exception ex) { Append(log, "  Hata: " + ex.Message, Theme.Red); res = InstallResult.Failed; }
                    officeResult = res;
                }
                else try
                {
                    res = Engine.Install(a, l => Append(log, l, Theme.Dim), (st, p) =>
                    {
                        if (p >= 0) curFrac = p;
                        UI(() => { row.Status = st; row.Progress = p; row.Invalidate(); });
                    }, () => cancel);
                }
                catch (Exception ex) { Append(log, "  Hata: " + ex.Message, Theme.Red); res = InstallResult.Failed; }
                doneCount++; curFrac = 0;
                string txt; Color col; int state;
                switch (res)
                {
                    case InstallResult.Ok: txt = "Kuruldu"; col = Theme.Green; state = 2; okCount++; a.Installed = true; break;
                    case InstallResult.OkReboot: txt = "Kuruldu (yeniden başlatma gerekli)"; col = Theme.Green; state = 2; okCount++; needReboot = true; a.Installed = true; break;
                    case InstallResult.Already: txt = "Zaten kurulu ve güncel"; col = Theme.Blue; state = 4; skipCount++; a.Installed = true; break;
                    case InstallResult.NotFound: txt = "Paket bulunamadı"; col = Theme.Red; state = 3; errCount++; break;
                    case InstallResult.NoWinget: txt = "winget gerekli — önce winget'i kur"; col = Theme.Yellow; state = 3; errCount++; break;
                    case InstallResult.Cancelled: txt = "İptal edildi"; col = Theme.Dim; state = 3; break;
                    default: txt = "Kurulamadı (ayrıntı kayıtta)"; col = Theme.Red; state = 3; errCount++; break;
                }
                Append(log, (state == 3 ? "✖ " : "✔ ") + a.Name + ": " + txt, col);
                UI(() => { row.State = state; row.Status = txt; row.StatusColor = col; row.Progress = -1; row.Invalidate(); });
            }
            running = false;
            var elapsed = DateTime.Now - started;
            UI(() =>
            {
                cancelBtn.Enabled = false;
                overall.Value = 1; overall.Bar = errCount == 0 ? Theme.Green : Theme.Yellow; ovPercent.ForeColor = overall.Bar; ovPercent.Text = cancel ? "—" : "100%";
                ovTitle.Text = cancel ? "Kurulum iptal edildi" : errCount == 0 ? "Tüm kurulumlar tamamlandı!" : "Kurulum tamamlandı (" + errCount + " hata)";
                ovSub.Text = okCount + " kuruldu  •  " + skipCount + " zaten vardı  •  " + errCount + " hata  •  Süre: " + FormatSpan(elapsed) + (needReboot ? "  •  Yeniden başlatma önerilir" : "");
                navInstall.Badge = null; navInstall.Invalidate();
                foreach (var c in cards) c.Invalidate();
                UpdateSelCount();
                try { FlashWindow(Handle, true); System.Media.SystemSounds.Asterisk.Play(); } catch { }
                if (!cancel && rebootToggle.On)
                {
                    Process.Start(new ProcessStartInfo("shutdown", "/r /t 60 /c \"ZETX FORMATTER: Kurulum bitti, bilgisayar 60 saniye içinde yeniden başlatılacak. İptal: shutdown /a\"") { CreateNoWindow = true, UseShellExecute = false });
                    MessageBox.Show(this, "Bilgisayar 60 saniye içinde yeniden başlatılacak.\nİptal etmek için Tamam'a basıp 'Hayır' de.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if (MessageBox.Show(this, "Yeniden başlatma yapılsın mı?", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                        Process.Start(new ProcessStartInfo("shutdown", "/a") { CreateNoWindow = true, UseShellExecute = false });
                }
            });
        }

        static string FormatSpan(TimeSpan t) { return t.TotalHours >= 1 ? string.Format("{0}sa {1}dk", (int)t.TotalHours, t.Minutes) : string.Format("{0}dk {1:00}sn", t.Minutes, t.Seconds); }

        void OnTick()
        {
            if (!running) return;
            foreach (var r in rows) if (r.State == 1) r.Tick();
            int total = Math.Max(1, rows.Count);
            float v = (doneCount + Math.Min(0.95f, curFrac)) / total;
            overall.Value = v;
            ovPercent.Text = (int)(v * 100) + "%";
            ovTitle.Text = cancel ? "İptal ediliyor..." : (Math.Min(doneCount + 1, total)) + " / " + total + "  —  " + curName + " kuruluyor";
            ovSub.Text = "Geçen süre: " + FormatSpan(DateTime.Now - started) + "   •   Kurulan: " + okCount + "   •   Hata: " + errCount;
            navInstall.Badge = doneCount + "/" + total; navInstall.Invalidate();
        }

        // ================= Updates page =================
        void BuildUpdates()
        {
            var page = NewPage("Güncellemeler");
            updLog = NewLog();
            var w = Wrap(updLog, 14); w.Dock = DockStyle.Fill;
            page.Controls.Add(w);
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Theme.S(56), BackColor = Theme.Bg, WrapContents = false };
            scanBtn = new FlatButton("Güncellemeleri Tara", 1, "") { Width = Theme.S(200), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), 0) };
            updBtn = new FlatButton("Tümünü Güncelle", 0, "") { Width = Theme.S(190), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), 0) };
            var winUpd = new FlatButton("Windows Update'i Aç", 1, "") { Width = Theme.S(200), Height = Theme.S(40) };
            row.Controls.Add(scanBtn); row.Controls.Add(updBtn); row.Controls.Add(winUpd);
            page.Controls.Add(row);
            page.Controls.Add(Header("Güncellemeler", "Bilgisayarındaki tüm programların yeni sürümlerini bul ve tek tıkla hepsini güncelle."));
            Append(updLog, "Kurulu programların güncellemelerini görmek için 'Güncellemeleri Tara'ya bas.", Theme.Sub);
            scanBtn.Click += (s, e) => RunUpd("upgrade --include-unknown --accept-source-agreements --disable-interactivity", "Güncellemeler taranıyor...");
            updBtn.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Güncellemesi olan tüm programlar sessizce güncellensin mi?\n(Güncellenen programlar kapanabilir.)", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    RunUpd("upgrade --all --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", "Tüm programlar güncelleniyor...");
            };
            winUpd.Click += (s, e) => { try { Process.Start("ms-settings:windowsupdate"); } catch { } };
        }
        void RunUpd(string args, string title)
        {
            if (!Engine.WingetOk) { Append(updLog, "winget bulunamadı. Programlar sekmesindeki 'winget'i Kur / Onar' butonunu kullan.", Theme.Red); return; }
            if (running) { Append(updLog, "Şu an bir kurulum çalışıyor, bitince tekrar dene.", Theme.Yellow); return; }
            scanBtn.Enabled = updBtn.Enabled = false; updLog.Clear();
            Append(updLog, title, Theme.Accent);
            new Thread(() =>
            {
                int code = 0;
                try
                {
                    code = Engine.Run("winget", args, l =>
                    {
                        var t = l.TrimEnd();
                        if (t.Trim().Length <= 2) return;
                        int junk = t.Count(c => "█▒░▓-\\|/ ".IndexOf(c) >= 0);
                        if (junk > t.Length * 0.8 && !t.StartsWith("---")) return;
                        Append(updLog, t, t.StartsWith("Successfully") || t.Contains("Başarıyla") ? Theme.Green : Theme.Text);
                    }, null);
                }
                catch (Exception ex) { Append(updLog, "Hata: " + ex.Message, Theme.Red); }
                Append(updLog, "", Theme.Sub);
                Append(updLog, "İşlem bitti.", Theme.Green);
                UI(() => scanBtn.Enabled = updBtn.Enabled = true);
            }) { IsBackground = true }.Start();
        }

        // ================= System page =================
        void BuildSystem()
        {
            var page = NewPage("Sistem & Ayarlar");
            var scroll = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg };
            page.Controls.Add(scroll);
            page.Controls.Add(Header("Sistem & Ayarlar", "Bilgisayarının özeti ve format sonrası önerilen Windows ayarları."));

            // info cards
            var info = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Theme.Bg, Margin = new Padding(0) };
            scroll.Controls.Add(info);
            var vals = new Dictionary<string, Label>();
            foreach (var t in new[] { new[] { "", "İşletim Sistemi" }, new[] { "", "İşlemci" }, new[] { "", "Ekran Kartı" }, new[] { "", "Bellek (RAM)" }, new[] { "", "Diskler" }, new[] { "", "Bilgisayar" } })
            {
                var card = new RoundPanel { Size = new Size(Theme.S(330), Theme.S(86)), Fill = Theme.Card, Margin = new Padding(0, 0, Theme.S(12), Theme.S(12)) };
                card.Controls.Add(new Lbl(t[0], Theme.Icon(16f), Theme.Accent) { BackColor = Theme.Card, Location = new Point(Theme.S(16), Theme.S(18)) });
                card.Controls.Add(new Lbl(t[1].ToUpper(TR), Theme.FS(8f), Theme.Dim) { BackColor = Theme.Card, Location = new Point(Theme.S(56), Theme.S(14)) });
                var v = new Label { Text = "Okunuyor...", Font = Theme.FS(9.5f), ForeColor = Theme.Text, BackColor = Theme.Card, Location = new Point(Theme.S(56), Theme.S(34)), Size = new Size(Theme.S(262), Theme.S(42)), AutoEllipsis = true, UseMnemonic = false };
                card.Controls.Add(v); vals[t[1]] = v;
                info.Controls.Add(card);
            }
            gpuValue = vals["Ekran Kartı"];
            new Thread(() =>
            {
                string os = Engine.Wmi("Win32_OperatingSystem", "Caption"), build = Engine.Wmi("Win32_OperatingSystem", "BuildNumber");
                string cpu = Engine.Wmi("Win32_Processor", "Name"), gpu = Engine.Wmi("Win32_VideoController", "Name");
                string ram = "-";
                try { double b; if (double.TryParse(Engine.Wmi("Win32_ComputerSystem", "TotalPhysicalMemory"), out b)) ram = Math.Round(b / 1073741824.0) + " GB"; } catch { }
                string disks = "";
                try { foreach (var d in DriveInfo.GetDrives()) if (d.IsReady && d.DriveType == DriveType.Fixed) disks += d.Name.TrimEnd('\\') + " " + (d.TotalFreeSpace / 1073741824) + "/" + (d.TotalSize / 1073741824) + " GB boş   "; } catch { }
                string pc = Environment.MachineName + "  •  " + Environment.UserName;
                if (Theme.Shot) { os = "Windows 11 Pro"; build = "26100"; cpu = "AMD Ryzen 9 5950X 16-Core Processor"; gpu = "NVIDIA GeForce RTX 3070 Ti"; ram = "32 GB"; disks = "C: 412/931 GB boş   D: 1240/1863 GB boş"; pc = "ZETX-PC  •  Volkan"; }
                UI(() =>
                {
                    vals["İşletim Sistemi"].Text = os.Replace("Microsoft ", "") + "  (Build " + build + ")";
                    vals["İşlemci"].Text = cpu; vals["Ekran Kartı"].Text = gpu; vals["Bellek (RAM)"].Text = ram;
                    vals["Diskler"].Text = disks.Trim(); vals["Bilgisayar"].Text = pc;
                });
            }) { IsBackground = true }.Start();

            // driver + tools row
            var tools = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Theme.Bg, Margin = new Padding(0, Theme.S(4), 0, Theme.S(16)) };
            scroll.Controls.Add(tools);
            var drv = new FlatButton("Ekran Kartı Sürücüsünü İndir", 0, "") { Width = Theme.S(270), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), Theme.S(8)) };
            drv.Click += (s, e) =>
            {
                string g = (gpuValue.Text ?? "").ToLowerInvariant();
                string url = g.Contains("nvidia") || g.Contains("geforce") || g.Contains("rtx") ? "https://www.nvidia.com/tr-tr/drivers/"
                    : g.Contains("amd") || g.Contains("radeon") ? "https://www.amd.com/en/support/download/drivers.html"
                    : "https://www.intel.com/content/www/us/en/support/detect.html";
                try { Process.Start(url); } catch { }
            };
            var rp = new FlatButton("Geri Yükleme Noktası Oluştur", 1, "") { Width = Theme.S(270), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), Theme.S(8)) };
            rp.Click += (s, e) =>
            {
                rp.Enabled = false; rp.Text = "Oluşturuluyor...";
                new Thread(() =>
                {
                    var o = Engine.Capture("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive $env:SystemDrive -ErrorAction SilentlyContinue; Checkpoint-Computer -Description 'ZETX FORMATTER' -RestorePointType MODIFY_SETTINGS; if($?){'OK'}\"", 300000) ?? "";
                    UI(() =>
                    {
                        rp.Enabled = true; rp.Text = "Geri Yükleme Noktası Oluştur";
                        MessageBox.Show(this, o.Contains("OK") ? "Geri yükleme noktası oluşturuldu." : "Geri yükleme noktası oluşturulamadı.\n(Windows aynı gün içinde birden fazla noktaya izin vermeyebilir.)\n\n" + o.Trim(), "ZETX FORMATTER", MessageBoxButtons.OK, o.Contains("OK") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    });
                }) { IsBackground = true }.Start();
            };
            var dm = new FlatButton("Aygıt Yöneticisi", 1, "") { Width = Theme.S(180), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), Theme.S(8)) };
            dm.Click += (s, e) => { try { Process.Start("devmgmt.msc"); } catch { } };
            var act = new FlatButton("Windows Etkinleştirme", 1, "") { Width = Theme.S(210), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), Theme.S(8)) };
            act.Click += (s, e) => { try { Process.Start("ms-settings:activation"); } catch { } };
            tools.Controls.Add(drv); tools.Controls.Add(rp); tools.Controls.Add(dm); tools.Controls.Add(act);

            // tweaks
            scroll.Controls.Add(new Lbl("Önerilen Windows Ayarları", Theme.FS(12f), Theme.Text) { Margin = new Padding(0, 0, 0, Theme.S(4)) });
            scroll.Controls.Add(new Lbl("Açmak istediklerini seç ve 'Seçilenleri Uygula'ya bas. Bazı ayarlar oturumu kapatıp açınca tam etkin olur.", Theme.F(9f), Theme.Sub) { Margin = new Padding(0, 0, 0, Theme.S(10)) });
            var tl = new RoundPanel { Fill = Theme.Card, Size = new Size(Theme.S(700), Theme.S(10)), Margin = new Padding(0, 0, 0, Theme.S(12)) };
            int y = Theme.S(8);
            foreach (var tw in Tweak.All())
            {
                var tg = new Toggle { BackColor = Theme.Card };
                tg.Location = new Point(Theme.S(16), y + Theme.S(14));
                var nl = new Lbl(tw.Name, Theme.FS(10f), Theme.Text) { BackColor = Theme.Card, Location = new Point(Theme.S(76), y + Theme.S(7)) };
                var dl = new Lbl(tw.Desc, Theme.F(8.5f), Theme.Sub) { BackColor = Theme.Card, Location = new Point(Theme.S(76), y + Theme.S(28)) };
                tl.Controls.Add(tg); tl.Controls.Add(nl); tl.Controls.Add(dl);
                tweakToggles.Add(new KeyValuePair<Tweak, Toggle>(tw, tg));
                y += Theme.S(54);
            }
            tl.Height = y + Theme.S(8);
            scroll.Controls.Add(tl);
            var applyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, Theme.S(20)) };
            var apply = new FlatButton("Seçilenleri Uygula", 0, "") { Width = Theme.S(210), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), 0) };
            var restartExp = new FlatButton("Gezgin'i Yeniden Başlat", 1, "") { Width = Theme.S(230), Height = Theme.S(40) };
            applyRow.Controls.Add(apply); applyRow.Controls.Add(restartExp);
            scroll.Controls.Add(applyRow);
            apply.Click += (s, e) =>
            {
                int n = 0; var errs = new List<string>();
                foreach (var kv in tweakToggles)
                    if (kv.Value.On) { try { kv.Key.Apply(); n++; } catch (Exception ex) { errs.Add(kv.Key.Name + ": " + ex.Message); } }
                MessageBox.Show(this, n + " ayar uygulandı." + (errs.Count > 0 ? "\n\nHatalar:\n" + string.Join("\n", errs) : "") + "\n\nDeğişikliklerin görünmesi için 'Gezgin'i Yeniden Başlat'a basabilirsin.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshTweaks();
            };
            restartExp.Click += (s, e) =>
            {
                try { foreach (var p in Process.GetProcessesByName("explorer")) p.Kill(); } catch { }
                Thread.Sleep(800);
                try { if (Process.GetProcessesByName("explorer").Length == 0) Process.Start("explorer.exe"); } catch { }
            };
            scroll.Resize += (s, e) => tl.Width = Math.Max(Theme.S(500), Math.Min(Theme.S(1010), scroll.ClientSize.Width - Theme.S(20)));
        }
        void RefreshTweaks()
        {
            new Thread(() =>
            {
                foreach (var kv in tweakToggles)
                {
                    bool on = false; try { on = kv.Key.IsOn(); } catch { }
                    var t = kv.Value; UI(() => t.On = on);
                }
            }) { IsBackground = true }.Start();
        }

        // ================= About page =================
        void BuildAbout()
        {
            var page = NewPage("Hakkında");
            var host = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg };
            page.Controls.Add(host);
            page.Controls.Add(Header("Hakkında", "ZETX FORMATTER hakkında bilgiler ve iletişim."));

            var card = new RoundPanel { Size = new Size(Theme.S(720), Theme.S(212)), Fill = Theme.Panel, Margin = new Padding(0, 0, 0, Theme.S(14)) };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics; Theme.Hq(g);
                Theme.DrawLogo(g, new RectangleF(Theme.S(28), Theme.S(28), Theme.S(84), Theme.S(84)));
                using (var f = Theme.F(22f, FontStyle.Bold))
                {
                    Theme.DrawText(g, "ZETX", f, Theme.Text, new Rectangle(Theme.S(132), Theme.S(30), Theme.S(110), Theme.S(44)), TextFormatFlags.Left);
                    var w = TextRenderer.MeasureText(g, "ZETX", f).Width;
                    Theme.DrawText(g, "FORMATTER", f, Theme.Accent, new Rectangle(Theme.S(132) + w - Theme.S(6), Theme.S(30), Theme.S(260), Theme.S(44)), TextFormatFlags.Left);
                }
                using (var f = Theme.F(9.5f)) Theme.DrawText(g, "Sürüm " + Version + "  •  Windows 10 / 11", f, Theme.Sub, new Rectangle(Theme.S(134), Theme.S(74), Theme.S(400), Theme.S(20)), TextFormatFlags.Left);
                using (var f = Theme.F(9.5f))
                    Theme.DrawText(g, "Format attıktan sonra Chrome, Discord, Steam, OBS, VLC ve onlarca programı tek tek aramadan, tek tıkla, sessiz modda ve en güncel sürümüyle kurmak için geliştirildi. Kurulumlar resmi kaynaklardan (winget / üretici siteleri) yapılır.", f, Theme.Text, new Rectangle(Theme.S(28), Theme.S(130), card.Width - Theme.S(56), Theme.S(60)), TextFormatFlags.WordBreak);
            };
            host.Controls.Add(card);

            var contact = new RoundPanel { Size = new Size(Theme.S(720), Theme.S(196)), Fill = Theme.Panel, Margin = new Padding(0, 0, 0, Theme.S(14)) };
            contact.Controls.Add(new Lbl("İLETİŞİM", Theme.FS(8.5f), Theme.Dim) { BackColor = Theme.Panel, Location = new Point(Theme.S(28), Theme.S(18)) });
            AddContact(contact, Theme.S(46), "", "Program Sahibi", "Volkan Sezer", null);
            AddContact(contact, Theme.S(92), "", "Discord", "discord.gg/nvqc84AHZf", Discord);
            AddContact(contact, Theme.S(138), "", "E-posta", Mail, "mailto:" + Mail);
            host.Controls.Add(contact);

            var feat = new RoundPanel { Size = new Size(Theme.S(720), Theme.S(290)), Fill = Theme.Panel };
            feat.Controls.Add(new Lbl("ÖZELLİKLER", Theme.FS(8.5f), Theme.Dim) { BackColor = Theme.Panel, Location = new Point(Theme.S(28), Theme.S(18)) });
            string[] fs = {
                Catalog.All.Count + " popüler program, " + Catalog.Categories.Length + " kategori, arama ve hazır paketler",
                "Sessiz (otomatik) kurulum — hiçbir kurulum ekranıyla uğraşmazsın",
                "Kurulu programları algılar ve tekrar kurmaz",
                "Kendi profillerini oluştur (Ev PC, Oyun PC...), tek tıkla kur, dosya olarak başka PC'ye taşı",
                "Listede olmayan programları ekle: .exe / .msi dosyası veya indirme linki",
                "Özel programlarda sessiz kurulum algılama (MSI, Inno Setup, NSIS)",
                "Tüm programları tek tıkla güncelleme",
                "winget yoksa otomatik kurulum/onarım + yedek doğrudan indirme",
                "Önerilen Windows ayarları, geri yükleme noktası ve sürücü kısayolları" };
            int yy = Theme.S(46);
            foreach (var f in fs)
            {
                feat.Controls.Add(new Lbl("", Theme.Icon(9f), Theme.Accent) { BackColor = Theme.Panel, Location = new Point(Theme.S(28), yy + Theme.S(3)) });
                feat.Controls.Add(new Lbl(f, Theme.F(9.5f), Theme.Text) { BackColor = Theme.Panel, Location = new Point(Theme.S(54), yy) });
                yy += Theme.S(25);
            }
            host.Controls.Add(feat);
            host.Controls.Add(new Lbl("© " + DateTime.Now.Year + " Volkan Sezer — ZETX. Tüm hakları saklıdır.", Theme.F(8.5f), Theme.Dim) { Margin = new Padding(Theme.S(4), Theme.S(14), 0, Theme.S(10)) });
        }
        void AddContact(Control parent, int y, string glyph, string title, string value, string link)
        {
            var ic = new RoundPanel { Size = new Size(Theme.S(36), Theme.S(36)), Fill = Color.FromArgb(40, 255, 106, 26), Stroke = Color.Empty, Radius = 9, BackColor = Theme.Panel, Location = new Point(Theme.S(28), y) };
            ic.Controls.Add(new Lbl(glyph, Theme.Icon(12f), Theme.Accent) { BackColor = Color.Transparent, Location = new Point(Theme.S(9), Theme.S(10)) });
            parent.Controls.Add(ic);
            parent.Controls.Add(new Lbl(title, Theme.F(8.5f), Theme.Sub) { BackColor = Theme.Panel, Location = new Point(Theme.S(76), y - Theme.S(1)) });
            if (link == null) parent.Controls.Add(new Lbl(value, Theme.FS(10.5f), Theme.Text) { BackColor = Theme.Panel, Location = new Point(Theme.S(76), y + Theme.S(15)) });
            else
            {
                var ll = new LinkLabel { Text = value, Font = Theme.FS(10.5f), AutoSize = true, BackColor = Theme.Panel, LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent2, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline, Location = new Point(Theme.S(74), y + Theme.S(15)) };
                ll.LinkClicked += (s, e) => { try { Process.Start(link); } catch { } };
                parent.Controls.Add(ll);
                var copy = new FlatButton("Kopyala", 1, "") { Width = Theme.S(110), Height = Theme.S(32), Font = Theme.F(9f), Location = new Point(Theme.S(720) - Theme.S(140), y + Theme.S(2)) };
                copy.Click += (s, e) => { try { Clipboard.SetText(link.StartsWith("mailto:") ? value : link); copy.Text = "Kopyalandı"; copy.Invalidate(); } catch { } };
                parent.Controls.Add(copy);
            }
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
    }
}
