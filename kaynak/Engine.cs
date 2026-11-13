using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace ZetxFormatter
{
    public enum InstallResult { Ok, OkReboot, Already, NotFound, Failed, Cancelled, NoWinget }

    public static class Engine
    {
        public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZETX Formatter");
        public static bool WingetOk; public static volatile bool Detecting = true; public static string WingetVersion = "";
        static Process current; static readonly object plock = new object();

        public static int Run(string file, string args, Action<string> onLine, Func<bool> cancelled, int timeoutMs = 0)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = new Process { StartInfo = psi })
            {
                DataReceivedEventHandler h = (s, e) => { if (e.Data != null && onLine != null) onLine(e.Data); };
                p.OutputDataReceived += h; p.ErrorDataReceived += h;
                p.Start();
                lock (plock) current = p;
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                var sw = Stopwatch.StartNew();
                while (!p.WaitForExit(250))
                {
                    if ((cancelled != null && cancelled()) || (timeoutMs > 0 && sw.ElapsedMilliseconds > timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        lock (plock) current = null;
                        return int.MinValue;
                    }
                }
                p.WaitForExit();
                lock (plock) current = null;
                return p.ExitCode;
            }
        }

        public static string Capture(string file, string args, int timeoutMs = 60000)
        {
            var sb = new StringBuilder();
            try { Run(file, args, l => { lock (sb) sb.AppendLine(l); }, null, timeoutMs); } catch { return null; }
            return sb.ToString();
        }

        public static bool DetectWinget()
        {
            try
            {
                var o = Capture("winget", "--version", 20000);
                if (o != null && Regex.IsMatch(o, @"v?\d+\.\d+")) { WingetOk = true; WingetVersion = o.Trim(); return true; }
            }
            catch { }
            WingetOk = false; return false;
        }

        public static HashSet<string> GetInstalled(Action<string> log)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (WingetOk)
            {
                try
                {
                    Directory.CreateDirectory(DataDir);
                    string f = Path.Combine(DataDir, "installed.json");
                    if (File.Exists(f)) File.Delete(f);
                    Run("winget", "export -o \"" + f + "\" --accept-source-agreements --disable-interactivity", null, null, 180000);
                    if (File.Exists(f))
                        foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"PackageIdentifier\"\\s*:\\s*\"([^\"]+)\""))
                            set.Add(m.Groups[1].Value);
                }
                catch (Exception ex) { if (log != null) log("Kurulu program listesi alınamadı: " + ex.Message); }
            }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5"))
                    if (k != null && Convert.ToInt32(k.GetValue("Install", 0)) == 1) set.Add("NetFx3");
            }
            catch { }
            return set;
        }

        static readonly Regex sizeRx = new Regex(@"([\d\.,]+)\s*(KB|MB|GB)\s*/\s*([\d\.,]+)\s*(KB|MB|GB)", RegexOptions.IgnoreCase);
        static double ToBytes(string n, string u)
        {
            double v; double.TryParse(n.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v);
            u = u.ToUpperInvariant(); return v * (u == "KB" ? 1e3 : u == "MB" ? 1e6 : 1e9);
        }
        static bool IsNoise(string l)
        {
            var t = l.Trim();
            if (t.Length <= 2) return true;
            int junk = 0; foreach (char c in t) if ("█▒░▓-\\|/ ".IndexOf(c) >= 0) junk++;
            return junk > t.Length * 0.7;
        }

        public static InstallResult Install(AppItem a, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            if (a.Source == "dism") return InstallDism(a, log, status, cancelled);
            if (a.Source == "custom") return Custom.Install(a, log, status, cancelled);
            if (a.Source == "office") return Office.Install(new[] { Office.KeyOf(a) }, log, status, cancelled);
            if (!WingetOk)
            {
                if (a.FallbackUrl != null) return InstallDirect(a, log, status, cancelled);
                return InstallResult.NoWinget;
            }
            var ids = new List<string> { a.Id }; ids.AddRange(a.AltIds);
            InstallResult last = InstallResult.Failed;
            foreach (var id in ids)
            {
                status("Hazırlanıyor...", -1);
                string args = "install --id \"" + id + "\" -e --source " + a.Source + " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
                log("> winget " + args);
                int code = Run("winget", args, l =>
                {
                    if (IsNoise(l)) { var mm = sizeRx.Match(l); if (mm.Success) { double d = ToBytes(mm.Groups[1].Value, mm.Groups[2].Value), t = ToBytes(mm.Groups[3].Value, mm.Groups[4].Value); if (t > 0) status("İndiriliyor  " + mm.Groups[1].Value + " " + mm.Groups[2].Value + " / " + mm.Groups[3].Value + " " + mm.Groups[4].Value, (float)(d / t)); } return; }
                    var m = sizeRx.Match(l);
                    if (m.Success)
                    {
                        double d = ToBytes(m.Groups[1].Value, m.Groups[2].Value), t = ToBytes(m.Groups[3].Value, m.Groups[4].Value);
                        if (t > 0) status("İndiriliyor  " + m.Groups[1].Value + " " + m.Groups[2].Value + " / " + m.Groups[3].Value + " " + m.Groups[4].Value, (float)(d / t));
                        return;
                    }
                    string low = l.ToLowerInvariant();
                    if (low.Contains("download") || low.Contains("indiril")) status("İndiriliyor...", -1);
                    else if (low.Contains("install") || low.Contains("yükle") || low.Contains("kurul")) status("Kuruluyor...", -1);
                    else if (low.Contains("hash")) status("Doğrulanıyor...", -1);
                    log("  " + l.Trim());
                }, cancelled);
                if (code == int.MinValue) return InstallResult.Cancelled;
                log("  Çıkış kodu: " + code);
                if (code == 0) return InstallResult.Ok;
                if (code == 3010 || code == 1641 || code == -1978334967) return InstallResult.OkReboot;
                if (code == -1978335135 || code == -1978335189) return InstallResult.Already;
                if (code == -1978335212) { last = InstallResult.NotFound; continue; }
                last = InstallResult.Failed;
                break;
            }
            if (a.FallbackUrl != null && !cancelled())
            {
                log("  winget başarısız, doğrudan indirme deneniyor...");
                return InstallDirect(a, log, status, cancelled);
            }
            return last;
        }

        static InstallResult InstallDism(AppItem a, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            status("Windows özelliği etkinleştiriliyor (birkaç dakika sürebilir)...", -1);
            string dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe");
            int code = Run(dism, "/Online /Enable-Feature /FeatureName:" + a.Id + " /All /NoRestart", l =>
            {
                var m = Regex.Match(l, @"(\d+(\.\d+)?)%");
                if (m.Success) { double v; double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v); status("Etkinleştiriliyor %" + (int)v, (float)(v / 100)); }
                else if (!IsNoise(l)) log("  " + l.Trim());
            }, cancelled);
            if (code == int.MinValue) return InstallResult.Cancelled;
            if (code == 0) return InstallResult.Ok;
            if (code == 3010) return InstallResult.OkReboot;
            log("  DISM çıkış kodu: " + code); return InstallResult.Failed;
        }

        public static InstallResult InstallDirect(AppItem a, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ZETXFormatter"); Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Regex.Replace(a.Name, @"[^\w]", "") + a.FallbackExt);
            try
            {
                var dr = Download(a.FallbackUrl, file, log, status, cancelled);
                if (dr != InstallResult.Ok) return dr;
                status("Kuruluyor...", -1);
                int code = a.FallbackExt == ".msi"
                    ? Run("msiexec.exe", "/i \"" + file + "\" " + a.FallbackArgs + " /norestart", null, cancelled)
                    : Run(file, a.FallbackArgs, null, cancelled);
                try { File.Delete(file); } catch { }
                if (code == int.MinValue) return InstallResult.Cancelled;
                log("  Kurulum çıkış kodu: " + code);
                if (code == 0) return InstallResult.Ok;
                if (code == 3010 || code == 1641) return InstallResult.OkReboot;
                if (code == 1638) return InstallResult.Already;
                return InstallResult.Failed;
            }
            catch (Exception ex) { log("  Hata: " + ex.Message); return InstallResult.Failed; }
        }

        public static InstallResult Download(string url, string file, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            log("  İndiriliyor: " + url);
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
                    var done = new ManualResetEvent(false); Exception err = null; bool canc = false;
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        if (e.TotalBytesToReceive > 0) status(string.Format("İndiriliyor  {0:0.0} MB / {1:0.0} MB", e.BytesReceived / 1e6, e.TotalBytesToReceive / 1e6), (float)e.BytesReceived / e.TotalBytesToReceive);
                        else status(string.Format("İndiriliyor  {0:0.0} MB", e.BytesReceived / 1e6), -1);
                    };
                    wc.DownloadFileCompleted += (s, e) => { if (e.Cancelled) canc = true; else if (e.Error != null) err = e.Error; done.Set(); };
                    wc.DownloadFileAsync(new Uri(url), file);
                    while (!done.WaitOne(250)) if (cancelled()) { wc.CancelAsync(); done.WaitOne(3000); return InstallResult.Cancelled; }
                    if (canc) return InstallResult.Cancelled;
                    if (err != null) { log("  İndirme hatası: " + err.Message); return InstallResult.Failed; }
                }
                return InstallResult.Ok;
            }
            catch (Exception ex) { log("  İndirme hatası: " + ex.Message); return InstallResult.Failed; }
        }

        public static void KillCurrent() { lock (plock) { try { if (current != null && !current.HasExited) current.Kill(); } catch { } } }

        public static bool BootstrapWinget(Action<string> log)
        {
            string ps = @"$ErrorActionPreference='Continue'; $ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Output 'Mevcut App Installer kaydı deneniyor...'
try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction Stop; Write-Output 'Kayıt tamam.' } catch { Write-Output 'Kayıtlı paket yok, indirilecek.' }
if (Get-Command winget -ErrorAction SilentlyContinue) { Write-Output 'winget hazır.'; exit 0 }
$t = Join-Path $env:TEMP 'zetx_winget'; New-Item -ItemType Directory -Force -Path $t | Out-Null
Write-Output 'VCLibs indiriliyor...'; Invoke-WebRequest 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx' -OutFile ""$t\vclibs.appx"" -UseBasicParsing
Write-Output 'UI.Xaml indiriliyor...'; Invoke-WebRequest 'https://github.com/microsoft/microsoft-ui-xaml/releases/download/v2.8.6/Microsoft.UI.Xaml.2.8.x64.appx' -OutFile ""$t\xaml.appx"" -UseBasicParsing
Write-Output 'winget (App Installer) indiriliyor...'; Invoke-WebRequest 'https://aka.ms/getwinget' -OutFile ""$t\winget.msixbundle"" -UseBasicParsing
Write-Output 'Kuruluyor...'
try { Add-AppxPackage ""$t\vclibs.appx"" -ErrorAction SilentlyContinue } catch {}
try { Add-AppxPackage ""$t\xaml.appx"" -ErrorAction SilentlyContinue } catch {}
Add-AppxPackage ""$t\winget.msixbundle""
Write-Output 'Tamamlandı.'";
            string f = Path.Combine(Path.GetTempPath(), "zetx_winget.ps1");
            File.WriteAllText(f, ps, new UTF8Encoding(true));
            Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + f + "\"", l => { if (!string.IsNullOrWhiteSpace(l)) log(l); }, null, 900000);
            // Refresh PATH for WindowsApps alias
            string wa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (path.IndexOf(wa, StringComparison.OrdinalIgnoreCase) < 0) Environment.SetEnvironmentVariable("PATH", path + ";" + wa);
            return DetectWinget();
        }

        // ---------- Icons ----------
        static readonly Queue<KeyValuePair<AppItem, Action>> iconQ = new Queue<KeyValuePair<AppItem, Action>>();
        static bool iconWorkers;
        public static void LoadIcon(AppItem a, Action done)
        {
            lock (iconQ)
            {
                iconQ.Enqueue(new KeyValuePair<AppItem, Action>(a, done));
                if (!iconWorkers)
                {
                    iconWorkers = true;
                    for (int i = 0; i < 4; i++) new Thread(IconWorker) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
                }
            }
        }
        static int iconFails;
        static void IconWorker()
        {
            while (true)
            {
                KeyValuePair<AppItem, Action> job;
                lock (iconQ) { if (iconQ.Count == 0) return; job = iconQ.Dequeue(); }
                try { LoadIconNow(job.Key, job.Value); } catch { }
            }
        }
        static void LoadIconNow(AppItem a, Action done)
        {
            string dir = Path.Combine(DataDir, "icons"); Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, Regex.Replace(a.Domain, @"[^\w\.]", "_") + ".png");
            byte[] data = null;
            if (File.Exists(f) && new FileInfo(f).Length > 100) data = File.ReadAllBytes(f);
            else
            {
                if (iconFails > 6) return; // internet yok: denemeyi bırak
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create("https://www.google.com/s2/favicons?domain=" + Uri.EscapeDataString(a.Domain) + "&sz=64");
                    req.Timeout = 8000; req.ReadWriteTimeout = 8000; req.UserAgent = "Mozilla/5.0 ZETXFormatter";
                    using (var resp = req.GetResponse()) using (var st = resp.GetResponseStream()) using (var ms = new MemoryStream()) { st.CopyTo(ms); data = ms.ToArray(); }
                }
                catch { Interlocked.Increment(ref iconFails); return; }
                if (data != null && data.Length > 100) File.WriteAllBytes(f, data);
            }
            if (data != null && data.Length > 100)
            {
                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms))
                    if (img.Width >= 32) { a.Icon = new Bitmap(img); if (done != null) done(); }
            }
        }

        // ---------- Profiles ----------
        public static void SaveProfile(string path, IEnumerable<string> ids)
        {
            var sb = new StringBuilder("# ZETX FORMATTER profil dosyası\r\n");
            foreach (var i in ids) sb.Append(i).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        public static List<string> LoadProfile(string path)
        {
            var l = new List<string>();
            foreach (var line in File.ReadAllLines(path)) { var t = line.Trim(); if (t.Length > 0 && !t.StartsWith("#")) l.Add(t); }
            return l;
        }

        // ---------- System info ----------
        public static string Wmi(string cls, string prop)
        {
            try
            {
                var list = new List<string>();
                using (var s = new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
                    foreach (ManagementObject o in s.Get()) { var v = o[prop]; if (v != null) list.Add(v.ToString().Trim()); }
                return string.Join("  •  ", list.ToArray());
            }
            catch { return "-"; }
        }
    }

    public class Tweak
    {
        public string Name, Desc; public Func<bool> IsOn; public Action Apply; public bool OneShot;
        static void SetReg(RegistryKey root, string path, string name, object val, RegistryValueKind kind)
        {
            using (var k = root.CreateSubKey(path)) k.SetValue(name, val, kind);
        }
        static object GetReg(RegistryKey root, string path, string name)
        {
            try { using (var k = root.OpenSubKey(path)) return k == null ? null : k.GetValue(name); } catch { return null; }
        }
        const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string Pers = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static List<Tweak> All()
        {
            var l = new List<Tweak>();
            l.Add(new Tweak { Name = "Dosya uzantılarını göster", Desc = "Dosya adlarında .exe, .jpg gibi uzantılar görünür", IsOn = () => Equals(GetReg(Registry.CurrentUser, Adv, "HideFileExt"), 0), Apply = () => SetReg(Registry.CurrentUser, Adv, "HideFileExt", 0, RegistryValueKind.DWord) });
            l.Add(new Tweak { Name = "Gizli dosyaları göster", Desc = "Gizli klasörler ve dosyalar Gezgin'de görünür", IsOn = () => Equals(GetReg(Registry.CurrentUser, Adv, "Hidden"), 1), Apply = () => SetReg(Registry.CurrentUser, Adv, "Hidden", 1, RegistryValueKind.DWord) });
            l.Add(new Tweak { Name = "Koyu tema", Desc = "Windows ve uygulamalar koyu moda geçer", IsOn = () => Equals(GetReg(Registry.CurrentUser, Pers, "AppsUseLightTheme"), 0), Apply = () => { SetReg(Registry.CurrentUser, Pers, "AppsUseLightTheme", 0, RegistryValueKind.DWord); SetReg(Registry.CurrentUser, Pers, "SystemUsesLightTheme", 0, RegistryValueKind.DWord); } });
            l.Add(new Tweak { Name = "Oyun Modu", Desc = "Windows Oyun Modu açılır (oyunlara öncelik)", IsOn = () => Equals(GetReg(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled"), 1), Apply = () => SetReg(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord) });
            l.Add(new Tweak { Name = "Fare ivmesini kapat", Desc = "Oyunlarda daha tutarlı nişan (Enhance Pointer Precision kapalı)", IsOn = () => Equals(GetReg(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed"), "0"), Apply = () => { SetReg(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String); SetReg(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String); SetReg(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String); } });
            l.Add(new Tweak { Name = "Başlat menüsünde web aramasını kapat", Desc = "Aramada Bing sonuçları yerine sadece PC sonuçları", IsOn = () => Equals(GetReg(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"), 1), Apply = () => SetReg(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord) });
            l.Add(new Tweak { Name = "Başlangıç gecikmesini kaldır", Desc = "Açılışta başlangıç programları beklemeden çalışır", IsOn = () => Equals(GetReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec"), 0), Apply = () => SetReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, RegistryValueKind.DWord) });
            l.Add(new Tweak
            {
                Name = "Nihai Performans güç planı", Desc = "En yüksek performans güç planını ekler ve etkinleştirir", OneShot = true,
                IsOn = () => { var o = Engine.Capture("powercfg", "/getactivescheme", 10000) ?? ""; return o.Contains("e9a42b02") || o.ToLowerInvariant().Contains("ultimate") || o.ToLowerInvariant().Contains("nihai"); },
                Apply = () =>
                {
                    var o = Engine.Capture("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 15000) ?? "";
                    var m = System.Text.RegularExpressions.Regex.Match(o, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    Engine.Capture("powercfg", "/setactive " + (m.Success ? m.Value : "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"), 15000);
                }
            });
            return l;
        }
    }
}
