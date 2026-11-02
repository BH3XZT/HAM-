using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HamStartup
{
    /// <summary>Locates the four supported programs on this machine.</summary>
    public static class AppDetector
    {
        private static readonly string[] UninstallKeys = new string[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public static string Find(AppEntry app, bool deep)
        {
            string wanted = AppCatalog.KnownExeName(app.Id);

            // Prefer the real desktop icon when its target is the expected executable.
            // This preserves the shortcut's Start-in directory and any shortcut flags.
            string shortcut = ShortcutResolver.FindDesktopShortcut(ShortcutKeyword(app.Id), wanted);
            if (shortcut != null) return shortcut;

            string fromRegistry = FindInRegistry(app.Id, wanted);
            if (fromRegistry != null) return fromRegistry;

            string known = FindKnownLocation(app.Id, wanted);
            if (known != null) return known;

            string found = SearchForFile(QuickRoots(), wanted, 4, 1500, 1.5);
            if (found != null) return found;

            if (deep)
            {
                found = SearchForFile(DeepRoots(), wanted, 5, 12000, 9.0);
                if (found != null) return found;
            }
            return null;
        }

        public static string ShortcutKeyword(string id)
        {
            switch (id)
            {
                case "bkt": return "BktTimeSync";
                case "jtdx": return "jtdx";
                case "gt2": return "GridTracker";
                case "qlog": return "QLog";
                default: return id;
            }
        }

        private static string FindInRegistry(string id, string wantedExe)
        {
            string pattern;
            switch (id)
            {
                case "bkt": pattern = @"BktTimeSync"; break;
                case "jtdx": pattern = @"\bJTDX\b"; break;
                case "gt2": pattern = @"GridTracker"; break;
                case "qlog": pattern = @"^QLog\b"; break;
                default: pattern = Regex.Escape(id); break;
            }

            foreach (RegistryKey root in new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (string sub in UninstallKeys)
                {
                    try
                    {
                        using (RegistryKey key = root.OpenSubKey(sub))
                        {
                            if (key == null) continue;
                            foreach (string name in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey item = key.OpenSubKey(name))
                                    {
                                        if (item == null) continue;
                                        string display = item.GetValue("DisplayName") as string;
                                        if (string.IsNullOrEmpty(display)) continue;
                                        if (!Regex.IsMatch(display, pattern, RegexOptions.IgnoreCase)) continue;

                                        string icon = item.GetValue("DisplayIcon") as string;
                                        string candidate = FromIcon(icon, wantedExe);
                                        if (candidate != null) return candidate;

                                        string location = item.GetValue("InstallLocation") as string;
                                        candidate = Combine(location, wantedExe);
                                        if (candidate != null) return candidate;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        private static string FromIcon(string icon, string wantedExe)
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
                path = path.Trim();
                if (!File.Exists(path)) return null;

                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, wantedExe, StringComparison.OrdinalIgnoreCase)) return path;

                // DisplayIcon often points to a launcher/updater (e.g. BktTimeSyncStart.exe,
                // maintenancetool.exe). Prefer the real executable in the same folder.
                string dir = Path.GetDirectoryName(path);
                string sibling = Combine(dir, wantedExe);
                if (sibling != null) return sibling;
            }
            catch { }
            return null;
        }

        private static string FindKnownLocation(string id, string wantedExe)
        {
            List<string> candidates = new List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            switch (id)
            {
                case "bkt":
                    candidates.Add(Path.Combine(pf86, "BktTimeSync", wantedExe));
                    candidates.Add(Path.Combine(pf, "BktTimeSync", wantedExe));
                    break;

                case "jtdx":
                    AddVersioned(candidates, @"D:\电台软件\JTDX", @"bin\" + wantedExe);
                    AddVersioned(candidates, Path.Combine(pf, "JTDX"), @"bin\" + wantedExe);
                    candidates.Add(Path.Combine(pf, "JTDX", wantedExe));
                    candidates.Add(@"D:\电台软件\JTDX\159\bin\jtdx.exe");
                    break;

                case "gt2":
                    candidates.Add(@"D:\电台软件\GT网格助手\GridTracker2\GridTracker2.exe");
                    candidates.Add(Path.Combine(pf, "GridTracker2", wantedExe));
                    break;

                case "qlog":
                    candidates.Add(@"D:\电台软件\Qlog\qlog.exe");
                    candidates.Add(@"D:\电台软件\QLog\qlog.exe");
                    candidates.Add(Path.Combine(pf, "QLog", wantedExe));
                    break;
            }

            candidates.Add(Path.Combine(pf, wantedExe));
            candidates.Add(Path.Combine(pf86, wantedExe));

            foreach (string candidate in candidates)
            {
                try
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        private static void AddVersioned(List<string> list, string root, string relative)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                string[] dirs = Directory.GetDirectories(root);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (string dir in dirs)
                {
                    list.Add(Path.Combine(dir, relative));
                }
            }
            catch { }
        }

        private static string Combine(string dir, string file)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            try
            {
                string path = Path.Combine(dir.Trim().Trim('"'), file);
                if (File.Exists(path)) return path;
            }
            catch { }
            return null;
        }

        private static string[] QuickRoots()
        {
            List<string> roots = new List<string>();
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            roots.Add(@"D:\电台软件");
            roots.Add(@"D:\Radio");
            roots.Add(@"C:\HamRadio");
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            return roots.ToArray();
        }

        private static string[] DeepRoots()
        {
            List<string> roots = new List<string>(QuickRoots());
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
            roots.Add(@"D:\");
            roots.Add(@"E:\");
            return roots.ToArray();
        }

        private static string SearchForFile(string[] roots, string fileName, int maxDepth, int maxDirs, double maxSeconds)
        {
            Queue<KeyValuePair<string, int>> queue = new Queue<KeyValuePair<string, int>>();
            foreach (string root in roots)
            {
                try
                {
                    if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                        queue.Enqueue(new KeyValuePair<string, int>(root, 0));
                }
                catch { }
            }

            int visited = 0;
            Stopwatch watch = Stopwatch.StartNew();
            while (queue.Count > 0 && visited < maxDirs && watch.Elapsed.TotalSeconds < maxSeconds)
            {
                KeyValuePair<string, int> node = queue.Dequeue();
                visited++;
                string dir = node.Key;
                int depth = node.Value;
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }

                if (depth >= maxDepth) continue;

                try
                {
                    foreach (string sub in Directory.EnumerateDirectories(dir))
                    {
                        string name = Path.GetFileName(sub);
                        if (name.StartsWith("$") || name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("System32", StringComparison.OrdinalIgnoreCase)) continue;
                        queue.Enqueue(new KeyValuePair<string, int>(sub, depth + 1));
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
