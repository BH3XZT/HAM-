using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HamStartup
{
    /// <summary>Runs the configured applications one after another.</summary>
    public class Launcher
    {
        private readonly AppSettings _settings;
        private readonly Action<string, LogKind> _log;
        private readonly List<Process> _started = new List<Process>();

        public Launcher(AppSettings settings, Action<string, LogKind> log)
        {
            _settings = settings;
            _log = log;
        }

        public List<Process> StartedProcesses
        {
            get { return _started; }
        }

        public async Task RunAsync(CancellationToken token)
        {
            List<AppEntry> apps = new List<AppEntry>();
            foreach (AppEntry a in _settings.Apps)
            {
                if (a.Enabled) apps.Add(a);
            }

            if (apps.Count == 0)
            {
                _log("没有勾选任何软件，未执行启动。", LogKind.Warn);
                return;
            }

            Stopwatch total = Stopwatch.StartNew();
            int started = 0;
            int skipped = 0;
            int failed = 0;

            for (int i = 0; i < apps.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                AppEntry app = apps[i];
                _log(string.Format("▶ [{0}/{1}] {2}", i + 1, apps.Count, app.Name), LogKind.Step);
                _log("  启动方式：" + ProcessUtil.DescribeStart(app), LogKind.Info);

                if (string.Equals(app.Id, "qlog", StringComparison.OrdinalIgnoreCase))
                {
                    string qlogDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "hamradio", "QLog");
                    if (System.IO.Directory.Exists(qlogDir) && !ProcessUtil.IsDirectoryWritable(qlogDir))
                    {
                        _log("  警告：QLog 数据目录不可写（" + qlogDir + "）。", LogKind.Warn);
                        _log("  QLog 可能以“全新配置”启动；请正常双击运行启动器，或检查目录权限/杀毒软件。", LogKind.Warn);
                    }
                }

                if (!app.PathExists)
                {
                    _log("  找不到程序文件：" + (string.IsNullOrEmpty(app.ExePath) ? "(未设置路径)" : app.ExePath), LogKind.Error);
                    failed++;
                    continue;
                }

                Process existing = ProcessUtil.FindRunning(app);
                if (existing != null && _settings.SkipRunning)
                {
                    string pidText = "";
                    try { pidText = " (PID " + existing.Id + ")"; }
                    catch { }
                    _log("  已在运行" + pidText + "，跳过启动", LogKind.Warn);
                    skipped++;
                    continue;
                }

                Process process = null;
                try
                {
                    process = ProcessUtil.Start(app);
                    if (process != null)
                    {
                        _started.Add(process);
                        string pidText = "";
                        try { pidText = "，PID " + process.Id; }
                        catch { }
                        _log("  已启动" + pidText, LogKind.Success);
                    }
                    else
                    {
                        _log("  已请求启动（系统未返回进程句柄）", LogKind.Success);
                    }
                    started++;
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223)
                        _log("  用户取消了管理员权限授权，已跳过", LogKind.Warn);
                    else if (ex.NativeErrorCode == 193)
                        _log("  启动失败：不是有效的 Win32 应用程序（文件可能损坏或架构不匹配）", LogKind.Error);
                    else if (ex.NativeErrorCode == 740)
                        _log("  启动失败：该程序需要管理员权限，请勾选“以管理员身份启动”", LogKind.Error);
                    else if (ex.NativeErrorCode == 5)
                        _log("  启动失败：拒绝访问（可能被杀毒软件或系统策略拦截）", LogKind.Error);
                    else
                        _log("  启动失败：" + ex.Message, LogKind.Error);
                    failed++;
                    continue;
                }
                catch (Exception ex)
                {
                    _log("  启动失败：" + ex.Message, LogKind.Error);
                    failed++;
                    continue;
                }

                if (_settings.WaitForWindows && app.WaitForWindow && app.WaitSeconds > 0)
                {
                    bool ready = await WaitForWindowAsync(process, app, token);
                    if (!ready && process != null && !HasExitedSafe(process))
                    {
                        _log(string.Format("  等待窗口超时（{0} 秒），继续下一步", app.WaitSeconds), LogKind.Warn);
                    }
                }

                token.ThrowIfCancellationRequested();

                if (app.DelaySeconds > 0)
                {
                    _log(string.Format("  等待 {0} 秒后继续…", app.DelaySeconds), LogKind.Info);
                    await Task.Delay(app.DelaySeconds * 1000, token);
                }
            }

            token.ThrowIfCancellationRequested();

            if (_settings.FocusJtdx)
            {
                await Task.Delay(700);
                await FocusJtdxAsync();
            }

            _log(string.Format("完成：启动 {0}，跳过 {1}，失败 {2}，用时 {3:0.0} 秒",
                started, skipped, failed, total.Elapsed.TotalSeconds),
                failed > 0 ? LogKind.Warn : LogKind.Success);
        }

        public async Task RunAllAtOnceAsync(CancellationToken token)
        {
            List<AppEntry> apps = new List<AppEntry>();
            foreach (AppEntry a in _settings.Apps)
            {
                if (a.Enabled) apps.Add(a);
            }
            if (apps.Count == 0)
            {
                _log("没有勾选任何软件，未执行启动。", LogKind.Warn);
                return;
            }

            Stopwatch total = Stopwatch.StartNew();
            int started = 0;
            int skipped = 0;
            int failed = 0;

            _log("模式：全部同时启动（忽略顺序）", LogKind.Step);
            foreach (AppEntry app in apps)
            {
                token.ThrowIfCancellationRequested();
                _log("▶ " + app.Name, LogKind.Step);
                _log("  启动方式：" + ProcessUtil.DescribeStart(app), LogKind.Info);

                if (!app.PathExists)
                {
                    _log("  找不到程序文件：" + (string.IsNullOrEmpty(app.ExePath) ? "(未设置路径)" : app.ExePath), LogKind.Error);
                    failed++;
                    continue;
                }

                Process existing = ProcessUtil.FindRunning(app);
                if (existing != null && _settings.SkipRunning)
                {
                    _log("  已在运行，跳过启动", LogKind.Warn);
                    skipped++;
                    continue;
                }

                try
                {
                    Process process = ProcessUtil.Start(app);
                    if (process != null)
                    {
                        _started.Add(process);
                        _log("  已启动，PID " + SafePid(process), LogKind.Success);
                    }
                    else
                    {
                        _log("  已请求启动（未取得进程句柄）", LogKind.Success);
                    }
                    started++;
                }
                catch (Win32Exception ex)
                {
                    _log("  启动失败：" + StartupErrorText(ex), LogKind.Error);
                    failed++;
                }
                catch (Exception ex)
                {
                    _log("  启动失败：" + ex.Message, LogKind.Error);
                    failed++;
                }
            }

            if (_settings.FocusJtdx)
            {
                await Task.Delay(1500);
                await FocusJtdxAsync();
            }

            _log(string.Format("完成：启动 {0}，跳过 {1}，失败 {2}，用时 {3:0.0} 秒",
                started, skipped, failed, total.Elapsed.TotalSeconds),
                failed > 0 ? LogKind.Warn : LogKind.Success);
        }

        private static string SafePid(Process p)
        {
            try { return p.Id.ToString(); }
            catch { return "?"; }
        }

        private static string StartupErrorText(Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223) return "用户取消了管理员权限授权";
            if (ex.NativeErrorCode == 193) return "不是有效的 Win32 应用程序";
            if (ex.NativeErrorCode == 740) return "需要管理员权限，请勾选“以管理员身份启动”";
            if (ex.NativeErrorCode == 5) return "拒绝访问（可能被杀毒软件拦截）";
            return ex.Message;
        }

        private async Task<bool> WaitForWindowAsync(Process process, AppEntry app, CancellationToken token)
        {
            if (process == null) return false;
            Stopwatch watch = Stopwatch.StartNew();
            int accessDenied = 0;
            bool elevateFallback = false;
            while (watch.Elapsed.TotalSeconds < app.WaitSeconds)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (process.HasExited)
                    {
                        _log("  进程已退出（可能配置有误或被其他实例接管），跳过等待", LogKind.Warn);
                        return false;
                    }
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        _log(string.Format("  窗口已就绪（{0:0.0} 秒）", watch.Elapsed.TotalSeconds), LogKind.Success);
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process exited between checks.
                    return false;
                }
                catch (Win32Exception)
                {
                    accessDenied++;
                    if (accessDenied >= 3) elevateFallback = true;
                }
                catch { }

                if (elevateFallback)
                {
                    _log("  无法读取管理员进程的窗口状态，按固定延时继续", LogKind.Info);
                    await Task.Delay(2000, token);
                    return true;
                }

                await Task.Delay(250, token);
            }
            return false;
        }

        private async Task FocusJtdxAsync()
        {
            AppEntry jtdx = _settings.FindApp("jtdx");
            if (jtdx == null) return;

            Process process = ProcessUtil.FindRunning(jtdx);
            if (process == null)
            {
                _log("没有发现运行中的 JTDX，跳过窗口前置", LogKind.Info);
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                if (ProcessUtil.BringToFront(process))
                {
                    _log("JTDX 已切换到前台", LogKind.Success);
                    return;
                }
                await Task.Delay(300);
            }
            _log("无法自动前置 JTDX 窗口，请手动切换", LogKind.Warn);
        }

        public bool HasStoppableTargets()
        {
            foreach (Process p in _started)
            {
                try { if (!p.HasExited) return true; }
                catch { }
            }
            foreach (AppEntry app in _settings.Apps)
            {
                if (ProcessUtil.IsRunning(app)) return true;
            }
            return false;
        }

        /// <summary>Closes all running processes of the current scheme, plus processes started this session.</summary>
        public void StopAllSync()
        {
            List<Process> targets = new List<Process>();
            HashSet<int> seen = new HashSet<int>();

            AddProcessTargets(targets, seen, _started);
            foreach (AppEntry app in _settings.Apps)
            {
                string name = app.ProcessName;
                if (string.IsNullOrEmpty(name)) continue;
                Process[] all = null;
                try { all = Process.GetProcessesByName(name); }
                catch { }
                if (all != null) AddProcessTargets(targets, seen, all);
            }

            if (targets.Count == 0)
            {
                _log("当前没有正在运行的目标软件。", LogKind.Info);
                return;
            }

            _log("开始停止 " + targets.Count + " 个进程…", LogKind.Step);

            // First give every process a chance to close through its window.
            foreach (Process p in targets)
            {
                if (HasExitedSafe(p)) continue;
                try
                {
                    p.CloseMainWindow();
                }
                catch { }
            }

            // Wait a bit for the main windows and their child processes to exit.
            Stopwatch wait = Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < 4000)
            {
                bool alive = false;
                foreach (Process p in targets)
                {
                    if (!HasExitedSafe(p)) { alive = true; break; }
                }
                if (!alive) break;
                System.Threading.Thread.Sleep(150);
            }

            List<Process> stubborn = new List<Process>();
            foreach (Process p in targets)
            {
                if (HasExitedSafe(p)) continue;
                string name = "进程";
                int pid = 0;
                try { name = p.ProcessName; pid = p.Id; } catch { }
                try
                {
                    p.Kill();
                    p.WaitForExit(1500);
                    if (!HasExitedSafe(p)) stubborn.Add(p);
                    else _log("  已结束 " + name + " (PID " + pid + ")", LogKind.Warn);
                }
                catch (Exception ex)
                {
                    stubborn.Add(p);
                    _log("  无法结束 " + name + " (PID " + pid + ")：" + ex.Message, LogKind.Warn);
                }
            }

            // Elevated Ham programs cannot be killed by a non-elevated launcher.
            // Ask Windows once for an elevated taskkill; the user can cancel the UAC prompt.
            if (stubborn.Count > 0)
            {
                List<int> pids = new List<int>();
                foreach (Process p in stubborn)
                {
                    try { pids.Add(p.Id); }
                    catch { }
                }
                if (pids.Count > 0) TryElevatedTaskKill(pids);
            }

            int stillRunning = 0;
            foreach (Process p in targets)
                if (!HasExitedSafe(p)) stillRunning++;

            if (stillRunning == 0)
                _log("停止全部完成，目标软件已关闭。", LogKind.Success);
            else
                _log("仍有 " + stillRunning + " 个进程未关闭；如为管理员进程，请以管理员身份运行本工具后重试。", LogKind.Warn);

            foreach (Process p in targets) p.Dispose();
        }

        private static void AddProcessTargets(List<Process> targets, HashSet<int> seen, IEnumerable<Process> processes)
        {
            if (processes == null) return;
            foreach (Process p in processes)
            {
                if (p == null) continue;
                int pid = 0;
                try { pid = p.Id; }
                catch { continue; }
                if (!seen.Add(pid)) continue;
                targets.Add(p);
            }
        }

        private void TryElevatedTaskKill(List<int> pids)
        {
            try
            {
                StringBuilder args = new StringBuilder("/F /T");
                foreach (int pid in pids) args.Append(" /PID ").Append(pid);
                _log("  正在请求管理员权限结束 " + pids.Count + " 个进程…", LogKind.Warn);
                ProcessStartInfo psi = new ProcessStartInfo("taskkill.exe", args.ToString());
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process killer = Process.Start(psi);
                if (killer != null)
                {
                    killer.WaitForExit(15000);
                    _log("  已执行管理员 taskkill。", LogKind.Info);
                    killer.Dispose();
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    _log("  用户取消了管理员权限；部分软件可能仍在运行。", LogKind.Warn);
                else
                    _log("  管理员结束进程失败：" + ex.Message, LogKind.Warn);
            }
            catch (Exception ex)
            {
                _log("  管理员结束进程失败：" + ex.Message, LogKind.Warn);
            }
        }


        public async Task StopAllAsync()
        {
            await Task.Run(new Action(StopAllSync));
        }

        private static bool HasExitedSafe(Process p)
        {
            try { return p.HasExited; }
            catch { return true; }
        }
    }
}
