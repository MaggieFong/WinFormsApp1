using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RobotHandDemo.UI
{
    /// <summary>所有自繪元件的共同基底：雙緩衝、尺寸變更即重繪。</summary>
    public abstract class SmoothControl : Control
    {
        protected SmoothControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  卡片容器：白底、細框線、左側藍色標記、中文標題 + 英文副標
    // ═════════════════════════════════════════════════════════════
    public class CardPanel : Panel
    {
        public const int HeaderHeight = 48;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get; set; } = "";

        public CardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Card;
            Padding = new Padding(20, HeaderHeight + 12, 20, 16);
            Margin = new Padding(0, 0, 0, 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 外框
            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // 左側主色標記
            using (var b = new SolidBrush(Theme.Primary))
                g.FillRectangle(b, 0, 14, 4, 20);

            // 標題
            var titleSize = TextRenderer.MeasureText(g, Title, Theme.CardTitle, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Title, Theme.CardTitle, new Point(20, 24 - titleSize.Height / 2), Theme.Text, TextFormatFlags.NoPadding);

            // 英文副標
            if (!string.IsNullOrEmpty(Subtitle))
            {
                var sub = Theme.Spaced(Subtitle);
                var subSize = TextRenderer.MeasureText(g, sub, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, sub, Theme.Caption,
                    new Point(20 + titleSize.Width + 12, 24 - subSize.Height / 2 + 1), Theme.Muted, TextFormatFlags.NoPadding);
            }

            // 標題下分隔線
            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, 1, HeaderHeight, Width - 2, HeaderHeight);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  雙行標籤：中文主字 + 英文大寫小字
    // ═════════════════════════════════════════════════════════════
    public class DualLabel : SmoothControl
    {
        private string _primary = "", _secondary = "";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Primary { get => _primary; set { _primary = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Secondary { get => _secondary; set { _secondary = value; Invalidate(); } }

        public DualLabel()
        {
            BackColor = Color.Transparent;
            Size = new Size(110, 40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int mid = Height / 2;
            TextRenderer.DrawText(g, _primary, Theme.BodyBold, new Rectangle(0, 0, Width, mid + 2),
                Theme.Text, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Theme.Spaced(_secondary), Theme.Caption, new Rectangle(0, mid + 3, Width, mid),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  扁平滑桿：細軌道 + 主色填充 + 圓形把手，支援滑鼠拖曳、滾輪、方向鍵
    // ═════════════════════════════════════════════════════════════
    [DefaultEvent(nameof(ValueChanged))]
    public class FlatSlider : SmoothControl
    {
        private int _value;
        private bool _hover, _drag;
        private const int ThumbR = 8;

        public event EventHandler? ValueChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Minimum { get; set; } = 0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Maximum { get; set; } = 100;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set
            {
                int v = Math.Clamp(value, Minimum, Maximum);
                if (v == _value) return;
                _value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>true 表示目前是使用者用滑鼠/鍵盤操作中（非程式設定）。</summary>
        public bool IsUserInteracting => _drag;

        public FlatSlider()
        {
            BackColor = Color.Transparent;
            Height = 28;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        private RectangleF TrackRect => new(ThumbR + 1, Height / 2f - 2, Width - 2 * ThumbR - 2, 4);

        private float ValueToX(int v)
        {
            var t = TrackRect;
            return t.X + t.Width * (v - Minimum) / Math.Max(1, Maximum - Minimum);
        }

        private int XToValue(int x)
        {
            var t = TrackRect;
            float ratio = (x - t.X) / Math.Max(1f, t.Width);
            return Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = TrackRect;
            float x = ValueToX(_value);

            using (var path = Theme.RoundRect(t, 2))
            using (var b = new SolidBrush(Theme.Track))
                g.FillPath(b, path);

            var fill = new RectangleF(t.X, t.Y, Math.Max(0, x - t.X), t.Height);
            if (fill.Width > 0)
                using (var path = Theme.RoundRect(fill, 2))
                using (var b = new SolidBrush(Enabled ? Theme.Primary : Theme.Muted))
                    g.FillPath(b, path);

            // 刻度（每 25%）
            using (var tick = new SolidBrush(Theme.Border))
                for (int i = 0; i <= 4; i++)
                {
                    float tx = t.X + t.Width * i / 4f;
                    g.FillRectangle(tick, tx - 0.5f, t.Bottom + 5, 1, 3);
                }

            // 把手
            float r = (_hover || _drag || Focused) ? ThumbR + 1 : ThumbR;
            var thumb = new RectangleF(x - r, Height / 2f - r, r * 2, r * 2);
            if (_hover || _drag)
                using (var halo = new SolidBrush(Color.FromArgb(40, Theme.Primary)))
                    g.FillEllipse(halo, RectangleF.Inflate(thumb, 5, 5));
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, thumb);
            using (var pen = new Pen(Theme.Primary, 2.5f)) g.DrawEllipse(pen, thumb);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _drag = true;
            Value = XToValue(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) Value = XToValue(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _drag = true;
            Value += e.Delta > 0 ? 5 : -5;
            _drag = false;
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int step = e.Shift ? 10 : 1;
            _drag = true;
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= step; break;
                case Keys.Right: case Keys.Up: Value += step; break;
                case Keys.Home: Value = Minimum; break;
                case Keys.End: Value = Maximum; break;
            }
            _drag = false;
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  扁平按鈕：白底細框 → hover 淺藍 → 選取為主色實心
    // ═════════════════════════════════════════════════════════════
    public class FlatButton : SmoothControl
    {
        private bool _hover, _down, _selected;
        private string _subtitle = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected { get => _selected; set { _selected = value; Invalidate(); } }

        /// <summary>次要樣式（例如「清除」）：選取狀態不填色。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Secondary { get; set; }

        public FlatButton()
        {
            BackColor = Theme.Card;
            Cursor = Cursors.Hand;
            Size = new Size(110, 52);
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg, border, fg, sub;
            if (_selected && !Secondary)
            {
                bg = _down ? Theme.PrimaryDark : Theme.Primary;
                border = bg; fg = Color.White; sub = Color.FromArgb(190, 210, 240);
            }
            else if (_down)
            {
                bg = Theme.PrimaryLight; border = Theme.PrimaryDark; fg = Theme.PrimaryDark; sub = Theme.Primary;
            }
            else if (_hover || Focused)
            {
                bg = Theme.PrimaryLight; border = Theme.Primary; fg = Theme.Primary; sub = Theme.Primary;
            }
            else
            {
                bg = Theme.Card; border = Theme.Border; fg = Theme.Text; sub = Theme.Muted;
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundRect(rect, 3))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(border)) g.DrawPath(p, path);
            }

            if (string.IsNullOrEmpty(_subtitle))
            {
                TextRenderer.DrawText(g, Text, Theme.BodyBold, ClientRectangle, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                int mid = Height / 2;
                TextRenderer.DrawText(g, Text, Theme.BodyBold, new Rectangle(0, 0, Width, mid + 3), fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Theme.Spaced(_subtitle), Theme.Caption, new Rectangle(0, mid + 4, Width, mid), sub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _down = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  頁首：白底、左側品牌方塊 + 標題、右側連線狀態膠囊
    // ═════════════════════════════════════════════════════════════
    public class HeaderBar : SmoothControl
    {
        private string _status = "DEMO";
        private bool _connected;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get; set; } = "";

        public void SetConnection(bool connected, string text)
        {
            _connected = connected; _status = text; Invalidate();
        }

        public HeaderBar()
        {
            BackColor = Color.White;
            Height = 64;
            Dock = DockStyle.Top;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 品牌方塊
            var mark = new Rectangle(24, 16, 32, 32);
            using (var b = new SolidBrush(Theme.Primary)) g.FillRectangle(b, mark);
            using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, mark.Right - 10, mark.Bottom - 10, 10, 10);
            TextRenderer.DrawText(g, "DH", Theme.Caption, new Rectangle(mark.X, mark.Y, 22, 22), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // 標題
            TextRenderer.DrawText(g, Title, Theme.Title, new Rectangle(70, 10, Width - 300, 26), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Theme.Spaced(Subtitle), Theme.Caption, new Rectangle(71, 38, Width - 300, 16), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

            // 狀態膠囊
            var text = Theme.Spaced(_status);
            var sz = TextRenderer.MeasureText(g, text, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
            int pw = sz.Width + 40, ph = 26;
            var pill = new Rectangle(Width - pw - 24, (Height - ph) / 2, pw, ph);
            var dot = _connected ? Theme.Ok : Theme.Accent;
            using (var path = Theme.RoundRect(pill, ph / 2f))
            {
                using (var b = new SolidBrush(Color.FromArgb(24, dot))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(120, dot))) g.DrawPath(p, path);
            }
            using (var b = new SolidBrush(dot)) g.FillEllipse(b, pill.X + 12, pill.Y + ph / 2 - 4, 8, 8);
            TextRenderer.DrawText(g, text, Theme.Caption, new Rectangle(pill.X + 26, pill.Y, pill.Width - 30, ph), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // 底部主色線（USI 風格的藍色分隔）
            using (var b = new SolidBrush(Theme.Primary)) g.FillRectangle(b, 0, Height - 3, Width, 3);
            using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 0, Height - 3, 120, 3);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  狀態列：深藍底，左訊息、右版本/時間
    // ═════════════════════════════════════════════════════════════
    public class AppStatusBar : SmoothControl
    {
        private string _message = "就緒";
        private string _right = "";

        public string Message { get => _message; set { _message = value; Invalidate(); } }
        public string RightText { get => _right; set { _right = value; Invalidate(); } }

        public AppStatusBar()
        {
            BackColor = Theme.Navy;
            Height = 30;
            Dock = DockStyle.Bottom;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 20, Height / 2 - 3, 6, 6);
            TextRenderer.DrawText(g, _message, Theme.Small, new Rectangle(34, 0, Width - 300, Height),
                Color.FromArgb(220, 228, 240), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _right, Theme.Small, new Rectangle(Width - 280, 0, 260, Height),
                Color.FromArgb(150, 165, 190), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
