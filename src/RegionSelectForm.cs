using System;
using System.Drawing;
using System.Windows.Forms;

namespace Pasha
{
    // 矩形範囲をドラッグ選択する全画面オーバーレイ
    internal class RegionSelectForm : Form
    {
        private Point _start;
        private Rectangle _rect;
        private bool _dragging;
        public Rectangle Selection { get; private set; }

        public RegionSelectForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            BackColor = Color.Black;
            Opacity = 0.30;
            Cursor = Cursors.Cross;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            _dragging = true;
            _start = e.Location;
            _rect = new Rectangle(e.Location, Size.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            _rect = Rectangle.FromLTRB(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            var vs = SystemInformation.VirtualScreen;
            Selection = new Rectangle(_rect.X + vs.X, _rect.Y + vs.Y, _rect.Width, _rect.Height);
            DialogResult = (_rect.Width >= 1 && _rect.Height >= 1) ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_rect.Width > 0 && _rect.Height > 0)
            {
                using (var clear = new SolidBrush(Color.FromArgb(60, 60, 60)))
                    e.Graphics.FillRectangle(clear, _rect);
                using (var pen = new Pen(Color.FromArgb(0, 174, 255), 2))
                    e.Graphics.DrawRectangle(pen, _rect);
                string info = _rect.Width + " x " + _rect.Height;
                using (var f = new Font("Yu Gothic UI", 10))
                using (var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                using (var fg = new SolidBrush(Color.White))
                {
                    var sz = e.Graphics.MeasureString(info, f);
                    var pt = new PointF(_rect.X, Math.Max(0, _rect.Y - sz.Height - 2));
                    e.Graphics.FillRectangle(bg, pt.X, pt.Y, sz.Width, sz.Height);
                    e.Graphics.DrawString(info, f, fg, pt);
                }
            }
        }
    }
}
