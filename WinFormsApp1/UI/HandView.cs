using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RobotHandDemo.UI
{
    /// <summary>觸碰事件參數：哪一指、哪一節、是否觸碰中。</summary>
    public class TouchEventArgs : EventArgs
    {
        public int Finger { get; }
        public int Segment { get; }
        public bool Touched { get; }
        public TouchEventArgs(int finger, int segment, bool touched)
        {
            Finger = finger; Segment = segment; Touched = touched;
        }
    }

    /// <summary>
    /// 手部視圖：顯示靈巧手影像，於觸碰中的指節畫紅圈，指尖上方顯示彎曲度。
    /// 點擊指節可切換觸碰狀態（無硬體時用來模擬）。
    /// </summary>
    public class HandView : SmoothControl
    {
        // 指節中心座標，以影像寬高正規化 (0~1)。對應 Assets/hand.png (1254×1254)。
        // 索引：[手指][指節]，手指 0=拇指 1=食指 2=中指 3=無名指 4=小指；指節 0=近端 … 末端=指尖。
        private static readonly PointF[][] SegmentPoints =
        {
            new[] { P(895, 730), P(1010, 600) },                       // 拇指（2 節）
            new[] { P(712, 462), P(725, 302), P(733, 175) },           // 食指
            new[] { P(576, 462), P(575, 300), P(573, 165) },           // 中指
            new[] { P(447, 470), P(430, 322), P(410, 195) },           // 無名指
            new[] { P(340, 528), P(312, 402), P(285, 295) },           // 小指
        };
        private static PointF P(float x, float y) => new(x / 1254f, y / 1254f);

        public static int FingerCount => SegmentPoints.Length;
        public static int SegmentCount(int finger) => SegmentPoints[finger].Length;
        public static int TotalSegments
        {
            get { int n = 0; foreach (var f in SegmentPoints) n += f.Length; return n; }
        }

        private readonly bool[][] _touch;
        private readonly int[] _flex = new int[5];
        private Image? _image;
        private RectangleF _imgRect;
        private (int f, int s)? _hover;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private float _pulse; // 0~1 呼吸動畫

        public event EventHandler<TouchEventArgs>? TouchToggled;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image? Image { get => _image; set { _image = value; Invalidate(); } }

        /// <summary>允許滑鼠點擊模擬觸碰。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AllowClickToSimulate { get; set; } = true;

        public HandView()
        {
            BackColor = Theme.Card;
            _touch = new bool[SegmentPoints.Length][];
            for (int i = 0; i < _touch.Length; i++) _touch[i] = new bool[SegmentPoints[i].Length];

            _pulseTimer = new System.Windows.Forms.Timer { Interval = 40 };
            _pulseTimer.Tick += (_, _) =>
            {
                _pulse = (_pulse + 0.04f) % 1f;
                if (AnyTouched) Invalidate();
            };
            _pulseTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _pulseTimer.Dispose();
            base.Dispose(disposing);
        }

        public bool AnyTouched
        {
            get { foreach (var f in _touch) foreach (var t in f) if (t) return true; return false; }
        }

        public int TouchedCount
        {
            get { int n = 0; foreach (var f in _touch) foreach (var t in f) if (t) n++; return n; }
        }

        public bool GetTouch(int finger, int segment) => _touch[finger][segment];

        /// <summary>設定某指節是否觸碰中（硬體回報時呼叫）。</summary>
        public void SetTouch(int finger, int segment, bool touched)
        {
            if (finger < 0 || finger >= _touch.Length) return;
            if (segment < 0 || segment >= _touch[finger].Length) return;
            if (_touch[finger][segment] == touched) return;
            _touch[finger][segment] = touched;
            Invalidate();
            TouchToggled?.Invoke(this, new TouchEventArgs(finger, segment, touched));
        }

        public void ClearTouches()
        {
            for (int f = 0; f < _touch.Length; f++)
                for (int s = 0; s < _touch[f].Length; s++)
                    SetTouch(f, s, false);
        }

        public void SetFlex(int finger, int percent)
        {
            if (finger < 0 || finger >= _flex.Length) return;
            _flex[finger] = percent;
            Invalidate();
        }

        // ── 版面計算 ─────────────────────────────────────────
        private RectangleF ComputeImageRect()
        {
            if (_image == null) return RectangleF.Empty;
            // 留邊給角框與標示
            var area = new RectangleF(24, 40, Width - 48, Height - 80);
            float ratio = Math.Min(area.Width / _image.Width, area.Height / _image.Height);
            float w = _image.Width * ratio, h = _image.Height * ratio;
            return new RectangleF(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        }

        private PointF ToScreen(PointF n) => new(_imgRect.X + n.X * _imgRect.Width, _imgRect.Y + n.Y * _imgRect.Height);
        private float MarkerRadius => Math.Max(9f, _imgRect.Width * 0.026f);

        // ── 繪製 ─────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(BackColor);

            DrawGrid(g);
            DrawCornerBrackets(g);

            _imgRect = ComputeImageRect();
            if (_image == null)
            {
                TextRenderer.DrawText(g, "找不到手部影像 (Assets/hand.png)", Theme.Body, ClientRectangle, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            g.DrawImage(_image, _imgRect);
            DrawFlexTags(g);
            DrawSegments(g);
            DrawLegend(g);
        }

        private void DrawGrid(Graphics g)
        {
            using var pen = new Pen(Color.FromArgb(244, 246, 249));
            for (int x = 0; x < Width; x += 24) g.DrawLine(pen, x, 0, x, Height);
            for (int y = 0; y < Height; y += 24) g.DrawLine(pen, 0, y, Width, y);
        }

        private void DrawCornerBrackets(Graphics g)
        {
            using var pen = new Pen(Theme.Primary, 2f);
            int m = 12, l = 18;
            int r = Width - m - 1, b = Height - m - 1;
            g.DrawLines(pen, new[] { new Point(m, m + l), new Point(m, m), new Point(m + l, m) });
            g.DrawLines(pen, new[] { new Point(r - l, m), new Point(r, m), new Point(r, m + l) });
            g.DrawLines(pen, new[] { new Point(m, b - l), new Point(m, b), new Point(m + l, b) });
            g.DrawLines(pen, new[] { new Point(r - l, b), new Point(r, b), new Point(r, b - l) });
        }

        private void DrawSegments(Graphics g)
        {
            float r = MarkerRadius;
            for (int f = 0; f < SegmentPoints.Length; f++)
            {
                for (int s = 0; s < SegmentPoints[f].Length; s++)
                {
                    var c = ToScreen(SegmentPoints[f][s]);
                    bool touched = _touch[f][s];
                    bool hover = _hover.HasValue && _hover.Value.f == f && _hover.Value.s == s;

                    if (touched)
                    {
                        // 呼吸外圈
                        float pr = r + 4 + _pulse * 10;
                        int alpha = (int)(110 * (1 - _pulse));
                        using (var halo = new Pen(Color.FromArgb(alpha, Theme.Touch), 2f))
                            g.DrawEllipse(halo, c.X - pr, c.Y - pr, pr * 2, pr * 2);
                        using (var fill = new SolidBrush(Color.FromArgb(60, Theme.Touch)))
                            g.FillEllipse(fill, c.X - r, c.Y - r, r * 2, r * 2);
                        using (var pen = new Pen(Theme.Touch, 3.5f))
                            g.DrawEllipse(pen, c.X - r, c.Y - r, r * 2, r * 2);
                    }
                    else if (hover && AllowClickToSimulate)
                    {
                        using var pen = new Pen(Color.FromArgb(160, Theme.Primary), 2f) { DashStyle = DashStyle.Dash };
                        g.DrawEllipse(pen, c.X - r, c.Y - r, r * 2, r * 2);
                    }
                }
            }
        }

        private void DrawFlexTags(Graphics g)
        {
            for (int f = 0; f < SegmentPoints.Length; f++)
            {
                var tip = ToScreen(SegmentPoints[f][^1]);
                string text = $"{_flex[f]}%";
                var sz = TextRenderer.MeasureText(g, text, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
                float w = sz.Width + 12, h = 18;
                // 拇指標籤放右側，其餘放指尖上方
                RectangleF box = f == 0
                    ? new RectangleF(tip.X + MarkerRadius + 10, tip.Y - h / 2, w, h)
                    : new RectangleF(tip.X - w / 2, tip.Y - MarkerRadius - h - 14, w, h);

                var color = _flex[f] > 0 ? Theme.Primary : Theme.Muted;
                using (var path = Theme.RoundRect(box, 2))
                using (var b = new SolidBrush(color))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, text, Theme.Caption, Rectangle.Round(box), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void DrawLegend(Graphics g)
        {
            int x = 28, y = Height - 34;
            using (var pen = new Pen(Theme.Touch, 2.5f)) g.DrawEllipse(pen, x, y + 3, 12, 12);
            TextRenderer.DrawText(g, "觸碰中", Theme.Small, new Point(x + 18, y + 1), Theme.Text);
            TextRenderer.DrawText(g, Theme.Spaced("Touch"), Theme.Caption, new Point(x + 66, y + 4), Theme.Muted);

            if (AllowClickToSimulate)
            {
                const string hint = "點擊指節可模擬觸碰";
                var sz = TextRenderer.MeasureText(g, hint, Theme.Small);
                TextRenderer.DrawText(g, hint, Theme.Small, new Point(Width - sz.Width - 28, y + 1), Theme.Muted);
            }
        }

        // ── 滑鼠互動 ─────────────────────────────────────────
        private (int f, int s)? HitTest(Point p)
        {
            if (_image == null) return null;
            float r = MarkerRadius + 4;
            for (int f = 0; f < SegmentPoints.Length; f++)
                for (int s = 0; s < SegmentPoints[f].Length; s++)
                {
                    var c = ToScreen(SegmentPoints[f][s]);
                    float dx = p.X - c.X, dy = p.Y - c.Y;
                    if (dx * dx + dy * dy <= r * r) return (f, s);
                }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!AllowClickToSimulate) return;
            var hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit.HasValue ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover.HasValue) { _hover = null; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!AllowClickToSimulate || e.Button != MouseButtons.Left) return;
            var hit = HitTest(e.Location);
            if (hit.HasValue)
                SetTouch(hit.Value.f, hit.Value.s, !_touch[hit.Value.f][hit.Value.s]);
        }
    }
}
