using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HamStartup
{
    internal static class Program
    {
        /// <summary>产品版本号（与 AssemblyInfo.cs 保持一致）。</summary>
        public const string ProductVersion = "1.6.0";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (TryHandleInfoArgs(args)) return;

            string requestedConfig = null;
            bool autoStart = false;
            bool quitAfterStart = false;
            bool noElevate = false;
            string screenshotPath = null;
            int screenshotTab = 0;
            int screenshotScroll = 0;
            int screenshotWait = 1600;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
                {
                    autoStart = true;
                }
                else if (arg.Equals("--quit-after-start", StringComparison.OrdinalIgnoreCase))
                {
                    quitAfterStart = true;
                }
                else if (arg.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase))
                {
                    noElevate = true;
                }
                else if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    requestedConfig = args[i + 1];
                    i++;
                }
                else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    requestedConfig = arg.Substring("--config=".Length);
                }
                else if (arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    screenshotPath = args[i + 1];
                    i++;
                }
                else if (arg.Equals("--tab", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out screenshotTab);
                    i++;
                }
                else if (arg.Equals("--scroll", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out screenshotScroll);
                    i++;
                }
                else if (arg.Equals("--wait", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out screenshotWait);
                    i++;
                }
            }

            string configPath = AppSettings.ResolveConfigPath(requestedConfig);
            try
            {
                configPath = Path.GetFullPath(configPath);
            }
            catch { }

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                RunScreenshot(configPath, screenshotPath, screenshotTab, screenshotScroll, screenshotWait);
                return;
            }

            if (!noElevate && SecurityUtil.IsLowIntegrity())
            {
                DialogResult result = MessageBox.Show(
                    "检测到程序正以“受限权限（低完整性）”运行。\n\n" +
                    "在这种环境下：\n" +
                    "· 设置可能无法写入用户目录（程序已自动改用便携保存）；\n" +
                    "· QLog 等软件可能无法读取自己的配置，从而显示全新配置。\n\n" +
                    "是否以管理员身份重新启动，以恢复正常权限？",
                    "权限受限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    if (RelaunchElevated(args)) return;
                }
            }

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(configPath), "error.log"),
                        DateTime.Now + "  " + e.Exception + Environment.NewLine);
                }
                catch { }
                MessageBox.Show("程序发生异常：\n" + e.Exception.Message, "HAM 工作台",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {
                Application.Run(new MainForm(configPath, autoStart, quitAfterStart));
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(configPath), "error.log"),
                        DateTime.Now + "  " + ex + Environment.NewLine);
                }
                catch { }
                MessageBox.Show("启动失败：\n" + ex.Message, "HAM 工作台",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Pump(int milliseconds)
        {
            DateTime until = DateTime.Now.AddMilliseconds(milliseconds);
            while (DateTime.Now < until)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
        }

        /// <summary>
        /// 处理 --help / --version：写入父控制台（如果是从命令行启动），
        /// 双击启动没有控制台时用弹窗显示。
        /// </summary>
        private static bool TryHandleInfoArgs(string[] args)
        {
            bool help = false;
            bool version = false;
            foreach (string arg in args)
            {
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                    help = true;
                else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
                    version = true;
            }
            if (!help && !version) return false;

            StringBuilder text = new StringBuilder();
            if (version)
            {
                text.AppendLine("HAM 工作台 " + ProductVersion);
                try
                {
                    text.AppendLine("文件版本：" + typeof(Program).Assembly.GetName().Version.ToString());
                }
                catch { }
            }
            if (help)
            {
                text.AppendLine("用法：HamStartup.exe [选项]");
                text.AppendLine();
                text.AppendLine("  --help, -h              显示本帮助");
                text.AppendLine("  --version, -v           显示版本号");
                text.AppendLine("  --autostart             打开后自动开始启动流程");
                text.AppendLine("  --quit-after-start      启动流程结束后退出");
                text.AppendLine("  --config <路径>         使用指定的 settings.ini");
                text.AppendLine("  --no-elevate            受限权限时不弹“以管理员身份重启”提示");
                text.AppendLine("  --screenshot <png>      截图模式（用于生成发布截图）");
                text.AppendLine("  --tab <0-3>             截图页：0=启动顺序 1=连接联动 2=运行监测 3=授时");
                text.AppendLine("  --scroll <y>            截图时滚动连接联动页");
                text.AppendLine("  --wait <毫秒>           截图前等待时间（默认 1600）");
                text.AppendLine();
                text.AppendLine("不带参数时打开图形界面；所有设置自动保存。");
            }
            WriteInfoText(text.ToString());
            return true;
        }

        private static void WriteInfoText(string text)
        {
            bool attached = false;
            try { attached = AttachConsole(-1); } catch { }
            bool hasConsole = false;
            try { hasConsole = GetConsoleWindow() != IntPtr.Zero; } catch { }
            bool redirected = false;
            try { redirected = Console.IsOutputRedirected; } catch { }

            if (attached || hasConsole || redirected)
            {
                try
                {
                    Console.Out.Write(text);
                    Console.Out.Flush();
                }
                catch { }
            }
            if (attached)
            {
                try { FreeConsole(); } catch { }
            }

            if (!hasConsole && !redirected)
            {
                MessageBox.Show(text, "HAM 工作台 " + ProductVersion,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static bool RelaunchElevated(string[] args)
        {
            try
            {
                StringBuilder command = new StringBuilder();
                foreach (string arg in args)
                {
                    if (arg.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase)) continue;
                    if (command.Length > 0) command.Append(' ');
                    if (arg.IndexOf(' ') >= 0 || arg.IndexOf('"') >= 0)
                    {
                        command.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
                    }
                    else
                    {
                        command.Append(arg);
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = command.ToString();
                psi.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法以管理员身份重新启动：\n" + ex.Message +
                                "\n\n请关闭程序，然后在资源管理器中双击 HamStartup.exe 运行。",
                    "重新启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private static void RunScreenshot(string configPath, string path, int tab, int scroll, int wait)
        {
            try
            {
                path = Path.GetFullPath(path);
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using (MainForm form = new MainForm(configPath, false, false))
                {
                    form.FormBorderStyle = FormBorderStyle.None;
                    form.Show();
                    form.ShowTab(tab);
                    Pump(500);
                    if (tab == 1 && scroll > 0) form.ScrollUdp(scroll);
                    Pump(Math.Max(500, wait));
                    form.Refresh();
                    using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        bitmap.Save(path, ImageFormat.Png);
                    }
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(configPath), "error.log"),
                        DateTime.Now + "  screenshot: " + ex + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
