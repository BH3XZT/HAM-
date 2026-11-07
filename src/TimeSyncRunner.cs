using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HamStartup
{
    /// <summary>一次授时调用的结果。</summary>
    public class TimeSyncResult
    {
        public bool Started = false;
        public int ExitCode = -1;
        public string Output = "";
        public string Error = "";
        public double Seconds = 0;

        public bool Success
        {
            get { return Started && ExitCode == 0; }
        }
    }

    /// <summary>
    /// 调用外部 ic705timesync.exe：检测连接 / 试对时 / 对时。
    /// 只依赖控制台程序的退出码和标准输出，不把授时逻辑嵌进启动器。
    /// </summary>
    public static class TimeSyncRunner
    {
        public static string ResolveExe(TimeSyncSettings settings)
        {
            if (settings != null && !string.IsNullOrEmpty(settings.ExePath) && File.Exists(settings.ExePath))
                return settings.ExePath;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "ic705timesync.exe"),
                Path.Combine(baseDir, @"..\ic705timesync\dist\ic705timesync.exe"),
                Path.Combine(baseDir, @"..\ic705timesync\ic705timesync.exe"),
                @"D:\电脑软件\deepseek会话区\ic705timesync\dist\ic705timesync.exe",
                @"D:\电台软件\ic705timesync\ic705timesync.exe"
            };
            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch { }
            }
            return "";
        }

        /// <summary>action: read = 读 GPS 与偏差；dryrun = 只算不写；sync = 真正对时。</summary>
        public static string BuildArguments(TimeSyncSettings settings, string action)
        {
            StringBuilder sb = new StringBuilder();
            if (action == "read") sb.Append("read --json");
            else if (action == "dryrun") sb.Append("sync --dry-run --force");
            else sb.Append("sync");

            // 自 v1.5.2 起只支持串口方式；旧配置的 transport=lan 也按串口处理，
            // 避免独立 exe 去加载不存在的 icom-lan。
            sb.Append(" --transport serial --port \"").Append(
                string.IsNullOrEmpty(settings.Port) ? "auto" : settings.Port).Append("\"");
            return sb.ToString();
        }

        public static TimeSyncResult Run(TimeSyncSettings settings, string action)
        {
            TimeSyncResult result = new TimeSyncResult();
            string exe = ResolveExe(settings);
            if (string.IsNullOrEmpty(exe))
            {
                result.Error = "找不到 ic705timesync.exe，请在本页设置程序路径";
                return result;
            }

            bool elevate = (action == "sync") && settings.RunAsAdmin;
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = BuildArguments(settings, action);
            try { psi.WorkingDirectory = Path.GetDirectoryName(exe); }
            catch { }
            psi.CreateNoWindow = true;

            if (elevate)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = new UTF8Encoding(false);
                psi.StandardErrorEncoding = new UTF8Encoding(false);
            }

            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        result.Error = "无法启动进程";
                        return result;
                    }
                    result.Started = true;
                    if (!elevate)
                    {
                        result.Output = process.StandardOutput.ReadToEnd();
                        result.Error = process.StandardError.ReadToEnd();
                    }

                    int limitMs = Math.Max(5, settings.WaitSeconds) * 1000;
                    if (!process.WaitForExit(limitMs))
                    {
                        try { process.Kill(); } catch { }
                        result.Error = (result.Error + "\n调用超时（" + settings.WaitSeconds + " 秒）").Trim();
                    }
                    else
                    {
                        try { result.ExitCode = process.ExitCode; }
                        catch { result.ExitCode = -1; }
                    }
                    result.Seconds = watch.Elapsed.TotalSeconds;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                result.Error = ex.NativeErrorCode == 1223 ? "用户取消了管理员授权" : ex.Message;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        public static Task<TimeSyncResult> RunAsync(TimeSyncSettings settings, string action)
        {
            return Task.Run(delegate { return Run(settings, action); });
        }
    }
}
