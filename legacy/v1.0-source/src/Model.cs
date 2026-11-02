using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HamStartup
{
    public enum LogKind
    {
        Info,
        Step,
        Success,
        Warn,
        Error
    }

    /// <summary>One application in the launch chain.</summary>
    public class AppEntry
    {
        public string Id = "";
        public string Name = "";
        public string ExePath = "";
        public string Arguments = "";
        public bool Enabled = true;
        public bool WaitForWindow = true;
        public int WaitSeconds = 30;
        public int DelaySeconds = 3;
        public bool RunAsAdmin = false;

        private ShortcutInfo _shortcut;
        private string _shortcutFor = "";

        public bool IsShortcut
        {
            get
            {
                if (string.IsNullOrEmpty(ExePath)) return false;
                try
                {
                    return string.Equals(Path.GetExtension(ExePath), ".lnk", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }
        }

        public ShortcutInfo Shortcut
        {
            get
            {
                if (!IsShortcut) return null;
                if (_shortcut == null || !string.Equals(_shortcutFor, ExePath, StringComparison.OrdinalIgnoreCase))
                {
                    _shortcut = ShortcutResolver.Resolve(ExePath);
                    _shortcutFor = ExePath;
                }
                return _shortcut;
            }
        }

        public string TargetPath
        {
            get
            {
                if (!IsShortcut) return ExePath;
                ShortcutInfo info = Shortcut;
                return info != null && info.Valid ? info.TargetPath : "";
            }
        }

        public bool PathExists
        {
            get
            {
                if (string.IsNullOrEmpty(ExePath)) return false;
                if (!IsShortcut) return File.Exists(ExePath);
                ShortcutInfo info = Shortcut;
                return info != null && info.Valid && File.Exists(info.TargetPath);
            }
        }

        public string ProcessName
        {
            get
            {
                string path = IsShortcut ? TargetPath : ExePath;
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string n = Path.GetFileNameWithoutExtension(path);
                        if (!string.IsNullOrEmpty(n)) return n;
                    }
                    catch { }
                }
                return AppCatalog.KnownProcessName(Id);
            }
        }

        public string Directory
        {
            get
            {
                try
                {
                    string path = IsShortcut ? TargetPath : ExePath;
                    if (!string.IsNullOrEmpty(path)) return Path.GetDirectoryName(path);
                }
                catch { }
                return "";
            }
        }

        /// <summary>Start-in directory. For shortcuts this preserves the .lnk "起始位置".</summary>
        public string WorkingDirectory
        {
            get
            {
                if (IsShortcut)
                {
                    ShortcutInfo info = Shortcut;
                    if (info != null && info.Valid && !string.IsNullOrEmpty(info.WorkingDirectory))
                        return info.WorkingDirectory;
                }
                return Directory;
            }
        }
    }

    public static class AppCatalog
    {
        public static List<AppEntry> CreateDefaults()
        {
            List<AppEntry> list = new List<AppEntry>();

            AppEntry bkt = new AppEntry();
            bkt.Id = "bkt";
            bkt.Name = "BktTimeSync";
            bkt.WaitSeconds = 20;
            bkt.DelaySeconds = 3;
            bkt.RunAsAdmin = true;
            list.Add(bkt);

            AppEntry jtdx = new AppEntry();
            jtdx.Id = "jtdx";
            jtdx.Name = "JTDX";
            jtdx.WaitSeconds = 40;
            jtdx.DelaySeconds = 5;
            list.Add(jtdx);

            AppEntry gt = new AppEntry();
            gt.Id = "gt2";
            gt.Name = "GridTracker2";
            gt.WaitSeconds = 60;
            gt.DelaySeconds = 5;
            list.Add(gt);

            AppEntry qlog = new AppEntry();
            qlog.Id = "qlog";
            qlog.Name = "QLog";
            qlog.WaitSeconds = 45;
            qlog.DelaySeconds = 3;
            list.Add(qlog);

            return list;
        }

        public static string KnownProcessName(string id)
        {
            switch (id)
            {
                case "bkt": return "BktTimeSync";
                case "jtdx": return "jtdx";
                case "gt2": return "GridTracker2";
                case "qlog": return "qlog";
                default: return id;
            }
        }

        public static string KnownExeName(string id)
        {
            switch (id)
            {
                case "bkt": return "BktTimeSync.exe";
                case "jtdx": return "jtdx.exe";
                case "gt2": return "GridTracker2.exe";
                case "qlog": return "qlog.exe";
                default: return id + ".exe";
            }
        }

        /// <summary>Short explanatory text used by the UI.</summary>
        public static string Description(string id)
        {
            switch (id)
            {
                case "bkt": return "系统对时（无 UDP）";
                case "jtdx": return "FT8/FT4 解码，UDP 发送端";
                case "gt2": return "地图与台站追踪，UDP 接收端";
                case "qlog": return "通联日志，UDP 接收端";
                default: return "";
            }
        }
    }

    public class UdpSettings
    {
        public bool Enabled = true;

        public string JtdxUdp1Addr = "239.255.0.1";
        public int JtdxUdp1Port = 2237;
        public string JtdxUdp2Addr = "127.0.0.1";
        public int JtdxUdp2Port = 2333;
        public bool JtdxAcceptRequests = true;

        public string Gt2Addr = "239.255.0.1";
        public int Gt2Port = 2237;
        public bool Gt2Multicast = true;

        public int QlogPort = 2237;
        public string QlogAddr = "239.255.0.1";
        public bool QlogJoin = true;
        public int QlogTtl = 1;
        public string QlogForwardAddr = "127.0.0.1";
    }

    public class AppSettings
    {
        public string ConfigPath = "";
        public List<AppEntry> Apps = new List<AppEntry>();
        public bool SkipRunning = true;
        public bool WaitForWindows = true;
        public bool FocusJtdx = true;
        public bool CloseAppsOnExit = false;
        public bool MinimizeAfterStart = false;
        public UdpSettings Udp = new UdpSettings();

        public AppEntry FindApp(string id)
        {
            foreach (AppEntry a in Apps)
            {
                if (string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) return a;
            }
            return null;
        }

        /// <summary>
        /// Picks a writable settings file. Portable first (next to the exe), then the
        /// user's AppData, then %TEMP% as a last resort. This keeps the launcher working
        /// even when AppData writes are blocked by policy / sandbox / antivirus.
        /// </summary>
        public static string ResolveConfigPath(string requested)
        {
            if (!string.IsNullOrEmpty(requested))
            {
                try { return Path.GetFullPath(requested); }
                catch { return requested; }
            }

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string portable = Path.Combine(exeDir, "HamStartup.settings.ini");
            string roaming = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HamStartup", "settings.ini");
            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HamStartup", "settings.ini");
            string temp = Path.Combine(Path.GetTempPath(), "HamStartup", "settings.ini");

            // Preserve an existing configuration if its folder is writable.
            if (File.Exists(roaming) && IsWritable(roaming)) return roaming;
            if (File.Exists(local) && IsWritable(local)) return local;
            if (File.Exists(portable) && IsWritable(portable)) return portable;

            // If an old AppData config exists but is read-only, migrate a copy next to the exe.
            if (File.Exists(roaming) && IsWritable(portable))
            {
                try
                {
                    File.Copy(roaming, portable, false);
                    return portable;
                }
                catch { }
            }

            if (IsWritable(portable)) return portable;
            if (IsWritable(roaming)) return roaming;
            if (IsWritable(local)) return local;
            return temp;
        }

        private static bool IsWritable(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return false;
                string dir = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(dir)) return false;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(filePath))
                {
                    FileAttributes attributes = File.GetAttributes(filePath);
                    if ((attributes & FileAttributes.ReadOnly) != 0) return false;
                }

                string probe = filePath + ".write-test-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static string DefaultConfigPath
        {
            get { return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HamStartup", "settings.ini"); }
        }

        public static string DefaultConfigDirectory
        {
            get { return Path.GetDirectoryName(DefaultConfigPath); }
        }

        public static AppSettings Load(string path)
        {
            AppSettings s = new AppSettings();
            s.ConfigPath = path;
            s.Apps = AppCatalog.CreateDefaults();

            IniFile ini = new IniFile();
            if (File.Exists(path))
            {
                try { ini = IniFile.Load(path); }
                catch { ini = new IniFile(); }
            }

            s.SkipRunning = ini.GetBool("general", "skipRunning", true);
            s.WaitForWindows = ini.GetBool("general", "waitForWindows", true);
            s.FocusJtdx = ini.GetBool("general", "focusJtdx", true);
            s.CloseAppsOnExit = ini.GetBool("general", "closeAppsOnExit", false);
            s.MinimizeAfterStart = ini.GetBool("general", "minimizeAfterStart", false);

            string orderText = ini.Get("order", "apps", "bkt,jtdx,gt2,qlog");
            List<string> order = new List<string>();
            foreach (string raw in orderText.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = raw.Trim().ToLowerInvariant();
                if (id.Length == 0) continue;
                bool exists = false;
                foreach (AppEntry a in s.Apps)
                {
                    if (a.Id == id) { exists = true; break; }
                }
                if (exists && !order.Contains(id)) order.Add(id);
            }
            foreach (AppEntry a in s.Apps)
            {
                if (!order.Contains(a.Id)) order.Add(a.Id);
            }

            List<AppEntry> ordered = new List<AppEntry>();
            foreach (string id in order)
            {
                foreach (AppEntry a in s.Apps)
                {
                    if (a.Id != id) continue;
                    string sec = "app." + id;
                    a.Enabled = ini.GetBool(sec, "enabled", a.Enabled);
                    a.ExePath = ini.Get(sec, "path", a.ExePath);
                    a.Arguments = ini.Get(sec, "args", a.Arguments);
                    a.WaitForWindow = ini.GetBool(sec, "waitWindow", a.WaitForWindow);
                    a.WaitSeconds = ini.GetInt(sec, "waitSeconds", a.WaitSeconds);
                    a.DelaySeconds = ini.GetInt(sec, "delaySeconds", a.DelaySeconds);
                    a.RunAsAdmin = ini.GetBool(sec, "runAsAdmin", a.RunAsAdmin);
                    ordered.Add(a);
                    break;
                }
            }
            s.Apps = ordered;

            UdpSettings u = s.Udp;
            u.Enabled = ini.GetBool("udp", "enabled", true);
            u.JtdxUdp1Addr = ini.Get("udp", "jtdxUdp1Addr", u.JtdxUdp1Addr);
            u.JtdxUdp1Port = ini.GetInt("udp", "jtdxUdp1Port", u.JtdxUdp1Port);
            u.JtdxUdp2Addr = ini.Get("udp", "jtdxUdp2Addr", u.JtdxUdp2Addr);
            u.JtdxUdp2Port = ini.GetInt("udp", "jtdxUdp2Port", u.JtdxUdp2Port);
            u.JtdxAcceptRequests = ini.GetBool("udp", "jtdxAcceptRequests", u.JtdxAcceptRequests);
            u.Gt2Addr = ini.Get("udp", "gt2Addr", u.Gt2Addr);
            u.Gt2Port = ini.GetInt("udp", "gt2Port", u.Gt2Port);
            u.Gt2Multicast = ini.GetBool("udp", "gt2Multicast", u.Gt2Multicast);
            u.QlogPort = ini.GetInt("udp", "qlogPort", u.QlogPort);
            u.QlogAddr = ini.Get("udp", "qlogAddr", u.QlogAddr);
            u.QlogJoin = ini.GetBool("udp", "qlogJoin", u.QlogJoin);
            u.QlogTtl = ini.GetInt("udp", "qlogTtl", u.QlogTtl);
            u.QlogForwardAddr = ini.Get("udp", "qlogForwardAddr", u.QlogForwardAddr);

            return s;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(ConfigPath)) ConfigPath = DefaultConfigPath;
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            IniFile ini = new IniFile();
            ini.Set("general", "skipRunning", SkipRunning ? "True" : "False");
            ini.Set("general", "waitForWindows", WaitForWindows ? "True" : "False");
            ini.Set("general", "focusJtdx", FocusJtdx ? "True" : "False");
            ini.Set("general", "closeAppsOnExit", CloseAppsOnExit ? "True" : "False");
            ini.Set("general", "minimizeAfterStart", MinimizeAfterStart ? "True" : "False");

            List<string> ids = new List<string>();
            foreach (AppEntry a in Apps) ids.Add(a.Id);
            ini.Set("order", "apps", string.Join(",", ids.ToArray()));

            foreach (AppEntry a in Apps)
            {
                string sec = "app." + a.Id;
                ini.Set(sec, "name", a.Name);
                ini.Set(sec, "enabled", a.Enabled ? "True" : "False");
                ini.Set(sec, "path", a.ExePath);
                ini.Set(sec, "args", a.Arguments);
                ini.Set(sec, "waitWindow", a.WaitForWindow ? "True" : "False");
                ini.Set(sec, "waitSeconds", a.WaitSeconds.ToString());
                ini.Set(sec, "delaySeconds", a.DelaySeconds.ToString());
                ini.Set(sec, "runAsAdmin", a.RunAsAdmin ? "True" : "False");
            }

            UdpSettings u = Udp;
            ini.Set("udp", "enabled", u.Enabled ? "True" : "False");
            ini.Set("udp", "jtdxUdp1Addr", u.JtdxUdp1Addr);
            ini.Set("udp", "jtdxUdp1Port", u.JtdxUdp1Port.ToString());
            ini.Set("udp", "jtdxUdp2Addr", u.JtdxUdp2Addr);
            ini.Set("udp", "jtdxUdp2Port", u.JtdxUdp2Port.ToString());
            ini.Set("udp", "jtdxAcceptRequests", u.JtdxAcceptRequests ? "True" : "False");
            ini.Set("udp", "gt2Addr", u.Gt2Addr);
            ini.Set("udp", "gt2Port", u.Gt2Port.ToString());
            ini.Set("udp", "gt2Multicast", u.Gt2Multicast ? "True" : "False");
            ini.Set("udp", "qlogPort", u.QlogPort.ToString());
            ini.Set("udp", "qlogAddr", u.QlogAddr);
            ini.Set("udp", "qlogJoin", u.QlogJoin ? "True" : "False");
            ini.Set("udp", "qlogTtl", u.QlogTtl.ToString());
            ini.Set("udp", "qlogForwardAddr", u.QlogForwardAddr);

            ini.Save(ConfigPath);
        }
    }
}
