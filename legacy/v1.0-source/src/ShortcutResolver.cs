using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HamStartup
{
    public class ShortcutInfo
    {
        public bool Valid;
        public string TargetPath = "";
        public string Arguments = "";
        public string WorkingDirectory = "";
        public string Error = "";
    }

    /// <summary>
    /// Resolves a Windows .lnk through the WScript.Shell COM object so the launcher can
    /// know the target process name while still starting the shortcut exactly like a
    /// desktop double-click (preserving Start-in / shortcut flags).
    /// </summary>
    public static class ShortcutResolver
    {
        private static readonly Dictionary<string, ShortcutInfo> Cache =
            new Dictionary<string, ShortcutInfo>(StringComparer.OrdinalIgnoreCase);

        public static ShortcutInfo Resolve(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath)) return Invalid("快捷方式路径为空");
            string full;
            try { full = Path.GetFullPath(shortcutPath); }
            catch (Exception ex) { return Invalid(ex.Message); }

            if (!File.Exists(full)) return Invalid("快捷方式不存在");

            ShortcutInfo cached;
            lock (Cache)
            {
                if (Cache.TryGetValue(full, out cached)) return cached;
            }

            ShortcutInfo info = ReadWithWScript(full);
            lock (Cache) { Cache[full] = info; }
            return info;
        }

        public static void ClearCache()
        {
            lock (Cache) { Cache.Clear(); }
        }

        public static string FindDesktopShortcut(string keyword, string wantedExe)
        {
            foreach (string dir in ShortcutDirectories())
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string file in Directory.GetFiles(dir, "*.lnk"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;

                        ShortcutInfo info = Resolve(file);
                        if (!info.Valid) continue;
                        if (string.IsNullOrEmpty(wantedExe)) return file;
                        if (string.Equals(Path.GetFileName(info.TargetPath), wantedExe,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return file;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static IEnumerable<string> ShortcutDirectories()
        {
            List<string> dirs = new List<string>();

            string desktop = null;
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (key != null) desktop = key.GetValue("Desktop") as string;
                }
            }
            catch { }
            if (!string.IsNullOrEmpty(desktop))
            {
                try { dirs.Add(Environment.ExpandEnvironmentVariables(desktop)); }
                catch { }
            }

            try
            {
                string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (!string.IsNullOrEmpty(common)) dirs.Add(common);
            }
            catch { }
            try
            {
                string mine = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(mine)) dirs.Add(mine);
            }
            catch { }
            try
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (!string.IsNullOrEmpty(programs))
                {
                    dirs.Add(programs);
                    foreach (string sub in Directory.GetDirectories(programs))
                    {
                        dirs.Add(sub);
                    }
                }
            }
            catch { }

            return dirs;
        }

        private static ShortcutInfo ReadWithWScript(string path)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return Invalid("系统未提供 WScript.Shell，无法解析快捷方式");

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                if (shortcut == null) return Invalid("无法读取快捷方式");

                Type type = shortcut.GetType();
                string target = Convert.ToString(type.InvokeMember("TargetPath", BindingFlags.GetProperty,
                    null, shortcut, null));
                string arguments = Convert.ToString(type.InvokeMember("Arguments", BindingFlags.GetProperty,
                    null, shortcut, null));
                string working = Convert.ToString(type.InvokeMember("WorkingDirectory", BindingFlags.GetProperty,
                    null, shortcut, null));

                if (string.IsNullOrEmpty(target)) return Invalid("快捷方式没有目标程序");
                target = Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));
                if (!File.Exists(target)) return Invalid("快捷方式目标不存在：" + target);

                if (!string.IsNullOrEmpty(working))
                {
                    try { working = Environment.ExpandEnvironmentVariables(working.Trim().Trim('"')); }
                    catch { }
                }
                if (string.IsNullOrEmpty(working) || !Directory.Exists(working))
                {
                    try { working = Path.GetDirectoryName(target); }
                    catch { working = ""; }
                }

                ShortcutInfo info = new ShortcutInfo();
                info.Valid = true;
                info.TargetPath = target;
                info.Arguments = arguments ?? "";
                info.WorkingDirectory = working ?? "";
                return info;
            }
            catch (Exception ex)
            {
                return Invalid(ex.Message);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    try { Marshal.FinalReleaseComObject(shortcut); } catch { }
                }
                if (shell != null && Marshal.IsComObject(shell))
                {
                    try { Marshal.FinalReleaseComObject(shell); } catch { }
                }
            }
        }

        private static ShortcutInfo Invalid(string error)
        {
            ShortcutInfo info = new ShortcutInfo();
            info.Valid = false;
            info.Error = error ?? "";
            return info;
        }
    }
}
