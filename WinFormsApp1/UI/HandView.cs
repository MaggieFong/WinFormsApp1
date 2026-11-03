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
    /// 手部視圖：以程式繪製靈巧手（黑色手掌＋銀色分節手指），
    /// 依各指彎曲度 0~100 即時捲曲；觸碰中的指節畫紅圈並跟著手指移動。
    /// 點擊指節可切換觸碰狀態（無硬體時用來模擬）。
    /// </summary>
    public class HandView : SmoothControl
    {
        // ═════════════════════════════════════════════════════
        //  手部幾何（模型座標，掌心朝向畫面，拇指在右側）
        // ═════════════════════════════════════════════════════
        private sealed class FingerDef
        {
            public PointF Base;      // 指根（掌指關節）位置
            public float Splay;      // 張開角度，0 = 正上方，正值向右（度）
            public float Width;      // 指寬
            public float[] Length = Array.Empty<float>(); // 各指節長度（近端 → 指尖）
        }

        // 索引：0=拇指 1=食指 2=中指 3=無名指 4=小指
        private static readonly FingerDef[] Defs =
        {
            new() { Base = new(805, 735), Splay = 0,  Width = 70, Length = new[] { 120f, 100f } },       // 拇指
            new() { Base = new(678, 556), Splay = 5,  Width = 70, Length = new[] { 140f, 108f, 92f } },  // 食指
            new() { Base = new(553, 546), Splay = 0,  Width = 74, Length = new[] { 152f, 118f, 98f } },  // 中指
            new() { Base = new(428, 556), Splay = -5, Width = 70, Length = new[] { 138f, 106f, 90f } },  // 無名指
            new() { Base = new(320, 588), Splay = -12, Width = 60, Length = new[] { 108f, 88f, 76f } },  // 小指
        };

        // 各關節最大彎曲角（度）：掌指、近端指間、遠端指間
        private static readonly float[] JointMax = { 90f, 105f, 75f };

        // 拇指：平面內由「朝右上」轉到「橫過掌心」
        private const float ThumbRestAngle = -50f;   // 數學角度（y 軸向下），-90 = 正上方
        private const float ThumbFlexAngle = -158f;
        private const float ThumbTipBend = -20f;

        // 模型可視範圍
        private static readonly RectangleF ModelView = new(160, 140, 830, 870);

        public static int FingerCount => Defs.Length;
        public static int SegmentCount(int finger) => Defs[finger].Length.Length;
        public static int TotalSegments
        {
            get { int n = 0; foreach (var f in Defs) n += f.Length.Length; return n; }
        }

        // ── 計算後的畫面座標 ─────────────────────────────────
        private struct Seg
        {
            public PointF A, B;   // 起點（關節）→ 終點
            public float W;       // 畫面上的寬度
            public float Facing;  // >0 指腹朝向畫面，<0 已翻到背面（捲曲）
            public bool Tip;
            public PointF Center => new((A.X + B.X) / 2, (A.Y + B.Y) / 2);
        }

        private readonly Seg[][] _segs;
        private readonly bool[][] _touch;
        private readonly int[] _flex = new int[5];
        private float _scale = 1f;
        private PointF _origin;
        private (int f, int s)? _hover;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private float _pulse; // 0~1 呼吸動畫

        public event EventHandler<TouchEventArgs>? TouchToggled;

        /// <summary>允許滑鼠點擊模擬觸碰。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AllowClickToSimulate { get; set; } = true;

        public HandView()
        {
            BackColor = Theme.Card;
            _segs = new Seg[Defs.Length][];
            _touch = new bool[Defs.Length][];
            for (int i = 0; i < Defs.Length; i++)
            {
                _segs[i] = new Seg[Defs[i].Length.Length];
                _touch[i] = new bool[Defs[i].Length.Length];
            }

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

        // ═════════════════════════════════════════════════════
        //  公開 API
        // ═════════════════════════════════════════════════════
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

        /// <summary>設定手指彎曲度 0~100，手部圖形會立即重繪。</summary>
        public void SetFlex(int finger, int percent)
        {
            if (finger < 0 || finger >= _flex.Length) return;
            percent = Math.Clamp(percent, 0, 100);
            if (_flex[finger] == percent) return;
            _flex[finger] = percent;
            Invalidate();
        }

        // ═════════════════════════════════════════════════════
        //  幾何計算
        // ═════════════════════════════════════════════════════
        private RectangleF DrawArea => new(24, 56, Width - 48, Height - 56 - 44);

        private void UpdateTransform()
        {
            var area = DrawArea;
            _scale = Math.Max(0.05f, Math.Min(area.Width / ModelView.Width, area.Height / ModelView.Height));
            float w = ModelView.Width * _scale, h = ModelView.Height * _scale;
            _origin = new PointF(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2);
        }

        private PointF S(float x, float y) =>
            new(_origin.X + (x - ModelView.X) * _scale, _origin.Y + (y - ModelView.Y) * _scale);
        private PointF S(PointF p) => S(p.X, p.Y);

        private static float Rad(float deg) => deg * (float)Math.PI / 180f;

        /// <summary>依目前彎曲度計算所有指節在畫面上的位置。</summary>
        private void ComputeSegments()
        {
            UpdateTransform();

            // 四指：關節朝畫面方向彎曲，以投影長度 cos(累積角) 呈現捲曲
            for (int f = 1; f < Defs.Length; f++)
            {
                var d = Defs[f];
                float t = _flex[f] / 100f;
                var dir = new PointF((float)Math.Sin(Rad(d.Splay)), -(float)Math.Cos(Rad(d.Splay)));
                var p = d.Base;
                float cum = 0;
                for (int s = 0; s < d.Length.Length; s++)
                {
                    cum += JointMax[s] * t;
                    float c = (float)Math.Cos(Rad(cum));
                    float proj = d.Length[s] * c;
                    var q = new PointF(p.X + dir.X * proj, p.Y + dir.Y * proj);
                    _segs[f][s] = new Seg
                    {
                        A = S(p),
                        B = S(q),
                        W = d.Width * (1f - 0.05f * s) * _scale,
                        Facing = c,
                        Tip = s == d.Length.Length - 1,
                    };
                    p = q;
                }
            }

            // 拇指：平面內旋轉橫過掌心，同時略為縮短
            {
                var d = Defs[0];
                float t = _flex[0] / 100f;
                float a0 = ThumbRestAngle + (ThumbFlexAngle - ThumbRestAngle) * t;
                float a1 = a0 + ThumbTipBend * t;
                float[] angles = { a0, a1 };
                var p = d.Base;
                for (int s = 0; s < 2; s++)
                {
                    float len = d.Length[s] * (1f - 0.08f * t);
                    var q = new PointF(p.X + (float)Math.Cos(Rad(angles[s])) * len,
                                       p.Y + (float)Math.Sin(Rad(angles[s])) * len);
                    _segs[0][s] = new Seg
                    {
                        A = S(p),
                        B = S(q),
                        W = d.Width * (1f - 0.06f * s) * _scale,
                        Facing = 1f,
                        Tip = s == 1,
                    };
                    p = q;
                }
            }
        }

        // ═════════════════════════════════════════════════════
        //  繪製
        // ═════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            DrawGrid(g);
            DrawCornerBrackets(g);

            ComputeSegments();

            DrawArm(g);
            DrawPalm(g);
            DrawThumbPad(g);
            for (int f = Defs.Length - 1; f >= 1; f--) DrawFinger(g, f);   // 小指 → 食指，捲曲時蓋在掌面上
            DrawFinger(g, 0);                                                // 拇指在最上層

            DrawTouchMarkers(g);
            DrawFlexTags(g);
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

        private GraphicsPath ModelPolygon(params PointF[] modelPts)
        {
            var pts = new PointF[modelPts.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = S(modelPts[i]);
            var path = new GraphicsPath();
            path.AddClosedCurve(pts, 0.25f);
            return path;
        }

        private void DrawArm(Graphics g)
        {
            // 前臂
            var arm = RectangleF.FromLTRB(S(420, 0).X, S(0, 925).Y, S(600, 0).X, S(0, 1010).Y);
            using (var b = new LinearGradientBrush(arm, Color.FromArgb(20, 20, 22), Color.FromArgb(48, 48, 52), LinearGradientMode.Horizontal))
            {
                b.SetSigmaBellShape(0.5f, 0.6f);
                g.FillRectangle(b, arm);
            }

            // 腕部金屬環
            var ring = RectangleF.FromLTRB(S(395, 0).X, S(0, 878).Y, S(625, 0).X, S(0, 925).Y);
            FillMetal(g, ring, vertical: false, dark: false);
            using (var pen = new Pen(Color.FromArgb(70, 70, 75), Math.Max(1f, 2f * _scale)))
            {
                float y1 = ring.Top + ring.Height * 0.33f, y2 = ring.Top + ring.Height * 0.66f;
                g.DrawLine(pen, ring.Left, y1, ring.Right, y1);
                g.DrawLine(pen, ring.Left, y2, ring.Right, y2);
            }
        }

        private void DrawPalm(Graphics g)
        {
            using var path = ModelPolygon(
                new PointF(290, 575), new PointF(310, 548), new PointF(560, 535), new PointF(725, 545),
                new PointF(750, 600), new PointF(752, 700), new PointF(700, 840), new PointF(640, 885),
                new PointF(380, 885), new PointF(322, 830), new PointF(296, 700));
            var bounds = path.GetBounds();
            using (var b = new LinearGradientBrush(bounds, Color.FromArgb(52, 52, 56), Color.FromArgb(18, 18, 20), 60f))
                g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(90, 90, 96), Math.Max(1f, 1.5f * _scale)))
                g.DrawPath(pen, path);

            // 掌面高光
            using var hl = ModelPolygon(new PointF(330, 600), new PointF(520, 585), new PointF(500, 640), new PointF(340, 660));
            using (var b = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                g.FillPath(b, hl);
        }

        private void DrawThumbPad(Graphics g)
        {
            // 拇指根部的黑色肌肉塊（大魚際），跟著拇指彎曲略微旋轉
            float t = _flex[0] / 100f;
            var state = g.Save();
            var pivot = S(745, 800);
            g.TranslateTransform(pivot.X, pivot.Y);
            g.RotateTransform(-18f * t);
            g.TranslateTransform(-pivot.X, -pivot.Y);

            using var path = ModelPolygon(
                new PointF(700, 640), new PointF(785, 690), new PointF(835, 745),
                new PointF(800, 820), new PointF(705, 865), new PointF(660, 790));
            using (var b = new LinearGradientBrush(path.GetBounds(), Color.FromArgb(60, 60, 64), Color.FromArgb(22, 22, 24), 120f))
                g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(100, 100, 106), Math.Max(1f, 1.5f * _scale)))
                g.DrawPath(pen, path);

            g.Restore(state);
        }

        private void DrawFinger(Graphics g, int f)
        {
            var segs = _segs[f];
            for (int s = 0; s < segs.Length; s++)
            {
                DrawSegment(g, segs[s]);
                DrawJoint(g, segs[s]);
            }
        }

        /// <summary>畫一節銀色指節。指節捲到背面時顏色較暗，幾乎垂直畫面時畫成端面。</summary>
        private void DrawSegment(Graphics g, Seg seg)
        {
            float dx = seg.B.X - seg.A.X, dy = seg.B.Y - seg.A.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            bool dark = seg.Facing < 0;
            float w = seg.W;

            if (len < w * 0.45f)
            {
                // 端面：指節幾乎正對畫面
                var c = seg.Center;
                var r = new RectangleF(c.X - w / 2, c.Y - w * 0.3f, w, w * 0.6f);
                FillMetal(g, r, vertical: true, dark: dark, ellipse: true);
                return;
            }

            float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
            var state = g.Save();
            g.TranslateTransform(seg.A.X, seg.A.Y);
            g.RotateTransform(angle);

            float gap = w * 0.12f;
            var rect = new RectangleF(gap, -w / 2, Math.Max(1f, len - gap * (seg.Tip ? 1 : 2)), w);
            using (var path = seg.Tip ? BulletPath(rect) : Theme.RoundRect(rect, w * 0.18f))
            using (var b = MetalBrush(rect, vertical: true, dark: dark))
            {
                g.FillPath(b, path);
                using var pen = new Pen(Color.FromArgb(dark ? 60 : 90, 40, 40, 44), Math.Max(1f, 1.2f * _scale));
                g.DrawPath(pen, path);
            }

            g.Restore(state);
        }

        /// <summary>關節：深色轉軸 + 兩側銀色插銷。</summary>
        private void DrawJoint(Graphics g, Seg seg)
        {
            float dx = seg.B.X - seg.A.X, dy = seg.B.Y - seg.A.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = len > 0.5f ? (float)(Math.Atan2(dy, dx) * 180 / Math.PI) : -90f;

            var state = g.Save();
            g.TranslateTransform(seg.A.X, seg.A.Y);
            g.RotateTransform(angle);

            float w = seg.W * 1.04f, th = Math.Max(4f, seg.W * 0.26f);
            var band = new RectangleF(-th / 2, -w / 2, th, w);
            using (var path = Theme.RoundRect(band, th * 0.35f))
            using (var b = new SolidBrush(Color.FromArgb(28, 28, 30)))
                g.FillPath(b, path);

            float pin = Math.Max(2f, seg.W * 0.12f);
            using (var b = new SolidBrush(Color.FromArgb(185, 188, 194)))
            {
                g.FillEllipse(b, -pin / 2, -w / 2 - pin * 0.2f, pin, pin);
                g.FillEllipse(b, -pin / 2, w / 2 - pin * 0.8f, pin, pin);
            }

            g.Restore(state);
        }

        private static GraphicsPath BulletPath(RectangleF r)
        {
            // 指尖：近端小圓角、末端半圓
            var path = new GraphicsPath();
            float rr = r.Height * 0.18f, d = rr * 2;
            float capW = Math.Min(r.Height, r.Width * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - capW, r.Y, capW, r.Height, 270, 180);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Brush MetalBrush(RectangleF r, bool vertical, bool dark)
        {
            var b = new LinearGradientBrush(
                new RectangleF(r.X, r.Y - 1, Math.Max(1, r.Width), Math.Max(1, r.Height + 2)),
                Color.Black, Color.Black,
                vertical ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal);
            b.InterpolationColors = new ColorBlend
            {
                Colors = dark
                    ? new[] { Color.FromArgb(70, 72, 76), Color.FromArgb(150, 154, 160), Color.FromArgb(110, 113, 118), Color.FromArgb(55, 57, 60) }
                    : new[] { Color.FromArgb(105, 108, 114), Color.FromArgb(242, 244, 246), Color.FromArgb(178, 182, 188), Color.FromArgb(88, 90, 95) },
                Positions = new[] { 0f, 0.35f, 0.65f, 1f },
            };
            return b;
        }

        private void FillMetal(Graphics g, RectangleF r, bool vertical, bool dark, bool ellipse = false)
        {
            using var b = MetalBrush(r, vertical, dark);
            if (ellipse) g.FillEllipse(b, r); else g.FillRectangle(b, r);
            using var pen = new Pen(Color.FromArgb(80, 40, 40, 44), Math.Max(1f, 1.2f * _scale));
            if (ellipse) g.DrawEllipse(pen, r); else g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }

        private float MarkerRadius(Seg seg) => Math.Max(8f, seg.W * 0.42f);

        private void DrawTouchMarkers(Graphics g)
        {
            for (int f = 0; f < _segs.Length; f++)
            {
                for (int s = 0; s < _segs[f].Length; s++)
                {
                    var seg = _segs[f][s];
                    var c = seg.Center;
                    float r = MarkerRadius(seg);
                    bool touched = _touch[f][s];
                    bool hover = _hover.HasValue && _hover.Value.f == f && _hover.Value.s == s;

                    if (touched)
                    {
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
                        using var pen = new Pen(Color.FromArgb(200, Theme.Accent), 2f) { DashStyle = DashStyle.Dash };
                        g.DrawEllipse(pen, c.X - r, c.Y - r, r * 2, r * 2);
                    }
                }
            }
        }

        /// <summary>每根手指上方固定一列顯示彎曲度。</summary>
        private void DrawFlexTags(Graphics g)
        {
            for (int f = 0; f < Defs.Length; f++)
            {
                string text = $"{_flex[f]}%";
                var sz = TextRenderer.MeasureText(g, text, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
                float w = sz.Width + 12, h = 18;

                RectangleF box;
                if (f == 0)
                {
                    var p = S(Defs[0].Base.X + 110, Defs[0].Base.Y - 140);
                    box = new RectangleF(p.X - w / 2, p.Y - h / 2, w, h);
                }
                else
                {
                    var p = S(Defs[f].Base);
                    box = new RectangleF(p.X - w / 2, 22, w, h);
                }

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

        // ═════════════════════════════════════════════════════
        //  滑鼠互動
        // ═════════════════════════════════════════════════════
        private (int f, int s)? HitTest(Point p)
        {
            // 由上層（拇指、末節）往下找，與繪製順序相反
            int[] order = { 0, 1, 2, 3, 4 };
            foreach (int f in order)
                for (int s = _segs[f].Length - 1; s >= 0; s--)
                {
                    var seg = _segs[f][s];
                    var c = seg.Center;
                    float r = MarkerRadius(seg) + 3;
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
