using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
            string screenshotPath = null;
            int screenshotTab = 0;
            int screenshotScroll = 0;

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
            }

            string configPath = AppSettings.ResolveConfigPath(requestedConfig);
            try
            {
                configPath = Path.GetFullPath(configPath);
            }
            catch { }

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                RunScreenshot(configPath, screenshotPath, screenshotTab, screenshotScroll);
                return;
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
                MessageBox.Show("程序发生异常：\n" + e.Exception.Message, "HAM 启动台",
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
                MessageBox.Show("启动失败：\n" + ex.Message, "HAM 启动台",
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

        private static void RunScreenshot(string configPath, string path, int tab, int scroll)
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
                    Pump(1600);
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
