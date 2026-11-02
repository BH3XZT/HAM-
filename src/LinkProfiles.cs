using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace HamStartup
{
    public class LinkProfile
    {
        public string Id = "";
        public string Name = "";
        public string Protocol = "UDP";
        public string AppMatch = "";
        public string LocalAddress = "";
        public int LocalPort;
        public string RemoteAddress = "";
        public int RemotePort;
        public string Direction = "";
        public string Notes = "";
        public bool Enabled = true;
        public bool AutoMatch = true;

        public string EndpointText
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(LocalAddress)) sb.Append(LocalAddress);
                if (LocalPort > 0) sb.Append(':').Append(LocalPort);
                if (!string.IsNullOrEmpty(RemoteAddress) || RemotePort > 0)
                {
                    if (sb.Length > 0) sb.Append("  →  ");
                    sb.Append(string.IsNullOrEmpty(RemoteAddress) ? "*" : RemoteAddress);
                    if (RemotePort > 0) sb.Append(':').Append(RemotePort);
                }
                return sb.Length == 0 ? "—" : sb.ToString();
            }
        }
    }

    public class LinkMatchResult
    {
        public LinkProfile Profile;
        public RuntimeAppInfo App;
        public bool Matched;
        public string Detail = "";
    }

    public static class LinkProfileCatalog
    {
        public static List<LinkProfile> CreateDefaults()
        {
            List<LinkProfile> list = new List<LinkProfile>();

            LinkProfile p = new LinkProfile();
            p.Id = "link1";
            p.Name = "JTDX → GridTracker2 / QLog（UDP 组播）";
            p.Protocol = "UDP";
            p.AppMatch = "JTDX";
            p.LocalAddress = "239.255.0.1";
            p.LocalPort = 2237;
            p.Direction = "发送";
            p.Notes = "JTDX UDP1，实验室推荐组播地址";
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link2";
            p.Name = "GridTracker2 监听 UDP";
            p.Protocol = "UDP";
            p.AppMatch = "GridTracker2";
            p.LocalAddress = "239.255.0.1";
            p.LocalPort = 2237;
            p.Direction = "监听";
            p.Notes = "地图/台站数据接收";
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link3";
            p.Name = "QLog 监听 UDP";
            p.Protocol = "UDP";
            p.AppMatch = "QLog";
            p.LocalAddress = "239.255.0.1";
            p.LocalPort = 2237;
            p.Direction = "监听";
            p.Notes = "通联日志接收";
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link4";
            p.Name = "JTDX UDP2 / N1MM 等";
            p.Protocol = "UDP";
            p.AppMatch = "JTDX";
            p.LocalAddress = "127.0.0.1";
            p.LocalPort = 2333;
            p.Direction = "发送";
            p.Notes = "第二路 UDP 可选";
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link5";
            p.Name = "DX Cluster（TCP）";
            p.Protocol = "TCP";
            p.AppMatch = "N1MM";
            p.RemotePort = 7300;
            p.Direction = "连接";
            p.Notes = "N1MM/日志软件的 DX 集群连接，常见端口 7300";
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link6";
            p.Name = "串口 CAT（示例）";
            p.Protocol = "串口";
            p.AppMatch = "JTDX";
            p.LocalAddress = "COM10";
            p.Direction = "串口";
            p.Notes = "电台 CAT 串口；按实际 COM 修改";
            p.Enabled = false;
            list.Add(p);

            p = new LinkProfile();
            p.Id = "link7";
            p.Name = "自定义连接（其他）";
            p.Protocol = "其他";
            p.AppMatch = "";
            p.Direction = "其他";
            p.Notes = "其他检测到的连接方式";
            p.Enabled = false;
            list.Add(p);

            return list;
        }
    }

    public static class LinkMatcher
    {
        public static List<LinkMatchResult> Match(AppSettings settings, RuntimeSnapshot snapshot)
        {
            List<LinkMatchResult> results = new List<LinkMatchResult>();
            if (settings == null || snapshot == null) return results;

            foreach (LinkProfile profile in settings.Links)
            {
                if (profile == null) continue;
                LinkMatchResult result = new LinkMatchResult();
                result.Profile = profile;
                result.Matched = false;
                result.Detail = "未检测到";

                if (!profile.Enabled)
                {
                    result.Detail = "未启用";
                    results.Add(result);
                    continue;
                }

                RuntimeAppInfo app = FindApp(snapshot, profile.AppMatch);
                if (profile.Protocol == "UDP")
                {
                    if (app != null)
                    {
                        foreach (RuntimeEndpoint ep in app.Udp)
                        {
                            if (ep.Port == profile.LocalPort || ep.Port == profile.RemotePort)
                            {
                                result.App = app;
                                result.Matched = true;
                                result.Detail = "UDP " + ep.Port + " 已检测";
                                break;
                            }
                        }
                    }
                }
                else if (profile.Protocol == "TCP")
                {
                    if (app != null)
                    {
                        foreach (TcpConnectionInfo c in app.Tcp)
                        {
                            if (c.LocalPort == profile.LocalPort || c.RemotePort == profile.RemotePort)
                            {
                                result.App = app;
                                result.Matched = true;
                                result.Detail = (c.LikelyCluster ? "DX 集群 TCP " : "TCP ") +
                                                (c.RemotePort > 0 ? c.RemotePort : c.LocalPort) + " 已连接";
                                break;
                            }
                        }
                    }
                }
                else if (profile.Protocol == "串口")
                {
                    bool exists = false;
                    try
                    {
                        string port = (profile.LocalAddress ?? "").ToUpperInvariant().Replace("COM", "").Trim();
                        foreach (string name in SerialPort.GetPortNames())
                        {
                            if (name.ToUpperInvariant().Replace("COM", "").Trim() == port)
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (exists)
                    {
                        result.App = app;
                        result.Matched = true;
                        result.Detail = "串口 " + profile.LocalAddress + " 存在";
                    }
                    else result.Detail = "串口 " + profile.LocalAddress + " 未检测到";
                }
                else
                {
                    if (app != null)
                    {
                        result.App = app;
                        result.Matched = true;
                        result.Detail = "自定义连接 / 软件运行中";
                    }
                }

                if (app != null && string.IsNullOrEmpty(result.Detail)) result.Detail = app.Name + " 运行中";
                results.Add(result);
            }
            return results;
        }

        private static RuntimeAppInfo FindApp(RuntimeSnapshot snapshot, string appMatch)
        {
            if (snapshot == null) return null;
            string key = (appMatch ?? "").Trim().ToLowerInvariant();
            if (key.Length == 0) return snapshot.RunningCount > 0 ? snapshot.Apps[0] : null;

            foreach (RuntimeAppInfo app in snapshot.Apps)
            {
                if (!app.Running) continue;
                string name = ((app.Name ?? "") + " " + (app.ProcessName ?? "")).ToLowerInvariant();
                if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return app;
            }
            return null;
        }
    }

    public static class LinkParameterExport
    {
        public static void Export(string path, AppSettings settings, RuntimeSnapshot snapshot,
            List<LinkMatchResult> matches)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"tool\": \"HAM 工作台\",");
            sb.AppendLine("  \"version\": \"1.3\",");
            sb.AppendLine("  \"exportedAt\": \"" + Json(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "\",");
            sb.AppendLine("  \"linkProfiles\": [");
            for (int i = 0; i < settings.Links.Count; i++)
            {
                LinkProfile p = settings.Links[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"id\": \"" + Json(p.Id) + "\",");
                sb.AppendLine("      \"name\": \"" + Json(p.Name) + "\",");
                sb.AppendLine("      \"protocol\": \"" + Json(p.Protocol) + "\",");
                sb.AppendLine("      \"app\": \"" + Json(p.AppMatch) + "\",");
                sb.AppendLine("      \"local\": \"" + Json(p.LocalAddress) + "\",");
                sb.AppendLine("      \"localPort\": " + p.LocalPort + ",");
                sb.AppendLine("      \"remote\": \"" + Json(p.RemoteAddress) + "\",");
                sb.AppendLine("      \"remotePort\": " + p.RemotePort + ",");
                sb.AppendLine("      \"direction\": \"" + Json(p.Direction) + "\",");
                sb.AppendLine("      \"enabled\": " + (p.Enabled ? "true" : "false") + ",");
                sb.AppendLine("      \"notes\": \"" + Json(p.Notes) + "\"");
                sb.AppendLine("    }" + (i < settings.Links.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"detected\": [");
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.Apps.Count; i++)
                {
                    RuntimeAppInfo app = snapshot.Apps[i];
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"name\": \"" + Json(app.Name) + "\",");
                    sb.AppendLine("      \"process\": \"" + Json(app.ProcessName) + "\",");
                    sb.AppendLine("      \"pid\": " + app.Pid + ",");
                    sb.AppendLine("      \"running\": " + (app.Running ? "true" : "false") + ",");
                    sb.AppendLine("      \"status\": \"" + Json(app.Status) + "\",");
                    sb.AppendLine("      \"udp\": \"" + Json(JoinUdp(app)) + "\",");
                    sb.AppendLine("      \"tcp\": \"" + Json(JoinTcp(app)) + "\",");
                    sb.AppendLine("      \"adi\": \"" + Json(JoinAdi(app)) + "\"");
                    sb.AppendLine("    }" + (i < snapshot.Apps.Count - 1 ? "," : ""));
                }
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"matches\": [");
            if (matches != null)
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    LinkMatchResult m = matches[i];
                    sb.AppendLine("    { \"profile\": \"" + Json(m.Profile.Name) + "\", \"matched\": " +
                                  (m.Matched ? "true" : "false") + ", \"detail\": \"" + Json(m.Detail) + "\" }" +
                                  (i < matches.Count - 1 ? "," : ""));
                }
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string JoinUdp(RuntimeAppInfo app)
        {
            StringBuilder sb = new StringBuilder();
            foreach (RuntimeEndpoint ep in app.Udp)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(ep.Address).Append(':').Append(ep.Port);
            }
            return sb.ToString();
        }

        private static string JoinTcp(RuntimeAppInfo app)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TcpConnectionInfo c in app.Tcp)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(c.RemoteAddress).Append(':').Append(c.RemotePort).Append(' ').Append(c.State);
            }
            return sb.ToString();
        }

        private static string JoinAdi(RuntimeAppInfo app)
        {
            StringBuilder sb = new StringBuilder();
            foreach (AdiFileInfo f in app.AdiFiles)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(f.Path);
            }
            return sb.ToString();
        }

        private static string Json(string text)
        {
            if (text == null) return "";
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
