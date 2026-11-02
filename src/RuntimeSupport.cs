using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HamStartup
{
    internal class UdpRow
    {
        public string Address = "";
        public int Port;
        public int Pid;
    }

    internal class TcpRow
    {
        public string LocalAddress = "";
        public int LocalPort;
        public string RemoteAddress = "";
        public int RemotePort;
        public string State = "";
        public int Pid;
    }

    /// <summary>Reads the Windows UDP/TCP owner tables (no admin required for local endpoints).</summary>
    internal static class NetTable
    {
        private const int AF_INET = 2;
        private const int UDP_TABLE_OWNER_PID = 1;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order,
            int addressFamily, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order,
            int addressFamily, int tableClass, uint reserved);

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint LocalAddress;
            public uint LocalPort;
            public uint OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddress;
            public uint LocalPort;
            public uint RemoteAddress;
            public uint RemotePort;
            public uint OwningPid;
        }

        public static List<UdpRow> ReadUdp()
        {
            List<UdpRow> rows = new List<UdpRow>();
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int size = 0;
                uint result = GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
                if (size <= 0) size = 65536;
                buffer = Marshal.AllocHGlobal(size);
                result = GetExtendedUdpTable(buffer, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
                if (result != 0) return rows;

                int count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                for (int i = 0; i < count; i++)
                {
                    MIB_UDPROW_OWNER_PID row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(
                        IntPtr.Add(rowPtr, i * rowSize), typeof(MIB_UDPROW_OWNER_PID));
                    UdpRow item = new UdpRow();
                    item.Address = AddressText(row.LocalAddress);
                    item.Port = PortNumber(row.LocalPort);
                    item.Pid = (int)row.OwningPid;
                    rows.Add(item);
                }
            }
            catch { }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
            return rows;
        }

        public static List<TcpRow> ReadTcp()
        {
            List<TcpRow> rows = new List<TcpRow>();
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (size <= 0) size = 65536;
                buffer = Marshal.AllocHGlobal(size);
                uint result = GetExtendedTcpTable(buffer, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (result != 0) return rows;

                int count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                for (int i = 0; i < count; i++)
                {
                    MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(
                        IntPtr.Add(rowPtr, i * rowSize), typeof(MIB_TCPROW_OWNER_PID));
                    TcpRow item = new TcpRow();
                    item.LocalAddress = AddressText(row.LocalAddress);
                    item.LocalPort = PortNumber(row.LocalPort);
                    item.RemoteAddress = AddressText(row.RemoteAddress);
                    item.RemotePort = PortNumber(row.RemotePort);
                    item.State = TcpStateText(row.State);
                    item.Pid = (int)row.OwningPid;
                    rows.Add(item);
                }
            }
            catch { }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
            return rows;
        }

        private static int PortNumber(uint raw)
        {
            return ((int)(raw & 0xFF) << 8) | (int)((raw >> 8) & 0xFF);
        }

        private static string AddressText(uint address)
        {
            try
            {
                // The IP helper API stores IPv4 addresses in network byte order in a uint;
                // BitConverter on little-endian Windows yields the correct byte sequence.
                byte[] bytes = BitConverter.GetBytes(address);
                return new IPAddress(bytes).ToString();
            }
            catch { return "0.0.0.0"; }
        }

        private static string TcpStateText(uint state)
        {
            switch (state)
            {
                case 1: return "Closed";
                case 2: return "Listen";
                case 3: return "SynSent";
                case 4: return "SynReceived";
                case 5: return "Established";
                case 6: return "FinWait1";
                case 7: return "FinWait2";
                case 8: return "CloseWait";
                case 9: return "Closing";
                case 10: return "LastAck";
                case 11: return "TimeWait";
                case 12: return "DeleteTCB";
                default: return "State" + state;
            }
        }
    }

    internal static class AdiFinder
    {
        private static DateTime _deadline;

        public static List<AdiFileInfo> FindAll(AppSettings settings)
        {
            List<AdiFileInfo> result = new List<AdiFileInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _deadline = DateTime.Now.AddSeconds(2.5);

            AddKnown(result, seen, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JTDX", "wsjtx_log.adi"));

            string wsjtx = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WSJT-X");
            AddDirectoryFiles(result, seen, wsjtx, 1, "*.adi");
            AddDirectoryFiles(result, seen, wsjtx, 1, "*.adif");

            AddGridTrackerLogs(result, seen);

            List<string> roots = new List<string>();
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "N1MM Logger+"));
            roots.Add(@"D:\电台软件");
            roots.Add(@"D:\桌面");
            foreach (AppEntry app in settings.Apps)
            {
                if (!string.IsNullOrEmpty(app.Directory)) roots.Add(app.Directory);
            }

            foreach (string root in roots)
            {
                if (DateTime.Now > _deadline) break;
                ScanDirectory(result, seen, root, 4, 2200);
            }

            result.Sort(delegate(AdiFileInfo a, AdiFileInfo b) { return b.Modified.CompareTo(a.Modified); });
            if (result.Count > 60) result.RemoveRange(60, result.Count - 60);
            return result;
        }

        public static List<AdiFileInfo> Match(RuntimeAppInfo app, List<AdiFileInfo> all)
        {
            List<AdiFileInfo> matched = new List<AdiFileInfo>();
            string key = ((app.Name ?? "") + " " + (app.ProcessName ?? "") + " " + (app.Path ?? "")).ToLowerInvariant();

            string[] tokens;
            if (key.Contains("jtdx")) tokens = new string[] { "jtdx", "wsjtx_log" };
            else if (key.Contains("wsjtx") || key.Contains("wsjt-x")) tokens = new string[] { "wsjtx", "wsjt-x" };
            else if (key.Contains("n1mm")) tokens = new string[] { "n1mm" };
            else if (key.Contains("qlog")) tokens = new string[] { "qlog" };
            else if (key.Contains("log4om")) tokens = new string[] { "log4om" };
            else if (key.Contains("gridtracker")) tokens = new string[] { "gridtracker", "wsjtx_log", "jtdx" };
            else tokens = new string[] { app.Name == null ? "" : app.Name.ToLowerInvariant() };

            foreach (AdiFileInfo file in all)
            {
                string path = file.Path.ToLowerInvariant();
                foreach (string token in tokens)
                {
                    if (token.Length == 0) continue;
                    if (path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched.Add(file);
                        break;
                    }
                }
                if (matched.Count >= 6) break;
            }
            return matched;
        }

        public static string GuessHint(string path)
        {
            string p = (path ?? "").ToLowerInvariant();
            if (p.Contains("jtdx")) return "JTDX";
            if (p.Contains("wsjtx") || p.Contains("wsjt-x")) return "WSJT-X";
            if (p.Contains("n1mm")) return "N1MM";
            if (p.Contains("qlog")) return "QLog";
            if (p.Contains("log4om")) return "Log4OM";
            if (p.Contains("gridtracker")) return "GridTracker2";
            if (p.Contains("hrd")) return "HRD";
            return "通用";
        }

        private static void AddGridTrackerLogs(List<AdiFileInfo> result, HashSet<string> seen)
        {
            try
            {
                string file = UdpConfig.Gt2JsonPath;
                if (!File.Exists(file)) return;
                string text = File.ReadAllText(file);
                Match match = Regex.Match(text, "\"file\"\\s*:\\s*\"([^\"]+\\.(?:adi|adif))\"",
                    RegexOptions.IgnoreCase);
                while (match.Success)
                {
                    string path = match.Groups[1].Value.Replace("\\\\", "\\");
                    AddKnown(result, seen, path);
                    match = match.NextMatch();
                }
            }
            catch { }
        }

        private static void AddKnown(List<AdiFileInfo> result, HashSet<string> seen, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                string full = Path.GetFullPath(path);
                if (!seen.Add(full)) return;
                FileInfo info = new FileInfo(full);
                AdiFileInfo item = new AdiFileInfo();
                item.Path = full;
                item.Size = info.Length;
                item.Modified = info.LastWriteTime;
                item.AppHint = GuessHint(full);
                result.Add(item);
            }
            catch { }
        }

        private static void AddDirectoryFiles(List<AdiFileInfo> result, HashSet<string> seen,
            string dir, int depth, string pattern)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    AddKnown(result, seen, file);
                if (depth > 1)
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                        AddDirectoryFiles(result, seen, sub, depth - 1, pattern);
                }
            }
            catch { }
        }

        private static void ScanDirectory(List<AdiFileInfo> result, HashSet<string> seen,
            string root, int maxDepth, int maxDirs)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            Queue<KeyValuePair<string, int>> queue = new Queue<KeyValuePair<string, int>>();
            queue.Enqueue(new KeyValuePair<string, int>(root, 0));
            int visited = 0;

            while (queue.Count > 0 && visited < maxDirs && DateTime.Now < _deadline)
            {
                KeyValuePair<string, int> node = queue.Dequeue();
                visited++;
                try
                {
                    foreach (string file in Directory.GetFiles(node.Key, "*.adi"))
                        AddKnown(result, seen, file);
                    foreach (string file in Directory.GetFiles(node.Key, "*.adif"))
                        AddKnown(result, seen, file);
                }
                catch { }

                if (node.Value >= maxDepth) continue;
                try
                {
                    foreach (string sub in Directory.GetDirectories(node.Key))
                    {
                        string name = Path.GetFileName(sub);
                        if (name.StartsWith("$") || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Windows", StringComparison.OrdinalIgnoreCase)) continue;
                        queue.Enqueue(new KeyValuePair<string, int>(sub, node.Value + 1));
                    }
                }
                catch { }
            }
        }
    }
}
