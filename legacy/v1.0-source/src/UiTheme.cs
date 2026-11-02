using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HamStartup
{
    public static class UiTheme
    {
        public static readonly Color Navy = Color.FromArgb(14, 26, 43);
        public static readonly Color NavySoft = Color.FromArgb(23, 42, 66);
        public static readonly Color Bg = Color.FromArgb(244, 246, 250);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 232, 242);
        public static readonly Color BorderStrong = Color.FromArgb(205, 214, 228);
        public static readonly Color Text = Color.FromArgb(27, 36, 48);
        public static readonly Color TextMuted = Color.FromArgb(107, 122, 144);
        public static readonly Color TextFaint = Color.FromArgb(107, 122, 144);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentSoft = Color.FromArgb(232, 240, 255);
        public static readonly Color Green = Color.FromArgb(22, 163, 74);
        public static readonly Color GreenSoft = Color.FromArgb(220, 252, 231);
        public static readonly Color Amber = Color.FromArgb(194, 120, 3);
        public static readonly Color AmberSoft = Color.FromArgb(254, 243, 199);
        public static readonly Color Red = Color.FromArgb(220, 38, 38);
        public static readonly Color RedSoft = Color.FromArgb(254, 226, 226);
        public static readonly Color Purple = Color.FromArgb(124, 58, 237);

        public static Font Font(float size)
        {
            return new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static Button PrimaryButton(string text, int width, int height)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = height;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.Font = Font(10f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TabStop = true;
            return b;
        }

        public static Button SecondaryButton(string text, int width, int height)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = height;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = BorderStrong;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 253);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 242, 248);
            b.BackColor = Color.White;
            b.ForeColor = Text;
            b.Font = Font(9.5f);
            b.Cursor = Cursors.Hand;
            return b;
        }

        public static Button SmallButton(string text, int width, int height)
        {
            Button b = SecondaryButton(text, width, height);
            b.Font = Font(9f);
            return b;
        }

        public static Label MakeLabel(string text, float size, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = Font(size);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }

        public static Label MakeBoldLabel(string text, float size, Color color)
        {
            Label label = MakeLabel(text, size, color);
            label.Font = Font(size, FontStyle.Bold);
            return label;
        }

        public static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundRect(Graphics g, Rectangle rect, int radius, Color fill, Color border)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundRect(rect, radius))
            {
                using (SolidBrush brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (border != Color.Empty)
                {
                    using (Pen pen = new Pen(border)) g.DrawPath(pen, path);
                }
            }
            g.SmoothingMode = old;
        }
    }

    /// <summary>White card with a subtle 1px border.</summary>
    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Padding = new Padding(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    /// <summary>Rounded status badge in the header.</summary>
    public class StatusPill : Control
    {
        private Color _fill = UiTheme.GreenSoft;
        private Color _fore = UiTheme.Green;

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            Font = UiTheme.Font(9f, FontStyle.Bold);
            Height = 30;
            Width = 90;
            BackColor = Color.Transparent;
        }

        public void SetState(string text, Color fill, Color fore)
        {
            Text = text;
            _fill = fill;
            _fore = fore;
            Width = TextRenderer.MeasureText(text, Font).Width + 26;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiTheme.RoundRect(rect, Height / 2))
            using (SolidBrush brush = new SolidBrush(_fill))
            {
                e.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, _fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Small painted diagram of the UDP data flow.</summary>
    public class UdpDiagram : Control
    {
        public UdpDiagram()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 140;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int offset = Math.Max(0, (Width - 730) / 2);
            if (offset == 0 && Width < 730) offset = 0;

            DrawBox(g, new Rectangle(offset + 6, 42, 128, 52), "BktTimeSync", "系统对时", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 120, 8));
            DrawArrow(g, offset + 138, 68, offset + 190, 68);
            DrawCenterText(g, "对时", offset + 140, 46, 50, UiTheme.TextMuted);

            DrawBox(g, new Rectangle(offset + 194, 42, 112, 52), "JTDX", "解码 / 发送", Color.FromArgb(232, 240, 255), Color.FromArgb(37, 99, 235));

            using (Pen linePen = new Pen(Color.FromArgb(148, 163, 184), 1.6f))
            {
                g.DrawLine(linePen, offset + 306, 68, offset + 450, 68);
                g.DrawLine(linePen, offset + 450, 26, offset + 450, 110);
                g.DrawLine(linePen, offset + 450, 26, offset + 550, 26);
                g.DrawLine(linePen, offset + 450, 110, offset + 550, 110);
            }
            DrawArrowHead(g, offset + 550, 26);
            DrawArrowHead(g, offset + 550, 110);

            DrawLeftText(g, "UDP 组播 239.255.0.1:2237", offset + 318, 6, 220, UiTheme.Accent);

            DrawBox(g, new Rectangle(offset + 552, 6, 158, 44), "GridTracker2", "地图 / 台站", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 163, 74));
            DrawBox(g, new Rectangle(offset + 552, 90, 158, 44), "QLog", "通联日志", Color.FromArgb(243, 232, 255), Color.FromArgb(124, 58, 237));

            DrawLeftText(g, "BktTimeSync 不参与 UDP，只负责把电脑时间校准。", offset + 6, 116, 430, UiTheme.TextFaint);
        }

        private static void DrawBox(Graphics g, Rectangle rect, string title, string subtitle, Color fill, Color border)
        {
            UiTheme.FillRoundRect(g, rect, 10, fill, border);
            using (Font titleFont = UiTheme.Font(9.5f, FontStyle.Bold))
            using (Font subFont = UiTheme.Font(8f))
            {
                TextRenderer.DrawText(g, title, titleFont, new Rectangle(rect.X + 8, rect.Y + 6, rect.Width - 12, 20),
                    UiTheme.Text, TextFormatFlags.Left | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, subtitle, subFont, new Rectangle(rect.X + 8, rect.Y + 26, rect.Width - 12, 18),
                    UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.SingleLine);
            }
        }

        private static void DrawArrow(Graphics g, int x1, int y1, int x2, int y2)
        {
            using (Pen pen = new Pen(Color.FromArgb(148, 163, 184), 1.6f))
            {
                pen.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        private static void DrawArrowHead(Graphics g, int x, int y)
        {
            using (Pen pen = new Pen(Color.FromArgb(148, 163, 184), 1.6f))
            {
                pen.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(pen, x - 1, y, x, y);
            }
        }

        private static void DrawCenterText(Graphics g, string text, int x, int y, int width, Color color)
        {
            using (Font font = UiTheme.Font(8f))
            {
                TextRenderer.DrawText(g, text, font, new Rectangle(x, y, width, 18), color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
            }
        }

        private static void DrawLeftText(Graphics g, string text, int x, int y, int width, Color color)
        {
            using (Font font = UiTheme.Font(8f))
            {
                TextRenderer.DrawText(g, text, font, new Rectangle(x, y, width, 18), color,
                    TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }
}
