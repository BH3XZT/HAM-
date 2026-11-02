using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HamStartup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
