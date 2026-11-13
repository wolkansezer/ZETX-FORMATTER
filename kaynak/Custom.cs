using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace ZetxFormatter
{
    public class UserProfile
    {
        public string Name; public List<string> Ids = new List<string>();
    }

    public static class Custom
    {
        public const string Category = "Benim Eklediklerim";
        public static string StoreFile { get { return Path.Combine(Engine.DataDir, "ozel_programlar.txt"); } }
        public static string ProfileDir { get { return Path.Combine(Engine.DataDir, "profiller"); } }

        public static readonly string[] SilentModes = { "auto", "none", "S", "verysilent", "quiet", "qn", "custom" };
        public static readonly string[] SilentNames = {
            "Otomatik algıla (önerilen)", "Sessiz değil — kurulum penceresi açılsın", "/S  (NSIS kurulumları)",
            "/VERYSILENT  (Inno Setup kurulumları)", "/quiet  (Microsoft kurulumları)", "/qn  (MSI paketleri)", "Özel parametre yaz" };

        static string Clean(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim(); }

        public static string Serialize(AppItem a)
        {
            return string.Join("\t", new[] { a.Id, Clean(a.Name), a.CustomKind, Clean(a.CustomSrc), a.SilentMode, Clean(a.CustomArgs), Clean(a.Desc) });
        }
        public static AppItem Parse(string line)
        {
            var p = line.Split('\t');
            if (p.Length < 4 || !p[0].StartsWith("custom:")) return null;
            var a = new AppItem
            {
                Id = p[0], Name = p[1], CustomKind = p[2] == "file" ? "file" : "url", CustomSrc = p[3],
                SilentMode = p.Length > 4 && p[4].Length > 0 ? p[4] : "auto", CustomArgs = p.Length > 5 ? p[5] : "",
                Category = Category, Source = "custom"
            };
            a.Desc = p.Length > 6 && p[6].Length > 0 ? p[6] : DefaultDesc(a);
            a.Domain = a.CustomKind == "url" ? HostOf(a.CustomSrc) : "";
            return a;
        }
        public static string DefaultDesc(AppItem a)
        {
            return a.CustomKind == "file" ? "Bilgisayardan: " + Path.GetFileName(a.CustomSrc) : "Bağlantı: " + HostOf(a.CustomSrc);
        }
        public static string HostOf(string url)
        {
            try { return new Uri(url).Host; } catch { return ""; }
        }

        public static void LoadStore()
        {
            try
            {
                if (!File.Exists(StoreFile)) return;
                foreach (var l in File.ReadAllLines(StoreFile, Encoding.UTF8))
                {
                    var a = Parse(l);
                    if (a != null && Catalog.Find(a.Id) == null) Catalog.All.Add(a);
                }
            }
            catch { }
        }
        public static void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Engine.DataDir);
                File.WriteAllLines(StoreFile, Catalog.All.Where(x => x.Source == "custom").Select(Serialize).ToArray(), Encoding.UTF8);
            }
            catch { }
        }
        public static string NewId() { return "custom:" + Guid.NewGuid().ToString("N").Substring(0, 12); }

        public static Image IconFor(AppItem a)
        {
            if (a.CustomKind == "file")
            {
                try { if (File.Exists(a.CustomSrc) && a.CustomSrc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) using (var ic = Icon.ExtractAssociatedIcon(a.CustomSrc)) return ic.ToBitmap(); } catch { }
            }
            return null;
        }

        // ---------- profiles ----------
        static string SafeName(string n)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }
        public static List<UserProfile> LoadProfiles()
        {
            var list = new List<UserProfile>();
            try
            {
                Directory.CreateDirectory(ProfileDir);
                foreach (var f in Directory.GetFiles(ProfileDir, "*.zetx").OrderBy(File.GetCreationTime))
                {
                    var p = ReadProfileFile(f, false);
                    p.Name = Path.GetFileNameWithoutExtension(f);
                    list.Add(p);
                }
            }
            catch { }
            return list;
        }
        public static void SaveProfile(UserProfile p) { Directory.CreateDirectory(ProfileDir); WriteProfileFile(Path.Combine(ProfileDir, SafeName(p.Name) + ".zetx"), p); }
        public static void DeleteProfile(string name) { try { File.Delete(Path.Combine(ProfileDir, SafeName(name) + ".zetx")); } catch { } }
        public static bool ProfileExists(string name) { return File.Exists(Path.Combine(ProfileDir, SafeName(name) + ".zetx")); }

        /// Profile file: ids + definitions of custom programs (so it works on another PC)
        public static void WriteProfileFile(string path, UserProfile p)
        {
            var sb = new StringBuilder();
            sb.Append("# ZETX FORMATTER profil dosyası\r\n# name\t").Append(Clean(p.Name)).Append("\r\n");
            foreach (var id in p.Ids)
            {
                var a = Catalog.Find(id);
                if (a != null && a.Source == "custom") sb.Append("@custom\t").Append(Serialize(a)).Append("\r\n");
                else sb.Append(id).Append("\r\n");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        /// Reads a profile; registers unknown custom programs (when import == true they are saved to the store)
        public static UserProfile ReadProfileFile(string path, bool import)
        {
            var p = new UserProfile { Name = Path.GetFileNameWithoutExtension(path) };
            bool added = false;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var t = raw.Trim();
                if (t.StartsWith("# name\t")) { var n = t.Substring(7).Trim(); if (n.Length > 0) p.Name = n; continue; }
                if (t.Length == 0 || t.StartsWith("#")) continue;
                if (t.StartsWith("@custom\t"))
                {
                    var a = Parse(raw.Substring(raw.IndexOf('\t') + 1));
                    if (a == null) continue;
                    if (Catalog.Find(a.Id) == null) { Catalog.All.Add(a); added = true; }
                    if (!p.Ids.Contains(a.Id)) p.Ids.Add(a.Id);
                }
                else if (!p.Ids.Contains(t)) p.Ids.Add(t);
            }
            if (added) SaveStore();
            return p;
        }

        // ---------- installing a custom program ----------
        public static string DetectType(string file)
        {
            if (file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) return "msi";
            try
            {
                using (var fs = File.OpenRead(file))
                {
                    var buf = new byte[Math.Min(fs.Length, 12 * 1024 * 1024)];
                    int n = fs.Read(buf, 0, buf.Length);
                    string ascii = Encoding.ASCII.GetString(buf, 0, n);
                    string uni = Encoding.Unicode.GetString(buf, 0, n - (n % 2));
                    if (ascii.Contains("Inno Setup") || uni.Contains("Inno Setup")) return "inno";
                    if (ascii.Contains("Nullsoft") || ascii.Contains("NSIS Error")) return "nsis";
                    if (ascii.Contains("InstallShield") || uni.Contains("InstallShield")) return "installshield";
                    if (ascii.Contains("WixBundle") || uni.Contains("WixBundle") || ascii.Contains("WixBurn")) return "wix";
                }
            }
            catch { }
            return "unknown";
        }

        public static InstallResult Install(AppItem a, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            string file; bool temp = false;
            if (a.CustomKind == "file")
            {
                file = a.CustomSrc;
                if (!File.Exists(file)) { log("  Dosya bulunamadı: " + file + "  (Dosya USB bellekteyse takılı olduğundan emin ol.)"); return InstallResult.Failed; }
            }
            else
            {
                string dir = Path.Combine(Path.GetTempPath(), "ZETXFormatter"); Directory.CreateDirectory(dir);
                string ext = ".exe";
                try { var lp = new Uri(a.CustomSrc).AbsolutePath.ToLowerInvariant(); if (lp.EndsWith(".msi")) ext = ".msi"; } catch { }
                file = Path.Combine(dir, "ozel_" + a.Id.Substring(7) + ext);
                var r = Engine.Download(a.CustomSrc, file, log, status, cancelled);
                if (r != InstallResult.Ok) return r;
                temp = true;
                if (ext == ".exe" && IsMsiFile(file)) { var m = Path.ChangeExtension(file, ".msi"); if (File.Exists(m)) File.Delete(m); File.Move(file, m); file = m; }
                if (IsZipOrHtml(file)) { log("  İndirilen dosya bir kurulum dosyası değil (sayfa ya da arşiv olabilir). Doğrudan indirme linki kullan."); return InstallResult.Failed; }
            }
            string type = DetectType(file);
            string args;
            switch (a.SilentMode)
            {
                case "none": args = ""; break;
                case "S": args = "/S"; break;
                case "verysilent": args = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"; break;
                case "quiet": args = "/quiet /norestart"; break;
                case "qn": args = "/qn /norestart"; break;
                case "custom": args = a.CustomArgs ?? ""; break;
                default:
                    args = type == "msi" ? "/qn /norestart" : type == "inno" ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-" : type == "nsis" ? "/S" : type == "installshield" ? "/s /v\"/qn\"" : type == "wix" ? "/quiet /norestart" : "";
                    log("  Kurulum türü algılandı: " + (type == "unknown" ? "bilinmiyor (normal kurulum açılacak)" : type));
                    break;
            }
            bool interactive = args.Length == 0;
            status(interactive ? "Kurulum penceresi açıldı — kurulumu tamamla" : "Sessiz kuruluyor...", -1);
            log("  Çalıştırılıyor: " + Path.GetFileName(file) + (args.Length > 0 ? " " + args : "  (sessiz parametre yok)"));
            int code;
            try
            {
                code = type == "msi" || file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                    ? Engine.Run("msiexec.exe", "/i \"" + file + "\" " + (args.Length > 0 ? args : ""), null, cancelled)
                    : Engine.Run(file, args, null, cancelled);
            }
            catch (Exception ex) { log("  Çalıştırılamadı: " + ex.Message); return InstallResult.Failed; }
            if (temp) { try { File.Delete(file); } catch { } }
            if (code == int.MinValue) return InstallResult.Cancelled;
            log("  Çıkış kodu: " + code);
            if (code == 0) return InstallResult.Ok;
            if (code == 3010 || code == 1641) return InstallResult.OkReboot;
            if (code == 1638) return InstallResult.Already;
            if (code == 1602 || code == 1223) { log("  Kurulum kullanıcı tarafından iptal edildi."); return InstallResult.Failed; }
            return InstallResult.Failed;
        }
        static bool IsMsiFile(string f)
        {
            try { using (var fs = File.OpenRead(f)) { var b = new byte[8]; fs.Read(b, 0, 8); return b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0; } } catch { return false; }
        }
        static bool IsZipOrHtml(string f)
        {
            try
            {
                using (var fs = File.OpenRead(f))
                {
                    var b = new byte[64]; int n = fs.Read(b, 0, 64);
                    if (n >= 2 && b[0] == 'M' && b[1] == 'Z') return false;
                    if (n >= 4 && b[0] == 0xD0 && b[1] == 0xCF) return false;
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
