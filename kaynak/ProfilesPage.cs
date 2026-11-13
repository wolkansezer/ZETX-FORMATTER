using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZetxFormatter
{
    public partial class MainForm
    {
        List<UserProfile> profiles = new List<UserProfile>();
        UserProfile curProfile;
        DFlow profList, profApps; Panel profDetail, profEmpty; Lbl profName, profCount; FlatButton profInstall;

        // ================= custom programs =================
        AppCard MakeCard(AppItem a)
        {
            var c = new AppCard(a);
            c.Toggled += (s, e) => { UpdateSelCount(); if (filter == "Seçilenler") RefreshCards(); };
            if (a.Source == "custom")
            {
                var m = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.F(9.5f), ShowImageMargin = false };
                m.Items.Add("Düzenle", null, (s, e) => AddCustomApp(c.App));
                m.Items.Add("Profile ekle...", null, (s, e) => AddAppsToProfileMenu(new List<string> { c.App.Id }));
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("Sil", null, (s, e) => DeleteCustomApp(c.App));
                foreach (ToolStripItem it in m.Items) it.ForeColor = Theme.Text;
                c.ContextMenuStrip = m;
            }
            cards.Add(c);
            return c;
        }

        void LoadCardIcon(AppCard c)
        {
            var card = c;
            if (c.App.Source == "custom")
            {
                if (c.App.CustomKind == "file") { c.App.Icon = Custom.IconFor(c.App); c.Invalidate(); return; }
                if (string.IsNullOrEmpty(c.App.Domain)) return;
            }
            Engine.LoadIcon(c.App, () => UI(() => card.Invalidate()));
        }

        AppItem AddCustomApp(AppItem edit, bool quiet = false)
        {
            using (var d = new CustomAppDialog(edit))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Result == null) return null;
                var a = d.Result;
                if (edit == null)
                {
                    Catalog.All.Add(a);
                    a.Selected = true;
                    var c = MakeCard(a);
                    LoadCardIcon(c);
                }
                else { var c = cards.FirstOrDefault(x => x.App == a); if (c != null) LoadCardIcon(c); }
                Custom.SaveStore();
                // keep profiles that contain it up to date (definition is embedded in profile files)
                foreach (var p in Custom.LoadProfiles()) if (p.Ids.Contains(a.Id)) Custom.SaveProfile(p);
                if (search.Text.Length > 0) search.Text = "";
                if (!quiet) filter = Custom.Category;
                RefreshCards();
                if (pages["Profillerim"].Visible) RefreshProfiles();
                if (edit == null && !quiet)
                    MessageBox.Show(this, "\"" + a.Name + "\" eklendi ve seçildi.\n\nİstersen sağ tıklayıp 'Profile ekle...' ile bir profiline de ekleyebilirsin.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return a;
            }
        }

        void DeleteCustomApp(AppItem a)
        {
            if (MessageBox.Show(this, "\"" + a.Name + "\" listeden silinsin mi?\n(Profillerin içinden de çıkarılır.)", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Catalog.All.Remove(a);
            var c = cards.FirstOrDefault(x => x.App == a);
            if (c != null) { cards.Remove(c); c.Dispose(); }
            Custom.SaveStore();
            foreach (var p in Custom.LoadProfiles()) if (p.Ids.Remove(a.Id)) Custom.SaveProfile(p);
            RefreshCards(); RefreshProfiles();
        }

        // ================= profiles =================
        void SaveSelectionAsProfile()
        {
            var ids = Catalog.All.Where(a => a.Selected).Select(a => a.Id).ToList();
            if (ids.Count == 0) { MessageBox.Show(this, "Önce profile koymak istediğin programları seç.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string name = AskProfileName("Profil Olarak Kaydet", "Benim Programlarım", null);
            if (name == null) return;
            var p = new UserProfile { Name = name, Ids = ids };
            Custom.SaveProfile(p);
            curProfile = null; RefreshProfiles(name);
            if (MessageBox.Show(this, "\"" + name + "\" profili " + ids.Count + " programla kaydedildi.\nProfillerim sayfasına gidilsin mi?", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                ShowPage("Profillerim");
        }

        string AskProfileName(string title, string initial, string except)
        {
            while (true)
            {
                using (var d = new InputDialog(title, "Profil adı", initial))
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return null;
                    string n = d.Value;
                    if (n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { MessageBox.Show(this, "Profil adında \\ / : * ? \" < > | karakterleri kullanılamaz.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Warning); initial = n; continue; }
                    if (!string.Equals(n, except, StringComparison.OrdinalIgnoreCase) && Custom.ProfileExists(n))
                    {
                        if (MessageBox.Show(this, "\"" + n + "\" adında bir profil zaten var. Üzerine yazılsın mı?", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { initial = n; continue; }
                    }
                    return n;
                }
            }
        }

        void AddAppsToProfileMenu(List<string> ids)
        {
            var list = Custom.LoadProfiles();
            var m = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), BackColor = Theme.Card, Font = Theme.F(9.5f), ShowImageMargin = false };
            foreach (var p in list)
            {
                var prof = p;
                m.Items.Add(p.Name + "  (" + p.Ids.Count + ")", null, (s, e) =>
                {
                    foreach (var id in ids) if (!prof.Ids.Contains(id)) prof.Ids.Add(id);
                    Custom.SaveProfile(prof);
                    MessageBox.Show(this, "\"" + prof.Name + "\" profiline eklendi.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            if (list.Count > 0) m.Items.Add(new ToolStripSeparator());
            m.Items.Add("+ Yeni profil oluştur...", null, (s, e) =>
            {
                string n = AskProfileName("Yeni Profil", "Yeni Profil", null);
                if (n == null) return;
                Custom.SaveProfile(new UserProfile { Name = n, Ids = new List<string>(ids) });
                MessageBox.Show(this, "\"" + n + "\" profili oluşturuldu.", "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            foreach (ToolStripItem it in m.Items) it.ForeColor = Theme.Text;
            m.Show(Cursor.Position);
        }

        void BuildProfiles()
        {
            var page = NewPage("Profillerim");

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            page.Controls.Add(body);

            // top buttons
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Theme.S(56), BackColor = Theme.Bg, WrapContents = false };
            var newBtn = new FlatButton("Yeni Profil", 0, "") { Width = Theme.S(160), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), 0) };
            var impBtn = new FlatButton("Dosyadan İçe Aktar", 1, "") { Width = Theme.S(200), Height = Theme.S(40), Margin = new Padding(0, 0, Theme.S(10), 0) };
            var customBtn = new FlatButton("Listede Olmayan Program Ekle", 1, "") { Width = Theme.S(270), Height = Theme.S(40), Margin = new Padding(0) };
            newBtn.Click += (s, e) => NewProfile();
            impBtn.Click += (s, e) => ImportProfile();
            customBtn.Click += (s, e) => AddCustomApp(null);
            row.Controls.Add(newBtn); row.Controls.Add(impBtn); row.Controls.Add(customBtn);

            // detail
            profDetail = new RoundPanel { Dock = DockStyle.Fill, Fill = Theme.Panel, Padding = new Padding(Theme.S(20), Theme.S(18), Theme.S(20), Theme.S(16)) };
            var dHead = new Panel { Dock = DockStyle.Top, Height = Theme.S(118), BackColor = Theme.Panel };
            profName = new Lbl("", Theme.F(16f, FontStyle.Bold), Theme.Text) { BackColor = Theme.Panel, Location = new Point(0, 0) };
            profCount = new Lbl("", Theme.F(9.5f), Theme.Sub) { BackColor = Theme.Panel, Location = new Point(Theme.S(2), Theme.S(34)) };
            var tools = new FlowLayoutPanel { Location = new Point(0, Theme.S(64)), Height = Theme.S(44), BackColor = Theme.Panel, WrapContents = false, AutoSize = true };
            var pick = new FlatButton("Program Ekle / Çıkar", 0, "") { Width = Theme.S(186), Height = Theme.S(38), Margin = new Padding(0, 0, Theme.S(8), 0), Font = Theme.FS(9.5f) };
            var addC = new FlatButton("Özel Program", 1, "") { Width = Theme.S(140), Height = Theme.S(38), Margin = new Padding(0, 0, Theme.S(8), 0), Font = Theme.FS(9.5f) };
            var ren = new FlatButton("Ad Değiştir", 1, "") { Width = Theme.S(132), Height = Theme.S(38), Margin = new Padding(0, 0, Theme.S(8), 0), Font = Theme.FS(9.5f) };
            var exp = new FlatButton("Dışa Aktar", 1, "") { Width = Theme.S(122), Height = Theme.S(38), Margin = new Padding(0, 0, Theme.S(8), 0), Font = Theme.FS(9.5f) };
            var del = new FlatButton("Profili Sil", 2, "") { Width = Theme.S(120), Height = Theme.S(34), Font = Theme.FS(9f) };
            pick.Click += (s, e) => PickForProfile();
            addC.Click += (s, e) => AddCustomToProfile();
            ren.Click += (s, e) => RenameProfile();
            exp.Click += (s, e) => ExportProfile();
            del.Click += (s, e) => DeleteProfile();
            tools.Controls.AddRange(new Control[] { pick, addC, ren, exp });
            dHead.Controls.Add(del); dHead.Resize += (s, e) => del.Location = new Point(dHead.Width - del.Width, 0);
            dHead.Controls.Add(profName); dHead.Controls.Add(profCount); dHead.Controls.Add(tools);

            var dFoot = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(58), BackColor = Theme.Panel, Padding = new Padding(0, Theme.S(12), 0, 0) };
            profInstall = new FlatButton("BU PROFİLİ KUR", 0, "") { Dock = DockStyle.Right, Width = Theme.S(220), Font = Theme.F(10.5f, FontStyle.Bold) };
            var toSel = new FlatButton("Programlar Sayfasında Seç", 1, "") { Dock = DockStyle.Right, Width = Theme.S(240) };
            profInstall.Click += (s, e) => InstallProfile();
            toSel.Click += (s, e) =>
            {
                if (curProfile == null) return;
                foreach (var a in Catalog.All) a.Selected = curProfile.Ids.Contains(a.Id);
                filter = "Seçilenler"; if (search.Text.Length > 0) search.Text = "";
                RefreshCards(); ShowPage("Programlar");
            };
            dFoot.Controls.Add(toSel); dFoot.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Theme.S(10), BackColor = Theme.Panel }); dFoot.Controls.Add(profInstall);

            profApps = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Panel };
            profApps.Resize += (s, e) => { foreach (Control c in profApps.Controls) c.Width = ProfRowWidth(); };
            profDetail.Controls.Add(profApps); profDetail.Controls.Add(dFoot); profDetail.Controls.Add(dHead);

            // empty state
            profEmpty = new RoundPanel { Dock = DockStyle.Fill, Fill = Theme.Panel, Visible = false };
            var em1 = new Lbl("Henüz profilin yok", Theme.F(15f, FontStyle.Bold), Theme.Text) { BackColor = Theme.Panel };
            var em2 = new Lbl("Profil; format sonrası kurmak istediğin programların listesidir. Örneğin \"Ev Bilgisayarım\", \"Oyun PC\", \"Müşteri Kurulumu\"...\nBir kez oluştur, her formatta tek tıkla kur. Listede olmayan programlarını da (exe/msi veya link) ekleyebilirsin.", Theme.F(9.5f), Theme.Sub) { BackColor = Theme.Panel, TextAlign = ContentAlignment.MiddleCenter };
            var em3 = new FlatButton("İlk Profilini Oluştur", 0, "") { Width = Theme.S(230), Height = Theme.S(44) };
            em3.Click += (s, e) => NewProfile();
            profEmpty.Controls.Add(em1); profEmpty.Controls.Add(em2); profEmpty.Controls.Add(em3);
            profEmpty.Resize += (s, e) =>
            {
                int cx = profEmpty.Width / 2, cy = profEmpty.Height / 2;
                em1.Location = new Point(cx - em1.Width / 2, cy - Theme.S(90));
                em2.Location = new Point(cx - em2.Width / 2, cy - Theme.S(44));
                em3.Location = new Point(cx - em3.Width / 2, cy + Theme.S(20));
            };

            // list
            var leftHost = new Panel { Dock = DockStyle.Left, Width = Theme.S(290), BackColor = Theme.Bg, Padding = new Padding(0, 0, Theme.S(14), 0) };
            var lt = new Lbl("PROFİLLER", Theme.FS(8.5f), Theme.Dim) { Dock = DockStyle.Top, AutoSize = false, Height = Theme.S(24) };
            profList = new DFlow { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg };
            leftHost.Controls.Add(profList); leftHost.Controls.Add(lt);

            body.Controls.Add(profDetail); body.Controls.Add(profEmpty); body.Controls.Add(leftHost);
            page.Controls.Add(row);
            page.Controls.Add(Header("Profillerim", "Kendi program listelerini oluştur, istediğin zaman tek tıkla kur. Profiller dosya olarak başka bilgisayara taşınabilir."));
        }

        int ProfRowWidth() { return Math.Max(Theme.S(300), profApps.ClientSize.Width - (profApps.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth) - Theme.S(4)); }

        void RefreshProfiles(string select = null)
        {
            if (profList == null) return;
            profiles = Custom.LoadProfiles();
            string want = select ?? (curProfile != null ? curProfile.Name : null);
            curProfile = profiles.FirstOrDefault(p => p.Name == want) ?? profiles.FirstOrDefault();
            profList.SuspendLayout();
            foreach (Control c in profList.Controls.Cast<Control>().ToList()) c.Dispose();
            profList.Controls.Clear();
            foreach (var p in profiles)
            {
                var it = new ProfileItem(p) { Width = Theme.S(272), Selected = p == curProfile };
                var prof = p;
                it.Click += (s, e) => { curProfile = prof; foreach (ProfileItem x in profList.Controls) { x.Selected = x.Profile == prof; x.Invalidate(); } ShowProfileDetail(); };
                profList.Controls.Add(it);
            }
            profList.ResumeLayout();
            profEmpty.Visible = profiles.Count == 0;
            profDetail.Visible = profiles.Count > 0;
            ShowProfileDetail();
        }

        void ShowProfileDetail()
        {
            if (curProfile == null) return;
            var apps = curProfile.Ids.Select(Catalog.Find).Where(a => a != null).ToList();
            int missing = curProfile.Ids.Count - apps.Count;
            profName.Text = curProfile.Name;
            int custom = apps.Count(a => a.Source == "custom");
            profCount.Text = apps.Count + " program" + (custom > 0 ? "  •  " + custom + " özel program" : "") + (missing > 0 ? "  •  " + missing + " tanınmayan kayıt" : "") + "  •  Kurulu olanlar otomatik atlanır";
            profInstall.Enabled = apps.Count > 0 && !running;
            profApps.SuspendLayout();
            foreach (Control c in profApps.Controls.Cast<Control>().ToList()) c.Dispose();
            profApps.Controls.Clear();
            if (apps.Count == 0)
                profApps.Controls.Add(new Lbl("Bu profil boş. 'Program Ekle / Çıkar' ile listeden seç ya da 'Özel Program' ile kendi programını ekle.", Theme.F(10f), Theme.Dim) { BackColor = Theme.Panel, Margin = new Padding(Theme.S(4), Theme.S(20), 0, 0) });
            int w = ProfRowWidth();
            foreach (var a in apps.OrderBy(x => Array.IndexOf(Catalog.Categories, x.Category)).ThenBy(x => x.Name))
            {
                var r = new ProfileAppRow(a) { Width = w };
                var app = a;
                r.Remove += (s, e) => { curProfile.Ids.Remove(app.Id); Custom.SaveProfile(curProfile); RefreshProfiles(); };
                r.Edit += (s, e) => AddCustomApp(app);
                profApps.Controls.Add(r);
            }
            profApps.ResumeLayout();
        }

        void NewProfile()
        {
            string n = AskProfileName("Yeni Profil", "Yeni Profil " + (profiles.Count + 1), null);
            if (n == null) return;
            var p = new UserProfile { Name = n };
            Custom.SaveProfile(p);
            RefreshProfiles(n);
            PickForProfile();
        }
        void PickForProfile()
        {
            if (curProfile == null) return;
            using (var d = new PickerDialog(curProfile.Name, curProfile.Ids))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var keep = curProfile.Ids.Where(d.Picked.Contains).ToList();
                foreach (var id in d.Picked) if (!keep.Contains(id)) keep.Add(id);
                curProfile.Ids = keep;
                Custom.SaveProfile(curProfile);
                RefreshProfiles();
            }
        }
        void AddCustomToProfile()
        {
            if (curProfile == null) return;
            var prof = curProfile.Name;
            var a = AddCustomApp(null, true);
            if (a != null)
            {
                a.Selected = false;
                var p = Custom.LoadProfiles().FirstOrDefault(x => x.Name == prof);
                if (p != null) { p.Ids.Add(a.Id); Custom.SaveProfile(p); }
                RefreshProfiles(prof);
                ShowPage("Profillerim");
            }
        }
        void RenameProfile()
        {
            if (curProfile == null) return;
            string old = curProfile.Name;
            string n = AskProfileName("Profili Yeniden Adlandır", old, old);
            if (n == null || n == old) return;
            Custom.DeleteProfile(old);
            curProfile.Name = n; Custom.SaveProfile(curProfile);
            RefreshProfiles(n);
        }
        void DeleteProfile()
        {
            if (curProfile == null) return;
            if (MessageBox.Show(this, "\"" + curProfile.Name + "\" profili silinsin mi?\n(Programlar bilgisayardan kaldırılmaz, sadece liste silinir.)", "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Custom.DeleteProfile(curProfile.Name);
            curProfile = null; RefreshProfiles();
        }
        void ExportProfile()
        {
            if (curProfile == null) return;
            using (var d = new SaveFileDialog { Filter = "ZETX Profil (*.zetx)|*.zetx", FileName = curProfile.Name + ".zetx", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    Custom.WriteProfileFile(d.FileName, curProfile);
                    bool files = curProfile.Ids.Select(Catalog.Find).Any(a => a != null && a.Source == "custom" && a.CustomKind == "file");
                    MessageBox.Show(this, "Profil dışa aktarıldı.\n\nBu dosyayı USB belleğe veya buluta kopyala; format sonrası ZETX FORMATTER > Profillerim > 'Dosyadan İçe Aktar' ile aç." +
                        (files ? "\n\nDikkat: Profilde bilgisayardaki dosyadan eklenmiş program var. O kurulum dosyalarının da aynı yolda (ör. USB) bulunması gerekir." : ""), "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
        }
        void ImportProfile()
        {
            using (var d = new OpenFileDialog { Filter = "ZETX Profil (*.zetx)|*.zetx|Tüm dosyalar|*.*", Title = "Profil dosyasını seç" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int before = Catalog.All.Count;
                    var p = Custom.ReadProfileFile(d.FileName, true);
                    foreach (var a in Catalog.All.Skip(before).ToList()) LoadCardIcon(MakeCard(a));
                    string n = p.Name;
                    if (Custom.ProfileExists(n))
                    {
                        n = AskProfileName("Profili İçe Aktar", n + " (2)", null);
                        if (n == null) return;
                    }
                    p.Name = n; Custom.SaveProfile(p);
                    RefreshCards(); RefreshProfiles(n);
                    int unknown = p.Ids.Count(id => Catalog.Find(id) == null);
                    MessageBox.Show(this, "\"" + n + "\" profili içe aktarıldı (" + p.Ids.Count + " program)." + (unknown > 0 ? "\n" + unknown + " kayıt bu sürümde tanınmadı." : ""), "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(this, "Profil okunamadı: " + ex.Message, "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        void InstallProfile()
        {
            if (curProfile == null) return;
            var apps = curProfile.Ids.Select(Catalog.Find).Where(a => a != null).ToList();
            if (apps.Count == 0) return;
            if (MessageBox.Show(this, "\"" + curProfile.Name + "\" profilindeki " + apps.Count + " program kurulsun mu?" + (skipToggle.On ? "\n(Zaten kurulu olanlar atlanır.)" : ""), "ZETX FORMATTER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            StartInstall(apps, curProfile.Name);
        }

        // ---------- profile list item ----------
        class ProfileItem : XControl
        {
            public UserProfile Profile; public bool Selected;
            public ProfileItem(UserProfile p) { Profile = p; Height = Theme.S(64); Margin = new Padding(0, 0, 0, Theme.S(8)); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Hq(g);
                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (var p = Theme.Round(r, Theme.S(10)))
                {
                    using (var b = new SolidBrush(Selected ? Theme.Mix(Theme.Card, Theme.Accent, 0.12f) : hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, p);
                    using (var pen = new Pen(Selected ? Theme.Accent : Theme.Border, Selected ? 1.6f : 1f)) g.DrawPath(pen, p);
                }
                var ic = new RectangleF(Theme.S(12), (Height - Theme.S(38)) / 2f, Theme.S(38), Theme.S(38));
                using (var p = Theme.Round(ic, Theme.S(9)))
                using (var b = new LinearGradientBrush(ic, Theme.Accent2, Theme.AccentDark, 45f)) g.FillPath(b, p);
                string ini = Profile.Name.Length > 0 ? Profile.Name.Substring(0, 1).ToUpperInvariant() : "?";
                using (var f = Theme.F(13f, FontStyle.Bold)) Theme.DrawText(g, ini, f, Color.White, Rectangle.Round(ic), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using (var f = Theme.FS(10.5f)) Theme.DrawText(g, Profile.Name, f, Theme.Text, new Rectangle(Theme.S(62), Theme.S(12), Width - Theme.S(70), Theme.S(22)), TextFormatFlags.EndEllipsis);
                using (var f = Theme.F(8.5f)) Theme.DrawText(g, Profile.Ids.Count + " program", f, Theme.Sub, new Rectangle(Theme.S(62), Theme.S(34), Width - Theme.S(70), Theme.S(18)), TextFormatFlags.EndEllipsis);
            }
        }

        // ---------- app row inside a profile ----------
        class ProfileAppRow : XControl
        {
            public AppItem App; public event EventHandler Remove, Edit;
            bool overX, overE;
            public ProfileAppRow(AppItem a) { App = a; Height = Theme.S(52); Margin = new Padding(0, 0, 0, Theme.S(6)); Cursor = Cursors.Default; BackColor = Theme.Panel; }
            Rectangle XRect { get { return new Rectangle(Width - Theme.S(44), (Height - Theme.S(32)) / 2, Theme.S(32), Theme.S(32)); } }
            Rectangle ERect { get { return new Rectangle(Width - Theme.S(82), (Height - Theme.S(32)) / 2, Theme.S(32), Theme.S(32)); } }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool x = XRect.Contains(e.Location), ed = App.Source == "custom" && ERect.Contains(e.Location);
                if (x != overX || ed != overE) { overX = x; overE = ed; Invalidate(); }
                Cursor = x || ed ? Cursors.Hand : Cursors.Default;
                base.OnMouseMove(e);
            }
            protected override void OnMouseLeave(EventArgs e) { overX = overE = false; base.OnMouseLeave(e); }
            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.Button != MouseButtons.Left) return;
                if (XRect.Contains(e.Location) && Remove != null) Remove(this, e);
                else if (App.Source == "custom" && ERect.Contains(e.Location) && Edit != null) Edit(this, e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Hq(g);
                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (var p = Theme.Round(r, Theme.S(8)))
                {
                    using (var b = new SolidBrush(hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, p);
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                }
                var ir = new RectangleF(Theme.S(10), (Height - Theme.S(32)) / 2f, Theme.S(32), Theme.S(32));
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
                using (var f = Theme.FS(10f)) Theme.DrawText(g, App.Name, f, Theme.Text, new Rectangle(Theme.S(54), Theme.S(7), Width - Theme.S(150), Theme.S(20)), TextFormatFlags.EndEllipsis);
                string sub = App.Category + (App.Installed ? "  •  KURULU" : "") + (App.Source == "custom" ? "  •  " + App.Desc : "");
                using (var f = Theme.F(8.5f)) Theme.DrawText(g, sub, f, App.Installed ? Theme.Green : Theme.Sub, new Rectangle(Theme.S(54), Theme.S(27), Width - Theme.S(150), Theme.S(18)), TextFormatFlags.EndEllipsis);
                Action<Rectangle, string, bool, Color> btn = (rc, glyph, on, col) =>
                {
                    using (var p = Theme.Round(rc, Theme.S(7))) using (var b = new SolidBrush(on ? Color.FromArgb(50, col) : Color.Transparent)) g.FillPath(b, p);
                    using (var f = Theme.Icon(9.5f)) Theme.DrawText(g, glyph, f, on ? col : Theme.Dim, rc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
                btn(XRect, "", overX, Theme.Red);
                if (App.Source == "custom") btn(ERect, "", overE, Theme.Accent);
            }
        }
    }
}
