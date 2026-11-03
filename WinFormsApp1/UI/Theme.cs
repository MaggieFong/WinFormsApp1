using System.Drawing;
using System.Drawing.Drawing2D;

namespace RobotHandDemo.UI
{
    /// <summary>
    /// 全域配色與字型（企業藍白風格，參考 USI 官網）。
    /// 要換色只改這裡即可。
    /// </summary>
    internal static class Theme
    {
        // ── 色彩 ─────────────────────────────────────────
        public static readonly Color Primary = Color.FromArgb(0, 58, 145);      // #003A91 主色
        public static readonly Color PrimaryDark = Color.FromArgb(0, 40, 104);  // #002868 按下
        public static readonly Color PrimaryLight = Color.FromArgb(232, 239, 250); // #E8EFFA hover 底
        public static readonly Color Accent = Color.FromArgb(0, 160, 233);      // #00A0E9 亮藍點綴
        public static readonly Color Navy = Color.FromArgb(11, 31, 58);         // #0B1F3A 狀態列

        public static readonly Color Page = Color.FromArgb(243, 245, 248);      // #F3F5F8 頁面底
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(221, 227, 234);    // #DDE3EA
        public static readonly Color Track = Color.FromArgb(226, 231, 238);     // 滑桿軌道

        public static readonly Color Text = Color.FromArgb(31, 41, 55);         // #1F2937
        public static readonly Color Muted = Color.FromArgb(107, 119, 133);     // #6B7785
        public static readonly Color Touch = Color.FromArgb(230, 0, 18);        // #E60012 觸碰紅
        public static readonly Color Ok = Color.FromArgb(0, 166, 118);          // #00A676 正常綠

        // ── 字型 ─────────────────────────────────────────
        private const string Ui = "Microsoft JhengHei UI";
        private const string Latin = "Segoe UI";

        public static readonly Font Title = new(Ui, 14f, FontStyle.Bold);
        public static readonly Font CardTitle = new(Ui, 11f, FontStyle.Bold);
        public static readonly Font Body = new(Ui, 10f, FontStyle.Regular);
        public static readonly Font BodyBold = new(Ui, 10f, FontStyle.Bold);
        public static readonly Font Caption = new(Latin, 7.5f, FontStyle.Bold);   // 英文大寫小字
        public static readonly Font Small = new(Ui, 8.5f, FontStyle.Regular);
        public static readonly Font Value = new(Latin, 11f, FontStyle.Bold);      // 數值
        public static readonly Font BigValue = new(Latin, 26f, FontStyle.Bold);

        // ── 繪圖小工具 ───────────────────────────────────
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>英文標籤加字距（模擬 letter-spacing）。</summary>
        public static string Spaced(string s) => string.Join(" ", s.ToUpperInvariant().ToCharArray());
    }
}
