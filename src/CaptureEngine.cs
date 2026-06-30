using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pasha
{
    // 画面取得のための低レベル処理
    internal static class CaptureEngine
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT pvAttribute, int cbAttribute);
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        public static Bitmap CaptureRect(Rectangle rect)
        {
            if (rect.Width < 1 || rect.Height < 1) return null;
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        public static Bitmap CaptureActiveWindow()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return null;
            RECT r;
            // DWM で影を除いた実表示範囲を取得 (失敗時は GetWindowRect)
            if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0)
            {
                if (!GetWindowRect(h, out r)) return null;
            }
            var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            rect.Intersect(SystemInformation.VirtualScreen);
            return CaptureRect(rect);
        }

        public static Bitmap CaptureClientArea()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return null;
            RECT rc;
            if (!GetClientRect(h, out rc)) return null;
            var tl = new POINT { X = rc.Left, Y = rc.Top };
            if (!ClientToScreen(h, ref tl)) return null;
            var rect = new Rectangle(tl.X, tl.Y, rc.Right - rc.Left, rc.Bottom - rc.Top);
            rect.Intersect(SystemInformation.VirtualScreen);
            return CaptureRect(rect);
        }
    }
}
