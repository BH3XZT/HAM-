using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HamStartup
{
    public class JtdxUdpState
    {
        public bool Exists;
        public string Path = "";
        public string Udp1Addr = "";
        public int Udp1Port;
        public string Udp2Addr = "";
        public int Udp2Port;
        public bool AcceptRequests;
        public string Error = "";

        public bool IsMatch(UdpSettings u)
        {
            return Exists && Udp1Addr == u.JtdxUdp1Addr && Udp1Port == u.JtdxUdp1Port &&
                   Udp2Addr == u.JtdxUdp2Addr && Udp2Port == u.JtdxUdp2Port &&
                   AcceptRequests == u.JtdxAcceptRequests;
        }
    }

    public class Gt2UdpState
    {
        public bool Exists;
        public string Path = "";
        public string WsjtAddr = "";
        public int WsjtPort;
        public bool Multicast;
        public string Error = "";

        public bool IsMatch(UdpSettings u)
        {
            return Exists && WsjtAddr == u.Gt2Addr && WsjtPort == u.Gt2Port && Multicast == u.Gt2Multicast;
        }
    }

    public class QlogUdpState
    {
        public bool Exists;
        public string Path = "";
        public int Port;
        public string MulticastAddr = "";
        public bool Join;
        public int Ttl;
        public string ForwardAddr = "";
        public string Error = "";

        public bool IsMatch(UdpSettings u)
        {
            return Exists && Port == u.QlogPort &&
                   MulticastAddr == u.QlogAddr && Join == u.QlogJoin && Ttl == u.QlogTtl;
        }
    }

    public class UdpSnapshot
    {
        public JtdxUdpState Jtdx = new JtdxUdpState();
        public Gt2UdpState Gt2 = new Gt2UdpState();
        public QlogUdpState Qlog = new QlogUdpState();

        public List<string> Mismatches(UdpSettings u)
        {
            List<string> issues = new List<string>();

            if (!Jtdx.Exists)
                issues.Add("未找到 JTDX 配置文件");
            else if (!Jtdx.IsMatch(u))
                issues.Add(string.Format("JTDX：UDP1 {0}:{1} / UDP2 {2}:{3} / 接受请求 {4}",
                    Jtdx.Udp1Addr, Jtdx.Udp1Port, Jtdx.Udp2Addr, Jtdx.Udp2Port, Jtdx.AcceptRequests ? "开" : "关"));

            if (!Gt2.Exists)
                issues.Add("未找到 GridTracker2 配置文件");
            else if (!Gt2.IsMatch(u))
                issues.Add(string.Format("GridTracker2：{0}:{1} / 组播 {2}",
                    Gt2.WsjtAddr, Gt2.WsjtPort, Gt2.Multicast ? "开" : "关"));

            if (!Qlog.Exists)
                issues.Add("未找到 QLog 数据库");
            else if (!Qlog.IsMatch(u))
                issues.Add(string.Format("QLog：{0}:{1} / 加入组播 {2} / TTL {3}",
                    Qlog.MulticastAddr, Qlog.Port, Qlog.Join ? "开" : "关", Qlog.Ttl));

            return issues;
        }

        public bool AllMatch(UdpSettings u)
        {
            return Jtdx.IsMatch(u) && Gt2.IsMatch(u) && Qlog.IsMatch(u);
        }
    }

    /// <summary>Reads and writes the UDP-related settings of JTDX, GridTracker2 and QLog.</summary>
    public static class UdpConfig
    {
        public static string JtdxIniPath
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("HAMSTARTUP_CONFIG_ROOT");
                if (!string.IsNullOrEmpty(root)) return Path.Combine(root, "JTDX", "JTDX.ini");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JTDX", "JTDX.ini");
            }
        }

        public static string Gt2JsonPath
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("HAMSTARTUP_CONFIG_ROOT");
                if (!string.IsNullOrEmpty(root)) return Path.Combine(root, "GridTracker2", "Ginternal", "app-settings.json");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GridTracker2", "Ginternal", "app-settings.json");
            }
        }

        public static string QlogDbPath
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("HAMSTARTUP_CONFIG_ROOT");
                if (!string.IsNullOrEmpty(root)) return Path.Combine(root, "hamradio", "QLog", "qlog.db");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "hamradio", "QLog", "qlog.db");
            }
        }

        public static UdpSnapshot ReadAll()
        {
            UdpSnapshot snap = new UdpSnapshot();
            snap.Jtdx = ReadJtdx();
            snap.Gt2 = ReadGt2();
            snap.Qlog = ReadQlog();
            return snap;
        }

        public static JtdxUdpState ReadJtdx()
        {
            JtdxUdpState state = new JtdxUdpState();
            state.Path = JtdxIniPath;
            state.Exists = File.Exists(state.Path);
            if (!state.Exists) return state;
            try
            {
                IniFile ini = IniFile.Load(state.Path);
                state.Udp1Addr = ini.Get("Configuration", "UDPServer", "239.255.0.1");
                state.Udp1Port = ini.GetInt("Configuration", "UDPServerPort", 2237);
                state.Udp2Addr = ini.Get("Configuration", "UDP2Server", "127.0.0.1");
                state.Udp2Port = ini.GetInt("Configuration", "UDP2ServerPort", 2333);
                state.AcceptRequests = ini.GetBool("Configuration", "AcceptUDPRequests", true);
            }
            catch (Exception ex)
            {
                state.Exists = false;
                state.Error = ex.Message;
            }
            return state;
        }

        public static Gt2UdpState ReadGt2()
        {
            Gt2UdpState state = new Gt2UdpState();
            state.Path = Gt2JsonPath;
            state.Exists = File.Exists(state.Path);
            if (!state.Exists) return state;
            try
            {
                string text = File.ReadAllText(state.Path);
                string addr = JsonString(text, "wsjtIP");
                string port = JsonString(text, "wsjtUdpPort");
                string multicast = JsonBool(text, "multicast");

                state.WsjtAddr = addr ?? "";
                int p;
                state.WsjtPort = int.TryParse(port, out p) ? p : 0;
                state.Multicast = multicast != null && multicast.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                state.Exists = false;
                state.Error = ex.Message;
            }
            return state;
        }

        public static QlogUdpState ReadQlog()
        {
            QlogUdpState state = new QlogUdpState();
            state.Path = QlogDbPath;
            state.Exists = File.Exists(state.Path);
            if (!state.Exists) return state;
            try
            {
                string[] keys = new string[]
                {
                    "network/listener/wsjtx/port",
                    "network/listener/wsjtx/multicast/addr",
                    "network/listener/wsjtx/multicast/join",
                    "network/listener/wsjtx/multicast/ttl",
                    "network/forwarder/wsjtx/addrs"
                };
                string error;
                Dictionary<string, string> values = Sqlite3.ReadStrings(state.Path, keys, out error);
                if (values == null)
                {
                    state.Exists = false;
                    state.Error = error ?? "无法读取 QLog 数据库";
                    return state;
                }

                string text;
                int number;
                state.Port = values.TryGetValue(keys[0], out text) && int.TryParse(text, out number) ? number : 2237;

                if (values.TryGetValue(keys[1], out text)) state.MulticastAddr = text;
                else state.MulticastAddr = "239.255.0.1";

                if (values.TryGetValue(keys[2], out text)) state.Join = text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
                else state.Join = true;

                if (values.TryGetValue(keys[3], out text) && int.TryParse(text, out number)) state.Ttl = number;
                else state.Ttl = 1;

                if (values.TryGetValue(keys[4], out text)) state.ForwardAddr = text;
                else state.ForwardAddr = "127.0.0.1";
            }
            catch (Exception ex)
            {
                state.Exists = false;
                state.Error = ex.Message;
            }
            return state;
        }

        public static void ApplyJtdx(UdpSettings u, string backupDir)
        {
            string path = JtdxIniPath;
            if (!File.Exists(path)) throw new FileNotFoundException("未找到 JTDX 配置文件", path);
            BackupTo(path, backupDir);

            Dictionary<string, string> updates = new Dictionary<string, string>();
            updates["UDPServer"] = u.JtdxUdp1Addr;
            updates["UDPServerPort"] = u.JtdxUdp1Port.ToString();
            updates["UDP2Server"] = u.JtdxUdp2Addr;
            updates["UDP2ServerPort"] = u.JtdxUdp2Port.ToString();
            updates["AcceptUDPRequests"] = u.JtdxAcceptRequests ? "true" : "false";

            IniSectionEditor.UpdateKeys(path, "Configuration", updates);

            JtdxUdpState check = ReadJtdx();
            if (!check.IsMatch(u))
                throw new InvalidOperationException("JTDX 配置写入后校验失败，请检查 " + path);
        }

        public static void ApplyGt2(UdpSettings u, string backupDir)
        {
            string path = Gt2JsonPath;
            if (!File.Exists(path)) throw new FileNotFoundException("未找到 GridTracker2 配置文件", path);
            BackupTo(path, backupDir);

            byte[] raw = File.ReadAllBytes(path);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string text = File.ReadAllText(path);

            if (JsonString(text, "wsjtIP") == null ||
                JsonString(text, "wsjtUdpPort") == null ||
                JsonBool(text, "multicast") == null)
            {
                throw new InvalidOperationException("没有在 GridTracker2 配置中找到 wsjtIP / wsjtUdpPort / multicast 字段");
            }

            string updated = ReplaceJsonString(text, "wsjtIP", u.Gt2Addr);
            updated = ReplaceJsonString(updated, "wsjtUdpPort", u.Gt2Port.ToString());
            updated = ReplaceJsonBool(updated, "multicast", u.Gt2Multicast);

            if (updated == text) return;

            File.WriteAllText(path, updated, new UTF8Encoding(bom));

            Gt2UdpState check = ReadGt2();
            if (!check.IsMatch(u))
                throw new InvalidOperationException("GridTracker2 配置写入后校验失败，请检查 " + path);
        }

        public static void ApplyQlog(UdpSettings u, string backupDir)
        {
            string path = QlogDbPath;
            if (!File.Exists(path)) throw new FileNotFoundException("未找到 QLog 数据库", path);
            if (ProcessUtil.IsProcessNameRunning("qlog"))
                throw new InvalidOperationException("QLog 正在运行，请先关闭 QLog 再写入配置。");

            BackupTo(path, backupDir);

            Dictionary<string, string> values = new Dictionary<string, string>();
            values["network/listener/wsjtx/port"] = u.QlogPort.ToString();
            values["network/listener/wsjtx/multicast/addr"] = u.QlogAddr;
            values["network/listener/wsjtx/multicast/join"] = u.QlogJoin ? "1" : "0";
            values["network/listener/wsjtx/multicast/ttl"] = u.QlogTtl.ToString();
            values["network/forwarder/wsjtx/addrs"] = u.QlogForwardAddr;

            string error;
            if (!Sqlite3.WriteStrings(path, values, out error))
                throw new InvalidOperationException("写入 QLog 数据库失败：" + error);

            QlogUdpState check = ReadQlog();
            if (!check.IsMatch(u))
                throw new InvalidOperationException("QLog 配置写入后校验失败，请检查 " + path);
        }

        public static string CreateBackupDir(AppSettings settings)
        {
            string baseDir = Path.Combine(Path.GetDirectoryName(settings.ConfigPath), "backups");
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = Path.Combine(baseDir, stamp);
            int index = 1;
            while (Directory.Exists(dir))
            {
                dir = Path.Combine(baseDir, stamp + "_" + index);
                index++;
            }
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string BackupTo(string file, string backupDir)
        {
            if (!File.Exists(file)) return null;
            string dest = Path.Combine(backupDir, Path.GetFileName(file));
            File.Copy(file, dest, true);

            // SQLite may keep sidecar files; copy them too so the backup is consistent.
            foreach (string suffix in new string[] { "-wal", "-shm", "-journal" })
            {
                try
                {
                    string sidecar = file + suffix;
                    if (File.Exists(sidecar))
                    {
                        File.Copy(sidecar, dest + suffix, true);
                    }
                }
                catch { }
            }
            return dest;
        }

        public static string DescribeListeners(int port)
        {
            try
            {
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] endpoints = properties.GetActiveUdpListeners();
                int count = 0;
                foreach (IPEndPoint ep in endpoints)
                {
                    if (ep.Port == port) count++;
                }
                if (count == 0) return string.Format("当前没有程序监听 UDP {0} 端口", port);
                return string.Format("当前有 {0} 个 UDP 端点监听 {1} 端口（JTDX / GridTracker2 / QLog 都可能占用）", count, port);
            }
            catch
            {
                return "";
            }
        }

        private static string JsonString(string text, string key)
        {
            Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string JsonBool(string text, string key)
        {
            Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ReplaceJsonString(string text, string key, string value)
        {
            string escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            Regex regex = new Regex("(\"" + Regex.Escape(key) + "\"\\s*:\\s*\")[^\"]*(\")");
            return regex.Replace(text,
                delegate(Match m) { return m.Groups[1].Value + escaped + m.Groups[2].Value; }, 1);
        }

        private static string ReplaceJsonBool(string text, string key, bool value)
        {
            Regex regex = new Regex("(\"" + Regex.Escape(key) + "\"\\s*:\\s*)(true|false)", RegexOptions.IgnoreCase);
            return regex.Replace(text,
                delegate(Match m) { return m.Groups[1].Value + (value ? "true" : "false"); }, 1);
        }
    }

    /// <summary>Minimal SQLite access through the winsqlite3.dll shipped with Windows 10/11.</summary>
    internal static class Sqlite3
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private const int SQLITE_OPEN_URI = 0x00000040;

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr errorMessage);

        public static Dictionary<string, string> ReadStrings(string path, string[] keys, out string error)
        {
            error = null;
            IntPtr db;
            string uri;
            try
            {
                uri = new Uri(path).AbsoluteUri + "?mode=ro&immutable=1";
            }
            catch
            {
                uri = path;
            }
            int rc = sqlite3_open_v2(NullTerminated(uri), out db, SQLITE_OPEN_READONLY | SQLITE_OPEN_URI, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                error = "无法打开 QLog 数据库（错误 " + rc + "）";
                if (db != IntPtr.Zero) sqlite3_close(db);
                return null;
            }

            try
            {
                sqlite3_busy_timeout(db, 1500);
                StringBuilder sql = new StringBuilder("SELECT name,value FROM log_param WHERE name IN (");
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0) sql.Append(',');
                    sql.Append(Quote(keys[i]));
                }
                sql.Append(')');

                IntPtr stmt;
                rc = sqlite3_prepare_v2(db, NullTerminated(sql.ToString()), -1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK)
                {
                    error = ErrorMessage(db);
                    return null;
                }

                Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        string name = Utf8(sqlite3_column_text(stmt, 0));
                        string value = Utf8(sqlite3_column_text(stmt, 1));
                        if (name != null) result[name] = value;
                    }
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
                return result;
            }
            finally
            {
                sqlite3_close(db);
            }
        }

        public static bool WriteStrings(string path, Dictionary<string, string> values, out string error)
        {
            error = null;
            IntPtr db;
            int rc = sqlite3_open_v2(NullTerminated(path), out db, SQLITE_OPEN_READWRITE, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                error = "无法打开 QLog 数据库（错误 " + rc + "）";
                if (db != IntPtr.Zero) sqlite3_close(db);
                return false;
            }

            try
            {
                sqlite3_busy_timeout(db, 2000);
                if (!Exec(db, "BEGIN IMMEDIATE;", out error)) return false;

                bool ok = true;
                foreach (KeyValuePair<string, string> kv in values)
                {
                    string insert = string.Format(
                        "INSERT OR IGNORE INTO log_param(name,value) SELECT {0},{1} WHERE NOT EXISTS (SELECT 1 FROM log_param WHERE name={0});",
                        Quote(kv.Key), Quote(kv.Value));
                    if (!Exec(db, insert, out error)) { ok = false; break; }

                    string update = string.Format("UPDATE log_param SET value={0} WHERE name={1};",
                        Quote(kv.Value), Quote(kv.Key));
                    if (!Exec(db, update, out error)) { ok = false; break; }
                }

                if (!ok)
                {
                    string rollbackError;
                    Exec(db, "ROLLBACK;", out rollbackError);
                    return false;
                }

                if (!Exec(db, "COMMIT;", out error)) return false;
                return true;
            }
            finally
            {
                sqlite3_close(db);
            }
        }

        private static bool Exec(IntPtr db, string sql, out string error)
        {
            error = null;
            IntPtr errMsg;
            int rc = sqlite3_exec(db, NullTerminated(sql), IntPtr.Zero, IntPtr.Zero, out errMsg);
            if (rc == SQLITE_OK) return true;
            error = errMsg != IntPtr.Zero ? Utf8(errMsg) : ErrorMessage(db);
            if (errMsg != IntPtr.Zero) sqlite3_free(errMsg);
            return false;
        }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_free(IntPtr pointer);

        private static string ErrorMessage(IntPtr db)
        {
            return Utf8(sqlite3_errmsg(db));
        }

        private static string Quote(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private static byte[] NullTerminated(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            byte[] result = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        private static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length++;
            if (length == 0) return "";
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
