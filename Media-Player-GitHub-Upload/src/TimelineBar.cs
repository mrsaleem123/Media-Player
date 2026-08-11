using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LumaPlayer
{
    internal sealed class TimelineBar : Control
    {
        private double duration;
        private double position;
        private bool dragging;

        internal event Action<double> SeekRequested;
        internal event Action<double> NudgeRequested;

        internal TimelineBar()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Height = 24;
            SetStyle(ControlStyles.Selectable, true);
        }

        internal double Duration
        {
            get { return duration; }
            set
            {
                duration = Math.Max(0.0, value);
                if (position > duration) position = duration;
                Invalidate();
            }
        }

        internal double Position
        {
            get { return position; }
            set
            {
                if (!dragging)
                {
                    position = Math.Max(0.0, duration > 0.0 ? Math.Min(duration, value) : value);
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF rail = new RectangleF(8, Height / 2f - 2f, Math.Max(1, Width - 16), 4f);
            using (GraphicsPath path = RoundedRectangle(rail, 2f))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(225, 229, 236)))
                e.Graphics.FillPath(brush, path);

            float progress = duration > 0.0 ? (float)(position / duration) : 0f;
            progress = Math.Max(0f, Math.Min(1f, progress));
            RectangleF played = new RectangleF(rail.X, rail.Y, Math.Max(4f, rail.Width * progress), rail.Height);
            using (GraphicsPath path = RoundedRectangle(played, 2f))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(40, 113, 245)))
                e.Graphics.FillPath(brush, path);

            float x = rail.X + rail.Width * progress;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(38, 0, 0, 0)))
                e.Graphics.FillEllipse(shadow, x - 7, rail.Y - 5, 14, 14);
            using (SolidBrush knob = new SolidBrush(Color.FromArgb(40, 113, 245)))
                e.Graphics.FillEllipse(knob, x - 5, rail.Y - 3, 10, 10);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                Capture = true;
                UpdateFromMouse(e.X, true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
                UpdateFromMouse(e.X, false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging && e.Button == MouseButtons.Left)
            {
                UpdateFromMouse(e.X, true);
                dragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            double steps = e.Delta / 120.0;
            if (Math.Abs(steps) > 0.001 && NudgeRequested != null)
                NudgeRequested(steps * 0.1);
        }

        private void UpdateFromMouse(int mouseX, bool notify)
        {
            if (duration <= 0.0) return;
            double ratio = (mouseX - 8.0) / Math.Max(1.0, Width - 16.0);
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            position = duration * ratio;
            Invalidate();
            if (notify && SeekRequested != null)
                SeekRequested(position);
        }

        private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
