using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HamStartup
{
    public class RuntimeEndpoint
    {
        public string Address = "";
        public int Port;
        public string Protocol = "UDP";
        public override string ToString()
        {
            return Protocol + " " + Address + ":" + Port;
        }
    }

    public class TcpConnectionInfo
    {
        public string LocalAddress = "";
        public int LocalPort;
        public string RemoteAddress = "";
        public int RemotePort;
        public string State = "";
        public bool LikelyCluster;
        public override string ToString()
        {
            return LocalAddress + ":" + LocalPort + " → " + RemoteAddress + ":" + RemotePort +
                   "  " + State + (LikelyCluster ? "  [疑似 DX 集群]" : "");
        }
    }

    public class AdiFileInfo
    {
        public string Path = "";
        public long Size;
        public DateTime Modified;
        public string AppHint = "";
        public override string ToString()
        {
            return Path;
        }
    }

    public class RuntimeAppInfo
    {
        public string Name = "";
        public string ProcessName = "";
        public int Pid;
        public string Path = "";
        public string WindowTitle = "";
        public bool Running;
        public bool Responding;
        public bool HasWindow;
        public double CpuPercent;
        public long MemoryMb;
        public DateTime? StartTime;
        public string Status = "";
        public int ExitCode;
        public bool IsConfigured;
        public List<RuntimeEndpoint> Udp = new List<RuntimeEndpoint>();
        public List<TcpConnectionInfo> Tcp = new List<TcpConnectionInfo>();
        public List<AdiFileInfo> AdiFiles = new List<AdiFileInfo>();

        public string UdpText
        {
            get
            {
                if (Udp.Count == 0) return "—";
                StringBuilder sb = new StringBuilder();
                foreach (RuntimeEndpoint e in Udp)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(e.Port);
                }
                return sb.ToString();
            }
        }

        public string ClusterText
        {
            get
            {
                int n = 0;
                foreach (TcpConnectionInfo c in Tcp) if (c.LikelyCluster) n++;
                return n == 0 ? "—" : n + " 条";
            }
        }

        public string AdiText
        {
            get
            {
                if (AdiFiles.Count == 0) return "—";
                return AdiFiles[0].Path;
            }
        }
    }

    public class RuntimeSnapshot
    {
        public DateTime Time = DateTime.Now;
        public List<RuntimeAppInfo> Apps = new List<RuntimeAppInfo>();
        public List<AdiFileInfo> AdiFiles = new List<AdiFileInfo>();
        public List<string> NewAlerts = new List<string>();
        public List<string> RecentAlerts = new List<string>();
        public int RunningCount;
        public int UdpCount;
        public int ClusterCount;
    }

    /// <summary>
    /// Scans running Ham-related processes, their UDP/TCP endpoints and ADI files.
    /// Also keeps per-PID state to detect hangs and abnormal exits.
    /// </summary>
    public class RuntimeInspector
    {
        private class ProcessState
        {
            public Process Process;
            public string Name = "";
            public TimeSpan LastCpu;
            public DateTime LastCpuAt;
            public bool HasCpuSample;
            public int NotRespondingCount;
            public bool FreezeAlerted;
        }

        private static readonly string[] HamKeywords = new string[]
        {
            "jtdx", "wsjtx", "gridtracker", "qlog", "n1mm", "mshv", "js8call", "fldigi",
            "log4om", "dxlab", "dxkeeper", "spotcollector", "winwarbler", "hamradio",
            "bktimesync", "bktclock", "jtalert", "rumlog", "aclog", "skimmer", "cwget",
            "sdrsharp", "sdrconsole", "hdsdr", "omnirig", "flrig", "wspr", "varac",
            "winlink", "direwolf", "soundmodem", "ardop", "mmtty", "mixw", "wintest",
            "dxlog", "log4om2", "hrd", "hamradiodeluxe"
        };

        private static readonly int[] ClusterPorts = new int[]
        {
            23, 7300, 7301, 7373, 8000, 7000, 41112
        };

        private readonly Dictionary<int, ProcessState> _states =
            new Dictionary<int, ProcessState>();
        private readonly List<string> _recentAlerts = new List<string>();
        private List<AdiFileInfo> _adiCache = new List<AdiFileInfo>();
        private DateTime _adiCacheAt = DateTime.MinValue;
        private Dictionary<int, string> _processNames = new Dictionary<int, string>();

        public DateTime MuteAlertsUntil = DateTime.MinValue;

        public RuntimeSnapshot Collect(AppSettings settings, bool full)
        {
            RuntimeSnapshot snapshot = new RuntimeSnapshot();
            snapshot.Time = DateTime.Now;

            List<UdpRow> udpRows = NetTable.ReadUdp();
            List<TcpRow> tcpRows = NetTable.ReadTcp();

            Dictionary<string, AppEntry> configuredByProcess =
                new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (AppEntry app in settings.Apps)
            {
                if (!string.IsNullOrEmpty(app.ProcessName) && !configuredByProcess.ContainsKey(app.ProcessName))
                    configuredByProcess[app.ProcessName] = app;
            }

            Process[] all = Process.GetProcesses();
            _processNames = new Dictionary<int, string>();
            foreach (Process p in all)
            {
                try { _processNames[p.Id] = p.ProcessName; } catch { }
            }
            Dictionary<int, Process> targets = new Dictionary<int, Process>();
            foreach (Process p in all)
            {
                bool match = false;
                try
                {
                    if (configuredByProcess.ContainsKey(p.ProcessName)) match = true;
                    else if (IsHamProcessName(p.ProcessName) && !IsHelperProcess(p.ProcessName)) match = true;
                }
                catch { }
                if (match) targets[p.Id] = p;
                else
                {
                    try { p.Dispose(); } catch { }
                }
            }

            HashSet<int> seenPids = new HashSet<int>();
            foreach (KeyValuePair<int, Process> pair in targets)
            {
                Process p = pair.Value;
                ProcessState state;
                if (!_states.TryGetValue(pair.Key, out state))
                {
                    state = new ProcessState();
                    state.Process = p;
                    state.Name = p.ProcessName;
                    state.LastCpuAt = DateTime.Now;
                    _states[pair.Key] = state;
                }
                else
                {
                    try { state.Process.Dispose(); } catch { }
                    state.Process = p;
                }
                seenPids.Add(pair.Key);

                RuntimeAppInfo info = BuildAppInfo(p, state, udpRows, tcpRows);
                AppEntry configured;
                if (configuredByProcess.TryGetValue(p.ProcessName, out configured))
                {
                    info.Name = configured.Name;
                    info.IsConfigured = true;
                    state.Name = configured.Name;
                }
                else if (string.IsNullOrEmpty(info.Name))
                {
                    info.Name = p.ProcessName;
                    state.Name = info.Name;
                }
                AttachRelatedEndpoints(info, udpRows, tcpRows);

                if (info.HasWindow && !info.Responding)
                {
                    state.NotRespondingCount++;
                    if (state.NotRespondingCount >= 3 && !state.FreezeAlerted)
                    {
                        state.FreezeAlerted = true;
                        info.Status = "无响应，可能卡死";
                        AddAlert(snapshot, "⚠ " + info.Name + " 已连续 " +
                                  (state.NotRespondingCount * 2) + " 秒无响应，可能卡死。");
                    }
                    else
                    {
                        info.Status = "无响应";
                    }
                }
                else
                {
                    state.NotRespondingCount = 0;
                    state.FreezeAlerted = false;
                    if (info.Status == "无响应" || info.Status == "无响应，可能卡死") info.Status = "正常";
                }

                snapshot.Apps.Add(info);
            }

            // Detect processes that disappeared since the previous scan.
            List<int> gone = new List<int>();
            foreach (KeyValuePair<int, ProcessState> pair in _states)
            {
                if (seenPids.Contains(pair.Key)) continue;
                Process process = pair.Value.Process;
                bool exited = false;
                int exitCode = 0;
                try
                {
                    exited = process.HasExited;
                    if (exited) exitCode = process.ExitCode;
                }
                catch { exited = true; }

                if (!exited) continue; // still alive but no longer matched; keep state

                bool abnormal = exitCode != 0 || pair.Value.NotRespondingCount >= 3;
                if (abnormal && DateTime.Now >= MuteAlertsUntil)
                {
                    string reason = exitCode != 0 ? "退出代码 " + exitCode : "退出前长时间无响应";
                    AddAlert(snapshot, "⚠ " + pair.Value.Name + " 可能已崩溃（" + reason + "）。");
                }
                gone.Add(pair.Key);
            }
            foreach (int pid in gone)
            {
                ProcessState state = _states[pid];
                try { state.Process.Dispose(); } catch { }
                _states.Remove(pid);
            }

            // Configured but not running rows.
            foreach (AppEntry app in settings.Apps)
            {
                bool found = false;
                foreach (RuntimeAppInfo info in snapshot.Apps)
                {
                    if (string.Equals(info.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) continue;
                RuntimeAppInfo idle = new RuntimeAppInfo();
                idle.Name = app.Name;
                idle.ProcessName = app.ProcessName;
                idle.Running = false;
                idle.IsConfigured = true;
                idle.Status = app.Enabled ? "未启动" : "未启用";
                snapshot.Apps.Add(idle);
            }

            if (full || DateTime.Now.Subtract(_adiCacheAt).TotalSeconds > 15)
            {
                _adiCache = AdiFinder.FindAll(settings);
                _adiCacheAt = DateTime.Now;
            }
            snapshot.AdiFiles = _adiCache;

            foreach (RuntimeAppInfo app in snapshot.Apps)
            {
                if (!app.Running) continue;
                app.AdiFiles = AdiFinder.Match(app, _adiCache);
            }

            snapshot.Apps.Sort(delegate(RuntimeAppInfo a, RuntimeAppInfo b)
            {
                if (a.Running != b.Running) return a.Running ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            foreach (RuntimeAppInfo app in snapshot.Apps)
            {
                if (app.Running) snapshot.RunningCount++;
                snapshot.UdpCount += app.Udp.Count;
                foreach (TcpConnectionInfo c in app.Tcp) if (c.LikelyCluster) snapshot.ClusterCount++;
            }

            snapshot.RecentAlerts.AddRange(_recentAlerts);
            return snapshot;
        }

        private void AddAlert(RuntimeSnapshot snapshot, string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            snapshot.NewAlerts.Add(line);
            _recentAlerts.Add(line);
            while (_recentAlerts.Count > 8) _recentAlerts.RemoveAt(0);
        }

        private static bool IsHamProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            string name = processName.ToLowerInvariant();
            foreach (string keyword in HamKeywords)
            {
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool IsHelperProcess(string processName)
        {
            string name = (processName ?? "").ToLowerInvariant();
            return name.Contains("udp_daemon") || name.Contains("rigctl") ||
                   name.Contains("jtdxjt9") || name.Contains("wsprd") ||
                   name.Contains("qtwebengine");
        }

        private RuntimeAppInfo BuildAppInfo(Process p, ProcessState state, List<UdpRow> udpRows, List<TcpRow> tcpRows)
        {
            RuntimeAppInfo info = new RuntimeAppInfo();
            info.Running = true;
            info.Pid = p.Id;
            info.ProcessName = p.ProcessName;
            info.Name = p.ProcessName;
            try { info.Path = p.MainModule.FileName; }
            catch { info.Path = ""; }

            try
            {
                info.HasWindow = p.MainWindowHandle != IntPtr.Zero;
                if (info.HasWindow)
                {
                    info.WindowTitle = p.MainWindowTitle;
                    info.Responding = p.Responding;
                }
                else
                {
                    info.Responding = true;
                }
            }
            catch
            {
                info.HasWindow = false;
                info.Responding = true;
            }

            try { info.StartTime = p.StartTime; }
            catch { }
            try { info.MemoryMb = p.WorkingSet64 / (1024 * 1024); }
            catch { }

            try
            {
                TimeSpan cpu = p.TotalProcessorTime;
                if (state.HasCpuSample)
                {
                    double elapsed = DateTime.Now.Subtract(state.LastCpuAt).TotalSeconds;
                    if (elapsed > 0)
                    {
                        double percent = (cpu - state.LastCpu).TotalSeconds / elapsed /
                                         Math.Max(1, Environment.ProcessorCount) * 100.0;
                        info.CpuPercent = Math.Max(0, Math.Min(100, percent));
                    }
                }
                state.LastCpu = cpu;
                state.LastCpuAt = DateTime.Now;
                state.HasCpuSample = true;
            }
            catch { }

            int pid = p.Id;
            foreach (UdpRow row in udpRows)
            {
                if (row.Pid != pid) continue;
                RuntimeEndpoint ep = new RuntimeEndpoint();
                ep.Address = row.Address;
                ep.Port = row.Port;
                ep.Protocol = "UDP";
                if (!info.Udp.Exists(delegate(RuntimeEndpoint x) { return x.Port == ep.Port && x.Address == ep.Address; }))
                    info.Udp.Add(ep);
            }
            info.Udp.Sort(delegate(RuntimeEndpoint a, RuntimeEndpoint b) { return a.Port.CompareTo(b.Port); });

            foreach (TcpRow row in tcpRows)
            {
                if (row.Pid != pid) continue;
                TcpConnectionInfo c = new TcpConnectionInfo();
                c.LocalAddress = row.LocalAddress;
                c.LocalPort = row.LocalPort;
                c.RemoteAddress = row.RemoteAddress;
                c.RemotePort = row.RemotePort;
                c.State = row.State;
                c.LikelyCluster = row.State == "Established" && IsClusterPort(row.RemotePort);
                info.Tcp.Add(c);
            }
            info.Tcp.Sort(delegate(TcpConnectionInfo a, TcpConnectionInfo b)
            {
                if (a.LikelyCluster != b.LikelyCluster) return a.LikelyCluster ? -1 : 1;
                return a.RemotePort.CompareTo(b.RemotePort);
            });

            info.Status = "正常";
            if (!info.HasWindow) info.Status = "后台运行";
            return info;
        }

        private void AttachRelatedEndpoints(RuntimeAppInfo info, List<UdpRow> udpRows, List<TcpRow> tcpRows)
        {
            if (_processNames.Count == 0) return;
            List<string> tokens = new List<string>();
            AddTokens(tokens, info.Name);
            AddTokens(tokens, info.ProcessName);
            if (tokens.Count == 0) return;

            foreach (UdpRow row in udpRows)
            {
                if (row.Pid == info.Pid) continue;
                string owner;
                if (!_processNames.TryGetValue(row.Pid, out owner)) continue;
                if (!MatchesOwner(owner, tokens)) continue;
                bool exists = false;
                foreach (RuntimeEndpoint ep in info.Udp)
                {
                    if (ep.Port == row.Port && ep.Address == row.Address) { exists = true; break; }
                }
                if (exists) continue;
                RuntimeEndpoint added = new RuntimeEndpoint();
                added.Address = row.Address;
                added.Port = row.Port;
                added.Protocol = "UDP";
                info.Udp.Add(added);
            }
            info.Udp.Sort(delegate(RuntimeEndpoint a, RuntimeEndpoint b) { return a.Port.CompareTo(b.Port); });

            foreach (TcpRow row in tcpRows)
            {
                if (row.Pid == info.Pid) continue;
                string owner;
                if (!_processNames.TryGetValue(row.Pid, out owner)) continue;
                if (!MatchesOwner(owner, tokens)) continue;
                bool exists = false;
                foreach (TcpConnectionInfo c in info.Tcp)
                {
                    if (c.LocalPort == row.LocalPort && c.RemotePort == row.RemotePort &&
                        c.RemoteAddress == row.RemoteAddress) { exists = true; break; }
                }
                if (exists) continue;
                TcpConnectionInfo added = new TcpConnectionInfo();
                added.LocalAddress = row.LocalAddress;
                added.LocalPort = row.LocalPort;
                added.RemoteAddress = row.RemoteAddress;
                added.RemotePort = row.RemotePort;
                added.State = row.State;
                added.LikelyCluster = row.State == "Established" && IsClusterPort(row.RemotePort);
                info.Tcp.Add(added);
            }
            info.Tcp.Sort(delegate(TcpConnectionInfo a, TcpConnectionInfo b)
            {
                if (a.LikelyCluster != b.LikelyCluster) return a.LikelyCluster ? -1 : 1;
                return a.RemotePort.CompareTo(b.RemotePort);
            });
        }

        private static void AddTokens(List<string> tokens, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string raw in Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+"))
            {
                if (raw.Length < 3) continue;
                if (raw == "exe" || raw == "app" || raw == "logger") continue;
                if (!tokens.Contains(raw)) tokens.Add(raw);
            }
        }

        private static bool MatchesOwner(string owner, List<string> tokens)
        {
            string lower = (owner ?? "").ToLowerInvariant();
            foreach (string token in tokens)
            {
                if (lower.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool IsClusterPort(int port)
        {
            foreach (int p in ClusterPorts) if (p == port) return true;
            return false;
        }
    }
}
