using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pasha
{
    internal static class AppInfo
    {
        public const string Name = "Pasha";
        public const string Version = "1.0.0";
        public const string Author = "persimmon";
        public const string Url = "https://github.com/persimmon-rf/pasha-screenshot";
        public const string License = "MIT License";
    }

    internal static class IconFactory
    {
        // カメラ風アイコンを動的生成 (.ico ファイル不要)
        public static Icon Make()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var body = new SolidBrush(Color.FromArgb(40, 90, 200)))
                    g.FillRectangle(body, 3, 9, 26, 18);
                using (var top = new SolidBrush(Color.FromArgb(40, 90, 200)))
                    g.FillRectangle(top, 10, 5, 9, 5);
                using (var lens = new SolidBrush(Color.White))
                    g.FillEllipse(lens, 11, 12, 12, 12);
                using (var lens2 = new SolidBrush(Color.FromArgb(40, 90, 200)))
                    g.FillEllipse(lens2, 14, 15, 6, 6);
                var h = bmp.GetHicon();
                using (var ico = Icon.FromHandle(h))
                    return (Icon)ico.Clone();
            }
        }
    }
}
