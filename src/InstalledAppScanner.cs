using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HamStartup
{
    public class InstalledAppCandidate
    {
        public string Name = "";
        public string Path = "";
        public override string ToString()
        {
            return Name + "    —    " + Path;
        }
    }

    /// <summary>Finds installed Ham-related programs in the registry and Start Menu/Desktop.</summary>
    public static class InstalledAppScanner
    {
        private static readonly string[] Keywords = new string[]
        {
            "n1mm", "wsjtx", "jtdx", "gridtracker", "qlog", "log4om", "mshv", "js8call",
            "fldigi", "dxlab", "dxkeeper", "spotcollector", "winwarbler", "ham radio",
            "hamradio", "bktime", "jtalert", "sdr", "winlink", "varac", "morse", "cw",
            "log4om", "rumlog", "aclog", "skimmer", "wintest", "dxlog", "hrd"
        };

        public static List<InstalledAppCandidate> Scan()
        {
            List<InstalledAppCandidate> list = new List<InstalledAppCandidate>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ScanRegistry(list, seen, Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            ScanRegistry(list, seen, Registry.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            ScanRegistry(list, seen, Registry.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            List<string> dirs = new List<string>();
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.Programs)); } catch { }
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); } catch { }
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)); } catch { }
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (key != null)
                    {
                        string desktop = key.GetValue("Desktop") as string;
                        if (!string.IsNullOrEmpty(desktop))
                            dirs.Add(Environment.ExpandEnvironmentVariables(desktop));
                    }
                }
            }
            catch { }

            foreach (string dir in dirs) ScanShortcuts(list, seen, dir, 2);

            list.Sort(delegate(InstalledAppCandidate a, InstalledAppCandidate b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        private static void ScanRegistry(List<InstalledAppCandidate> list, HashSet<string> seen,
            RegistryKey root, string path)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (string sub in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey item = key.OpenSubKey(sub))
                            {
                                if (item == null) continue;
                                string display = item.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(display) || !IsHam(display)) continue;

                                string icon = item.GetValue("DisplayIcon") as string;
                                string exe = FromIcon(icon);
                                if (exe == null)
                                {
                                    string location = item.GetValue("InstallLocation") as string;
                                    exe = FindExe(location, display);
                                }
                                Add(list, seen, CleanName(display), exe);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void ScanShortcuts(List<InstalledAppCandidate> list, HashSet<string> seen,
            string dir, int depth)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, "*.lnk"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!IsHam(name)) continue;
                    ShortcutInfo info = ShortcutResolver.Resolve(file);
                    if (info != null && info.Valid) Add(list, seen, CleanName(name), file);
                }
                if (depth <= 1) return;
                foreach (string sub in Directory.GetDirectories(dir))
                    ScanShortcuts(list, seen, sub, depth - 1);
            }
            catch { }
        }

        private static void Add(List<InstalledAppCandidate> list, HashSet<string> seen, string name, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string key = path;
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                ShortcutInfo info = ShortcutResolver.Resolve(path);
                if (info == null || !info.Valid) return;
                key = info.TargetPath;
                if (!key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
            }
            try { key = Path.GetFullPath(key); } catch { }
            if (!seen.Add(key)) return;

            InstalledAppCandidate c = new InstalledAppCandidate();
            c.Name = name;
            c.Path = path;
            list.Add(c);
        }

        private static bool IsHam(string text)
        {
            string lower = (text ?? "").ToLowerInvariant();
            foreach (string keyword in Keywords)
            {
                if (lower.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string cleaned = Regex.Replace(name, @"\s*\(.*?\)\s*", " ").Trim();
            return cleaned.Length == 0 ? name : cleaned;
        }

        private static string FromIcon(string icon)
        {
            if (string.IsNullOrEmpty(icon)) return null;
            string path = icon.Trim();
            try
            {
                if (path.StartsWith("\""))
                {
                    int end = path.IndexOf('"', 1);
                    if (end > 0) path = path.Substring(1, end - 1);
                }
                else
                {
                    int comma = path.LastIndexOf(',');
                    if (comma > 0)
                    {
                        string tail = path.Substring(comma + 1).Trim();
                        bool numeric = tail.Length > 0;
                        foreach (char c in tail) if (!char.IsDigit(c)) { numeric = false; break; }
                        if (numeric) path = path.Substring(0, comma);
                    }
                }
                if (File.Exists(path))
                {
                    if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        if (name.Contains("maintenance") || name.Contains("unins") ||
                            name.Contains("setup") || name.Contains("update")) return null;
                        return path;
                    }
                    string dir = Path.GetDirectoryName(path);
                    return FindExe(dir, Path.GetFileNameWithoutExtension(path));
                }
            }
            catch { }
            return null;
        }

        private static string FindExe(string dir, string hint)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
                string[] files = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
                string hintLower = (hint ?? "").ToLowerInvariant();
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (name.Contains("unins") || name.Contains("update") || name.Contains("setup") ||
                        name.Contains("maintenance")) continue;
                    if (IsHam(name) || hintLower.Contains(name)) return file;
                }
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (name.Contains("unins") || name.Contains("update") || name.Contains("setup") ||
                        name.Contains("maintenance")) continue;
                    return file;
                }
            }
            catch { }
            return null;
        }
    }
}
