using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        /// <summary>Closes only the processes this session started. Blocking; call from a worker.</summary>
        public void StopAllSync()
        {
            if (_started.Count == 0)
            {
                _log("本次没有由启动器启动的软件需要关闭。", LogKind.Info);
                return;
            }

            for (int i = _started.Count - 1; i >= 0; i--)
            {
                Process p = _started[i];
                string name = "进程";
                try { name = p.ProcessName + " (PID " + p.Id + ")"; }
                catch { }

                if (HasExitedSafe(p))
                {
                    _log(name + " 已经退出", LogKind.Info);
                    continue;
                }

                _log("正在关闭 " + name + " …", LogKind.Info);
                try
                {
                    bool requested = p.CloseMainWindow();
                    if (requested && p.WaitForExit(4000))
                    {
                        _log("  已正常关闭", LogKind.Success);
                        continue;
                    }
                    if (!HasExitedSafe(p))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                        _log("  已强制结束", LogKind.Warn);
                    }
                }
                catch (Exception ex)
                {
                    _log("  无法关闭：" + ex.Message + "（如为管理员进程请手动关闭）", LogKind.Error);
                }
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
