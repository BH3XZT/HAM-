using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HamStartup
{
    /// <summary>Process discovery, launching and window activation helpers.</summary>
    public static class ProcessUtil
    {
        public static Process FindRunning(AppEntry app)
        {
            string name = app.ProcessName;
            if (string.IsNullOrEmpty(name)) return null;
            Process[] all;
            try { all = Process.GetProcessesByName(name); }
            catch { return null; }

            foreach (Process p in all)
            {
                try
                {
                    if (!p.HasExited) return p;
                }
                catch { }
            }
            return null;
        }

        public static bool IsRunning(AppEntry app)
        {
            Process p = FindRunning(app);
            if (p == null) return false;
            try { p.Dispose(); } catch { }
            return true;
        }

        public static bool IsProcessNameRunning(string name)
        {
            try
            {
                Process[] all = Process.GetProcessesByName(name);
                foreach (Process p in all)
                {
                    try { if (!p.HasExited) return true; }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        public static Process Start(AppEntry app)
        {
            bool shortcut = app.IsShortcut;
            string launchPath = app.ExePath;
            string launchArguments = app.Arguments ?? "";
            string launchDirectory = app.WorkingDirectory;

            if (shortcut)
            {
                ShortcutInfo info = app.Shortcut;
                if (info == null || !info.Valid)
                    throw new InvalidOperationException("快捷方式无效：" + (info == null ? "无法解析" : info.Error));
                if (!File.Exists(info.TargetPath))
                    throw new InvalidOperationException("快捷方式目标不存在：" + info.TargetPath);
                if (string.Equals(Path.GetExtension(info.TargetPath), ".exe", StringComparison.OrdinalIgnoreCase))
                    ValidateExecutable(info.TargetPath);

                // Launch the shortcut's target directly instead of ShellExecute'ing the .lnk.
                // ShellExecute on a .lnk can block for many seconds in restricted environments.
                launchPath = info.TargetPath;
                launchDirectory = string.IsNullOrEmpty(info.WorkingDirectory) ? app.Directory : info.WorkingDirectory;
                launchArguments = info.Arguments ?? "";
                if (!string.IsNullOrEmpty(app.Arguments))
                {
                    launchArguments = string.IsNullOrEmpty(launchArguments)
                        ? app.Arguments
                        : launchArguments + " " + app.Arguments;
                }
            }
            else
            {
                ValidateExecutable(app.ExePath);
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = launchPath;
            psi.Arguments = launchArguments;
            psi.WorkingDirectory = launchDirectory;
            psi.UseShellExecute = true;
            if (app.RunAsAdmin) psi.Verb = "runas";

            Process process = Process.Start(psi);
            if (process == null && shortcut)
            {
                process = WaitForStarted(app, 4000);
            }
            return process;
        }

        public static string DescribeStart(AppEntry app)
        {
            if (app.IsShortcut)
            {
                ShortcutInfo info = app.Shortcut;
                if (info == null || !info.Valid) return "快捷方式（无法解析）";
                return "快捷方式 → " + info.TargetPath + "  [起始目录 " + info.WorkingDirectory + "]";
            }
            return app.ExePath + "  [起始目录 " + app.WorkingDirectory + "]";
        }

        private static Process WaitForStarted(AppEntry app, int milliseconds)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                Process found = FindRunning(app);
                if (found != null) return found;
                System.Threading.Thread.Sleep(150);
            }
            return null;
        }

        public static bool IsDirectoryWritable(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
                string test = Path.Combine(path, ".hamstartup-write-test-" + Guid.NewGuid().ToString("N"));
                using (FileStream stream = new FileStream(test, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.WriteByte(1);
                }
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Fails fast for files that exist but are not valid Windows executables
        /// (ShellExecute can otherwise block the caller for 10-20 seconds).
        /// </summary>
        public static void ValidateExecutable(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("程序文件不存在", path);

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".bat" || extension == ".cmd" || extension == ".com") return;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 0x40) throw new InvalidOperationException("文件不是有效的 Windows 可执行文件");
                    BinaryReader reader = new BinaryReader(stream);
                    if (reader.ReadByte() != 'M' || reader.ReadByte() != 'Z')
                        throw new InvalidOperationException("文件不是有效的 Windows 可执行文件（缺少 MZ 头）");

                    stream.Position = 0x3C;
                    int peOffset = reader.ReadInt32();
                    if (peOffset <= 0 || peOffset + 4L > stream.Length)
                        throw new InvalidOperationException("文件不是有效的 Windows 可执行文件（PE 偏移无效）");

                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550u)
                        throw new InvalidOperationException("文件不是有效的 Windows 可执行文件（缺少 PE 签名）");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("无法校验可执行文件：" + ex.Message, ex);
            }
        }

        public static bool BringToFront(Process p)
        {
            if (p == null) return false;
            IntPtr hwnd = IntPtr.Zero;
            try
            {
                p.Refresh();
                hwnd = p.MainWindowHandle;
            }
            catch { }

            if (hwnd == IntPtr.Zero)
            {
                try { hwnd = FindTopWindow((uint)p.Id, null); }
                catch { }
            }
            if (hwnd == IntPtr.Zero) return false;

            try
            {
                ShowWindow(hwnd, 9); // SW_RESTORE
                SetForegroundWindow(hwnd);
                return true;
            }
            catch { return false; }
        }

        public static bool ActivateByTitle(string titlePart)
        {
            IntPtr hwnd = FindTopWindow(0, titlePart);
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                ShowWindow(hwnd, 9);
                SetForegroundWindow(hwnd);
                return true;
            }
            catch { return false; }
        }

        public static void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
                }
                else if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
                }
            }
            catch { }
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IntPtr FindTopWindow(uint pid, string titlePart)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                try
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    if (GetWindowTextLength(hWnd) == 0) return true;

                    if (pid != 0)
                    {
                        uint owner;
                        GetWindowThreadProcessId(hWnd, out owner);
                        if (owner != pid) return true;
                    }

                    if (titlePart != null)
                    {
                        StringBuilder sb = new StringBuilder(512);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        if (sb.ToString().IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) < 0) return true;
                    }

                    found = hWnd;
                    return false;
                }
                catch { return true; }
            }, IntPtr.Zero);
            return found;
        }
    }
}
