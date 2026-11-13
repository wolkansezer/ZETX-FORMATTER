using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZetxFormatter
{
    /// Microsoft 365 apps via the official Office Deployment Tool
    public static class Office
    {
        public const string OdtUrl = "https://officecdn.microsoft.com/pr/wsus/setup.exe";
        // app key -> (ExcludeApp id, exe name)
        static readonly Dictionary<string, string> Exe = new Dictionary<string, string>
        {
            { "Word", "WINWORD.EXE" }, { "Excel", "EXCEL.EXE" }, { "PowerPoint", "POWERPNT.EXE" },
            { "Outlook", "OUTLOOK.EXE" }, { "OneNote", "ONENOTE.EXE" }, { "Access", "MSACCESS.EXE" }
        };
        public static IEnumerable<string> Keys { get { return Exe.Keys; } }
        public static string KeyOf(AppItem a) { return a.Id.Substring("office:".Length); }

        public static bool IsInstalled(string key)
        {
            string exe;
            if (!Exe.TryGetValue(key, out exe)) return false;
            foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                if (File.Exists(Path.Combine(pf, @"Microsoft Office\root\Office16", exe))) return true;
                if (File.Exists(Path.Combine(pf, @"Microsoft Office\Office16", exe))) return true;
            }
            return false;
        }

        public static string BuildConfig(IEnumerable<string> keep)
        {
            var k = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.AppendLine("<Configuration>");
            sb.AppendLine("  <Add OfficeClientEdition=\"64\" Channel=\"Current\">");
            sb.AppendLine("    <Product ID=\"O365ProPlusRetail\">");
            sb.AppendLine("      <Language ID=\"MatchOS\" Fallback=\"tr-tr\" />");
            foreach (var app in Exe.Keys) if (!k.Contains(app)) sb.AppendLine("      <ExcludeApp ID=\"" + app + "\" />");
            foreach (var x in new[] { "Groove", "Lync", "Teams", "Bing", "OutlookForWindows", "Publisher" }) sb.AppendLine("      <ExcludeApp ID=\"" + x + "\" />");
            sb.AppendLine("    </Product>");
            sb.AppendLine("  </Add>");
            sb.AppendLine("  <Property Name=\"FORCEAPPSHUTDOWN\" Value=\"TRUE\" />");
            sb.AppendLine("  <Updates Enabled=\"TRUE\" />");
            sb.AppendLine("  <Display Level=\"None\" AcceptEULA=\"TRUE\" />");
            sb.AppendLine("</Configuration>");
            return sb.ToString();
        }

        /// Installs the given Office apps. Already installed Office apps are kept in the configuration so they are not removed.
        public static InstallResult Install(IEnumerable<string> wanted, Action<string> log, Action<string, float> status, Func<bool> cancelled)
        {
            var keep = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);
            foreach (var k in Exe.Keys) if (IsInstalled(k)) keep.Add(k);
            string dir = Path.Combine(Path.GetTempPath(), "ZETXFormatter", "office"); Directory.CreateDirectory(dir);
            string setup = Path.Combine(dir, "setup.exe"), cfg = Path.Combine(dir, "zetx_office.xml");
            log("  Office uygulamaları: " + string.Join(", ", keep.ToArray()));
            status("Office Dağıtım Aracı indiriliyor...", -1);
            var r = Engine.Download(OdtUrl, setup, log, status, cancelled);
            if (r != InstallResult.Ok) return r;
            File.WriteAllText(cfg, BuildConfig(keep), new UTF8Encoding(false));
            status("Office indiriliyor ve kuruluyor (internet hızına göre 10-30 dk)...", -1);
            log("  Microsoft sunucularından Office indiriliyor ve kuruluyor. Bu adım uzun sürebilir, lütfen bekle.");
            int code = Engine.Run(setup, "/configure \"" + cfg + "\"", l => { if (!string.IsNullOrWhiteSpace(l)) log("  " + l.Trim()); }, cancelled);
            if (code == int.MinValue) return InstallResult.Cancelled;
            log("  Office kurulum çıkış kodu: " + code);
            bool ok = wanted.All(IsInstalled);
            if (code == 0 || ok)
            {
                log("  Office kuruldu. Word/Excel'i ilk açışta Microsoft hesabınla oturum açarak etkinleştir (Microsoft 365 aboneliği gerekir).");
                return InstallResult.Ok;
            }
            if (code == 3010) return InstallResult.OkReboot;
            log("  İpucu: Başka bir Office sürümü (ör. 2016/2019 MSI) kuruluysa önce onu kaldırman gerekebilir.");
            return InstallResult.Failed;
        }
    }
}
