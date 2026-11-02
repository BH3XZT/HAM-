using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HamStartup
{
    /// <summary>Simple painted network/launch topology used by the card+topology view.</summary>
    public class TopologyView : Control
    {
        private string _center = "HAM 工作台";
        private List<string> _left = new List<string>();
        private List<string> _right = new List<string>();

        public TopologyView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 164;
            BackColor = Color.White;
        }

        public void SetData(string center, List<string> left, List<string> right)
        {
            _center = center ?? "HAM 工作台";
            _left = left ?? new List<string>();
            _right = right ?? new List<string>();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int centerWidth = 150;
            int centerHeight = 54;
            Rectangle center = new Rectangle((Width - centerWidth) / 2, (Height - centerHeight) / 2,
                centerWidth, centerHeight);
            UiTheme.FillRoundRect(g, center, 12, Color.FromArgb(232, 240, 255), UiTheme.Accent);
            TextRenderer.DrawText(g, _center, UiTheme.Font(9.5f, FontStyle.Bold), center,
                UiTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);

            int max = Math.Max(_left.Count, _right.Count);
            if (max == 0)
            {
                TextRenderer.DrawText(g, "暂无拓扑数据", UiTheme.Font(8.5f),
                    new Rectangle(0, Height - 24, Width, 20), UiTheme.TextFaint,
                    TextFormatFlags.HorizontalCenter);
                return;
            }

            int count = Math.Min(4, max);
            int boxWidth = 150;
            int boxHeight = 34;
            int gap = Math.Max(8, (Height - 16 - count * boxHeight) / Math.Max(1, count + 1));
            int leftX = 14;
            int rightX = Width - boxWidth - 14;

            for (int i = 0; i < count; i++)
            {
                int y = 10 + i * (boxHeight + gap);
                if (i < _left.Count)
                {
                    Rectangle box = new Rectangle(leftX, y, boxWidth, boxHeight);
                    UiTheme.FillRoundRect(g, box, 8, Color.FromArgb(248, 250, 253), UiTheme.BorderStrong);
                    TextRenderer.DrawText(g, Trim(_left[i], 20), UiTheme.Font(8f), box,
                        UiTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine);
                    using (Pen pen = new Pen(Color.FromArgb(148, 163, 184), 1.3f))
                    {
                        g.DrawLine(pen, box.Right, box.Top + box.Height / 2, center.Left, center.Top + center.Height / 2);
                    }
                }
                if (i < _right.Count)
                {
                    Rectangle box = new Rectangle(rightX, y, boxWidth, boxHeight);
                    UiTheme.FillRoundRect(g, box, 8, Color.FromArgb(248, 250, 253), UiTheme.BorderStrong);
                    TextRenderer.DrawText(g, Trim(_right[i], 20), UiTheme.Font(8f), box,
                        UiTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine);
                    using (Pen pen = new Pen(Color.FromArgb(148, 163, 184), 1.3f))
                    {
                        g.DrawLine(pen, center.Right, center.Top + center.Height / 2, box.Left, box.Top + box.Height / 2);
                    }
                }
            }
            if (max > 4)
            {
                TextRenderer.DrawText(g, "+" + (max - 4) + " 更多", UiTheme.Font(8f),
                    new Rectangle(14, Height - 20, Width - 28, 16), UiTheme.TextFaint,
                    TextFormatFlags.HorizontalCenter);
            }
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }
    }
}
