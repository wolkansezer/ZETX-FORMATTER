using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("ZETX FORMATTER")]
[assembly: AssemblyProduct("ZETX FORMATTER")]
[assembly: AssemblyDescription("Format sonrası program kurulum aracı")]
[assembly: AssemblyCompany("Volkan Sezer - ZETX")]
[assembly: AssemblyCopyright("© 2026 Volkan Sezer")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace ZetxFormatter
{
    static class Program
    {
        public static bool IsAdmin;
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            bool win = Environment.OSVersion.Platform == PlatformID.Win32NT;
            try { IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { IsAdmin = false; }
            if (win && !IsAdmin && Array.IndexOf(args, "--noadmin") < 0)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--elevated") { Verb = "runas", UseShellExecute = true });
                    return;
                }
                catch { /* kullanıcı UAC'yi reddetti: normal modda devam */ }
            }
            bool created;
            using (var mx = new Mutex(true, "ZETX_FORMATTER_SINGLE", out created))
            {
                if (!created) { MessageBox.Show("ZETX FORMATTER zaten açık.", "ZETX FORMATTER"); return; }
                try { if (win) SetProcessDPIAware(); } catch { }
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; } catch { try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { } }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => { Console.Error.WriteLine(e.Exception.ToString()); MessageBox.Show("Beklenmeyen hata: " + e.Exception.Message, "ZETX FORMATTER", MessageBoxButtons.OK, MessageBoxIcon.Error); };
                Theme.Init();
                Application.Run(new MainForm());
            }
        }
    }
}
