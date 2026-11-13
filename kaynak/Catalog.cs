using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZetxFormatter
{
    public class AppItem
    {
        public string Name, Id, Desc, Domain, Category, Source = "winget";
        public string[] AltIds = new string[0];
        public string FallbackUrl, FallbackArgs, FallbackExt = ".exe";
        public bool Popular;
        public string CustomKind, CustomSrc, SilentMode = "auto", CustomArgs = "";
        // runtime
        public bool Selected, Installed;
        public Image Icon;
    }

    public class Preset
    {
        public string Name, Glyph; public string[] Ids;
        public Preset(string n, string g, params string[] ids) { Name = n; Glyph = g; Ids = ids; }
    }

    public static class Catalog
    {
        public static readonly string[] Categories = {
            "Benim Eklediklerim", "Tarayıcılar", "İletişim", "Oyun Platformları", "Oyun & Donanım Araçları", "Multimedya",
            "Yayın & Tasarım", "Araçlar", "Ofis & PDF", "Bulut", "Geliştirici", "Güvenlik", "Sistem Bileşenleri" };

        public static Color CatColor(string c)
        {
            switch (c)
            {
                case "Benim Eklediklerim": return Color.FromArgb(255, 145, 40);
                case "Tarayıcılar": return Color.FromArgb(61, 139, 255);
                case "İletişim": return Color.FromArgb(114, 137, 218);
                case "Oyun Platformları": return Color.FromArgb(255, 106, 26);
                case "Oyun & Donanım Araçları": return Color.FromArgb(255, 77, 94);
                case "Multimedya": return Color.FromArgb(46, 204, 113);
                case "Yayın & Tasarım": return Color.FromArgb(186, 104, 255);
                case "Araçlar": return Color.FromArgb(245, 184, 61);
                case "Ofis & PDF": return Color.FromArgb(0, 188, 212);
                case "Bulut": return Color.FromArgb(100, 181, 246);
                case "Geliştirici": return Color.FromArgb(0, 200, 160);
                case "Güvenlik": return Color.FromArgb(240, 98, 146);
                default: return Color.FromArgb(158, 168, 190);
            }
        }

        public static List<AppItem> All = new List<AppItem>();
        static string cur;
        static AppItem A(string name, string id, string desc, string domain, bool pop = false)
        {
            var a = new AppItem { Name = name, Id = id, Desc = desc, Domain = domain, Category = cur, Popular = pop };
            All.Add(a); return a;
        }
        static AppItem Off(string name, string key, string desc, string domain, bool pop = false)
        {
            var a = A(name, "office:" + key, desc, domain, pop); a.Source = "office"; return a;
        }
        static AppItem Store(string name, string id, string desc, string domain, bool pop = false)
        {
            var a = A(name, id, desc, domain, pop); a.Source = "msstore"; return a;
        }

        static Catalog()
        {
            cur = "Tarayıcılar";
            var chrome = A("Google Chrome", "Google.Chrome", "Hızlı ve en popüler web tarayıcısı", "chrome.com", true);
            chrome.FallbackUrl = "https://dl.google.com/chrome/install/latest/chrome_installer.exe"; chrome.FallbackArgs = "/silent /install";
            var ff = A("Mozilla Firefox", "Mozilla.Firefox", "Gizlilik odaklı açık kaynak tarayıcı", "firefox.com", true);
            ff.FallbackUrl = "https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=tr"; ff.FallbackArgs = "-ms";
            A("Brave", "Brave.Brave", "Reklam engelleyicili hızlı tarayıcı", "brave.com");
            A("Opera", "Opera.Opera", "Dahili VPN ve reklam engelleyici", "opera.com");
            A("Opera GX", "Opera.OperaGX", "Oyunculara özel tarayıcı (RAM/CPU limit)", "opera.com", true);
            A("Vivaldi", "Vivaldi.Vivaldi", "Tamamen özelleştirilebilir tarayıcı", "vivaldi.com");
            A("Tor Browser", "TorProject.TorBrowser", "Anonim internet gezintisi", "torproject.org");

            cur = "İletişim";
            var dc = A("Discord", "Discord.Discord", "Sesli/görüntülü sohbet ve sunucular", "discord.com", true);
            dc.FallbackUrl = "https://discord.com/api/downloads/distributions/app/installers/latest?channel=stable&platform=win&arch=x64"; dc.FallbackArgs = "-s";
            A("Telegram", "Telegram.TelegramDesktop", "Hızlı ve güvenli mesajlaşma", "telegram.org", true);
            Store("WhatsApp", "9NKSQGP7F2NH", "WhatsApp masaüstü uygulaması", "whatsapp.com", true);
            A("Zoom", "Zoom.Zoom", "Video konferans ve toplantı", "zoom.us");
            A("Microsoft Teams", "Microsoft.Teams", "İş ve okul toplantıları", "teams.microsoft.com");
            A("TeamSpeak 3", "TeamSpeakSystems.TeamSpeakClient", "Oyuncular için düşük gecikmeli ses", "teamspeak.com");
            A("Slack", "SlackTechnologies.Slack", "Ekip içi mesajlaşma", "slack.com");
            A("Signal", "OpenWhisperSystems.Signal", "Uçtan uca şifreli mesajlaşma", "signal.org");

            cur = "Oyun Platformları";
            var steam = A("Steam", "Valve.Steam", "En büyük PC oyun platformu", "steampowered.com", true);
            steam.FallbackUrl = "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe"; steam.FallbackArgs = "/S";
            var epic = A("Epic Games", "EpicGames.EpicGamesLauncher", "Epic Games Store ve ücretsiz oyunlar", "epicgames.com", true);
            epic.FallbackUrl = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/installer/download/EpicGamesLauncherInstaller.msi"; epic.FallbackExt = ".msi"; epic.FallbackArgs = "/qn";
            A("EA App", "ElectronicArts.EADesktop", "Battlefield, FC ve EA oyunları", "ea.com", true);
            A("Ubisoft Connect", "Ubisoft.Connect", "Ubisoft oyun platformu", "ubisoft.com");
            A("GOG Galaxy", "GOG.Galaxy", "DRM'siz oyunlar ve kütüphane birleştirme", "gog.com");
            A("Battle.net", "Blizzard.BattleNet", "Blizzard ve Call of Duty oyunları", "battle.net");
            Store("Xbox", "9MV0B5HZVK9Z", "Game Pass ve Xbox PC oyunları", "xbox.com");
            A("Playnite", "Playnite.Playnite", "Tüm oyun kütüphaneleri tek yerde", "playnite.link");

            cur = "Oyun & Donanım Araçları";
            A("MSI Afterburner", "Guru3D.Afterburner", "Ekran kartı overclock ve FPS takibi", "msi.com", true);
            A("RivaTuner (RTSS)", "Guru3D.RTSS", "Oyun içi FPS göstergesi ve limitleyici", "guru3d.com");
            A("CPU-Z", "CPUID.CPU-Z", "İşlemci, RAM ve anakart bilgisi", "cpuid.com");
            A("GPU-Z", "TechPowerUp.GPU-Z", "Ekran kartı detaylı bilgisi", "techpowerup.com");
            A("HWiNFO", "REALiX.HWiNFO", "Tüm donanım sensörleri ve sıcaklıklar", "hwinfo.com");
            A("CrystalDiskInfo", "CrystalDewWorld.CrystalDiskInfo", "Disk sağlığı ve sıcaklığı", "crystalmark.info");
            A("CrystalDiskMark", "CrystalDewWorld.CrystalDiskMark", "Disk hız testi", "crystalmark.info");
            A("Process Lasso", "BitSum.ProcessLasso", "İşlemci önceliği ve performans optimizasyonu", "bitsum.com");

            cur = "Multimedya";
            A("VLC Media Player", "VideoLAN.VLC", "Her formatı oynatan medya oynatıcı", "videolan.org", true);
            Store("Spotify", "9NCBCSZSJRSB", "Müzik ve podcast", "spotify.com", true);
            A("K-Lite Codec Pack", "CodecGuide.K-LiteCodecPack.Standard", "Tüm video/ses codec'leri", "codecguide.com");
            A("PotPlayer", "Daum.PotPlayer", "Hafif ve güçlü video oynatıcı", "potplayer.tv");
            A("MPC-HC", "clsid2.mpc-hc", "Klasik hafif medya oynatıcı", "mpc-hc.org");
            A("foobar2000", "PeterPawlowski.foobar2000", "Gelişmiş müzik oynatıcı", "foobar2000.org");
            A("Audacity", "Audacity.Audacity", "Ses kaydı ve düzenleme", "audacityteam.org");
            A("iTunes", "Apple.iTunes", "iPhone yedekleme ve müzik", "apple.com");

            cur = "Yayın & Tasarım";
            A("OBS Studio", "OBSProject.OBSStudio", "Yayın ve ekran kaydı", "obsproject.com", true);
            A("Streamlabs", "Streamlabs.Streamlabs", "Uyarılar ve temalar ile canlı yayın", "streamlabs.com");
            A("ShareX", "ShareX.ShareX", "Ekran görüntüsü ve GIF kaydı", "getsharex.com");
            A("HandBrake", "HandBrake.HandBrake", "Video dönüştürme ve sıkıştırma", "handbrake.fr");
            var gimp = A("GIMP", "GIMP.GIMP.3", "Ücretsiz fotoğraf düzenleme", "gimp.org"); gimp.AltIds = new[] { "GIMP.GIMP" };
            A("Paint.NET", "dotPDN.PaintDotNet", "Basit ve hızlı resim düzenleme", "getpaint.net");
            A("Krita", "KDE.Krita", "Dijital çizim ve illüstrasyon", "krita.org");
            A("Kdenlive", "KDE.Kdenlive", "Ücretsiz video kurgu", "kdenlive.org");
            A("Blender", "BlenderFoundation.Blender", "3D modelleme ve animasyon", "blender.org");
            A("CapCut", "ByteDance.CapCut", "Kolay video düzenleme (Reels/Shorts)", "capcut.com");

            cur = "Araçlar";
            var z7 = A("7-Zip", "7zip.7zip", "RAR/ZIP/7Z arşiv açma", "7-zip.org", true);
            z7.FallbackUrl = "https://www.7-zip.org/a/7z2409-x64.exe"; z7.FallbackArgs = "/S";
            A("WinRAR", "RARLab.WinRAR", "Popüler arşiv yöneticisi", "win-rar.com");
            A("Notepad++", "Notepad++.Notepad++", "Gelişmiş metin editörü", "notepad-plus-plus.org", true);
            A("Everything", "voidtools.Everything", "Saniyeler içinde dosya arama", "voidtools.com");
            A("PowerToys", "Microsoft.PowerToys", "Windows için güçlü ek araçlar", "learn.microsoft.com");
            A("AnyDesk", "AnyDesk.AnyDesk", "Uzak masaüstü bağlantısı", "anydesk.com", true);
            A("TeamViewer", "TeamViewer.TeamViewer", "Uzaktan destek ve erişim", "teamviewer.com");
            A("qBittorrent", "qBittorrent.qBittorrent", "Reklamsız torrent istemcisi", "qbittorrent.org");
            A("Internet Download Manager", "Tonec.InternetDownloadManager", "Hızlandırılmış dosya indirme", "internetdownloadmanager.com");
            A("Rufus", "Rufus.Rufus", "Önyüklenebilir USB oluşturma", "rufus.ie");
            A("Revo Uninstaller", "RevoUninstaller.RevoUninstaller", "Artık bırakmadan program kaldırma", "revouninstaller.com");
            A("WinDirStat", "WinDirStat.WinDirStat", "Diskte yer kaplayanları bul", "windirstat.net");
            A("Lightshot", "Skillbrains.Lightshot", "Hızlı ekran görüntüsü", "prnt.sc");

            cur = "Ofis & PDF";
            Off("Microsoft Word", "Word", "Microsoft 365 — kelime işlemci", "word.cloud.microsoft", true);
            Off("Microsoft Excel", "Excel", "Microsoft 365 — tablo ve hesaplama", "excel.cloud.microsoft", true);
            Off("Microsoft PowerPoint", "PowerPoint", "Microsoft 365 — sunum hazırlama", "powerpoint.cloud.microsoft", true);
            Off("Microsoft Outlook", "Outlook", "Microsoft 365 — e-posta ve takvim (klasik)", "outlook.com");
            Off("Microsoft OneNote", "OneNote", "Microsoft 365 — dijital not defteri", "onenote.com");
            Off("Microsoft Access", "Access", "Microsoft 365 — veritabanı", "microsoft.com");
            A("LibreOffice", "TheDocumentFoundation.LibreOffice", "Ücretsiz ofis paketi (Word/Excel uyumlu)", "libreoffice.org", true);
            A("ONLYOFFICE", "ONLYOFFICE.DesktopEditors", "MS Office benzeri ücretsiz ofis", "onlyoffice.com");
            A("Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit", "PDF görüntüleme ve imzalama", "adobe.com", true);
            A("SumatraPDF", "SumatraPDF.SumatraPDF", "Çok hafif PDF okuyucu", "sumatrapdfreader.org");
            A("Notion", "Notion.Notion", "Notlar, görevler ve wiki", "notion.so");
            A("Obsidian", "Obsidian.Obsidian", "Markdown not uygulaması", "obsidian.md");

            cur = "Bulut";
            A("Google Drive", "Google.GoogleDrive", "Drive dosyalarını PC ile eşitle", "drive.google.com", true);
            A("Dropbox", "Dropbox.Dropbox", "Bulut depolama ve eşitleme", "dropbox.com");
            A("MEGA", "Mega.MEGASync", "20 GB ücretsiz bulut depolama", "mega.io");
            Store("iCloud", "9PKTQ5699M62", "iPhone fotoğraf ve dosya eşitleme", "icloud.com");

            cur = "Geliştirici";
            A("Visual Studio Code", "Microsoft.VisualStudioCode", "Popüler kod editörü", "code.visualstudio.com", true);
            A("Git", "Git.Git", "Sürüm kontrol sistemi", "git-scm.com");
            A("GitHub Desktop", "GitHub.GitHubDesktop", "GitHub için görsel arayüz", "github.com");
            A("Python 3.13", "Python.Python.3.13", "Python programlama dili", "python.org");
            A("Node.js LTS", "OpenJS.NodeJS.LTS", "JavaScript çalışma ortamı", "nodejs.org");
            A("Windows Terminal", "Microsoft.WindowsTerminal", "Modern komut satırı", "github.com/microsoft/terminal");
            A("Java (Temurin 21)", "EclipseAdoptium.Temurin.21.JRE", "Java uygulamaları ve Minecraft için", "adoptium.net");
            A("Postman", "Postman.Postman", "API test aracı", "postman.com");

            cur = "Güvenlik";
            A("Malwarebytes", "Malwarebytes.Malwarebytes", "Kötü amaçlı yazılım temizleyici", "malwarebytes.com", true);
            A("Bitwarden", "Bitwarden.Bitwarden", "Ücretsiz şifre yöneticisi", "bitwarden.com");
            A("KeePassXC", "KeePassXCTeam.KeePassXC", "Çevrimdışı şifre kasası", "keepassxc.org");
            A("Proton VPN", "Proton.ProtonVPN", "Ücretsiz ve güvenli VPN", "protonvpn.com");
            A("Cloudflare WARP", "Cloudflare.Warp", "Hızlı DNS ve güvenli bağlantı", "one.one.one.one");

            cur = "Sistem Bileşenleri";
            var vc64 = A("Visual C++ 2015-2022 x64", "Microsoft.VCRedist.2015+.x64", "Oyun ve programlar için zorunlu", "visualstudio.microsoft.com", true);
            vc64.FallbackUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"; vc64.FallbackArgs = "/install /quiet /norestart";
            var vc86 = A("Visual C++ 2015-2022 x86", "Microsoft.VCRedist.2015+.x86", "32-bit programlar için", "visualstudio.microsoft.com", true);
            vc86.FallbackUrl = "https://aka.ms/vs/17/release/vc_redist.x86.exe"; vc86.FallbackArgs = "/install /quiet /norestart";
            A("Visual C++ 2013 x64", "Microsoft.VCRedist.2013.x64", "Eski oyunlar için", "visualstudio.microsoft.com");
            A("Visual C++ 2012 x64", "Microsoft.VCRedist.2012.x64", "Eski oyunlar için", "visualstudio.microsoft.com");
            A("Visual C++ 2010 x64", "Microsoft.VCRedist.2010.x64", "Eski oyunlar için", "visualstudio.microsoft.com");
            A("Visual C++ 2008 x64", "Microsoft.VCRedist.2008.x64", "Eski oyunlar için", "visualstudio.microsoft.com");
            A("DirectX Runtime", "Microsoft.DirectX", "DirectX 9-11 oyun bileşenleri", "microsoft.com", true);
            A(".NET Desktop Runtime 8", "Microsoft.DotNet.DesktopRuntime.8", "Modern .NET programları için", "dotnet.microsoft.com", true);
            var nf35 = A(".NET Framework 3.5", "NetFx3", "Eski oyun ve programlar için (Windows özelliği)", "dotnet.microsoft.com");
            nf35.Source = "dism";
            A("WebView2 Runtime", "Microsoft.EdgeWebView2Runtime", "Birçok modern uygulama için gerekli", "microsoft.com");
            A("XNA Framework", "Microsoft.XNARedist", "Terraria vb. XNA oyunları için", "microsoft.com");
            A("OpenAL", "OpenAL.OpenAL", "Oyunlarda 3D ses desteği", "openal.org");
        }

        public static readonly Preset[] Presets = {
            new Preset("Temel Paket", "", "Google.Chrome", "7zip.7zip", "VideoLAN.VLC", "Notepad++.Notepad++", "Adobe.Acrobat.Reader.64-bit", "Microsoft.VCRedist.2015+.x64", "Microsoft.VCRedist.2015+.x86", "Microsoft.DirectX", "Microsoft.DotNet.DesktopRuntime.8", "AnyDesk.AnyDesk"),
            new Preset("Oyuncu Paketi", "", "Valve.Steam", "EpicGames.EpicGamesLauncher", "ElectronicArts.EADesktop", "Ubisoft.Connect", "Discord.Discord", "Opera.OperaGX", "Guru3D.Afterburner", "Guru3D.RTSS", "CPUID.CPU-Z", "TechPowerUp.GPU-Z", "REALiX.HWiNFO", "Microsoft.VCRedist.2015+.x64", "Microsoft.VCRedist.2015+.x86", "Microsoft.VCRedist.2013.x64", "Microsoft.VCRedist.2012.x64", "Microsoft.VCRedist.2010.x64", "Microsoft.DirectX", "7zip.7zip"),
            new Preset("Yayıncı Paketi", "", "OBSProject.OBSStudio", "Streamlabs.Streamlabs", "Discord.Discord", "ShareX.ShareX", "Audacity.Audacity", "VideoLAN.VLC", "HandBrake.HandBrake", "9NCBCSZSJRSB", "ByteDance.CapCut", "Google.Chrome"),
            new Preset("Ofis Paketi", "", "Google.Chrome", "office:Word", "office:Excel", "office:PowerPoint", "office:Outlook", "Adobe.Acrobat.Reader.64-bit", "Zoom.Zoom", "Microsoft.Teams", "Google.GoogleDrive", "7zip.7zip", "AnyDesk.AnyDesk", "9NKSQGP7F2NH"),
            new Preset("Geliştirici Paketi", "", "Microsoft.VisualStudioCode", "Git.Git", "GitHub.GitHubDesktop", "Python.Python.3.13", "OpenJS.NodeJS.LTS", "Microsoft.WindowsTerminal", "Notepad++.Notepad++", "Microsoft.PowerToys", "Postman.Postman", "Google.Chrome"),
        };

        public static AppItem Find(string id)
        {
            foreach (var a in All) if (string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }
    }
}
