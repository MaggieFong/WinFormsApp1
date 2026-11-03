using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RobotHandDemo.UI;

namespace RobotHandDemo
{
    public partial class FormHandDemo : Form
    {
        // ── 手指 / 手勢定義 ───────────────────────────────────
        private static readonly (string Zh, string En, string Short)[] Fingers =
        {
            ("拇指", "Thumb", "拇"),
            ("食指", "Index", "食"),
            ("中指", "Middle", "中"),
            ("無名指", "Ring", "無"),
            ("小指", "Pinky", "小"),
        };

        private sealed record Gesture(string Zh, string En, int[] Target);

        // 目標彎曲度 0~100，順序：拇、食、中、無名、小
        private static readonly Gesture[] Gestures =
        {
            new("張開", "Open",     new[] {   0,   0,   0,   0,   0 }),
            new("握拳", "Fist",     new[] { 100, 100, 100, 100, 100 }),
            new("比讚", "Thumb up", new[] {   0, 100, 100, 100, 100 }),
            new("剪刀", "Peace",    new[] { 100,   0,   0, 100, 100 }),
            new("OK",   "OK",       new[] {  85,  85,   0,   0,   0 }),
            new("指向", "Point",    new[] { 100,   0, 100, 100, 100 }),
        };

        // ── 狀態 ─────────────────────────────────────────────
        private readonly int[] _flex = new int[5];

        // ── 元件 ─────────────────────────────────────────────
        private HeaderBar _header = null!;
        private AppStatusBar _status = null!;
        private HandView _hand = null!;
        private Panel _touchStrip = null!;
        private readonly FlatSlider[] _sliders = new FlatSlider[5];
        private readonly Label[] _valueLabels = new Label[5];
        private readonly FlatButton[] _presetButtons = new FlatButton[Gestures.Length];
        private ListBox _log = null!;
        private Image? _handImage;

        // ── 手勢過渡動畫 ─────────────────────────────────────
        private System.Windows.Forms.Timer? _animTimer;
        private System.Windows.Forms.Timer? _clockTimer;
        private readonly Stopwatch _animClock = new();
        private int[] _animFrom = new int[5];
        private int[] _animTo = new int[5];
        private bool _animating;
        private const double AnimDurationMs = 280;

        public FormHandDemo()
        {
            InitializeComponent();
            Font = Theme.Body;
            BackColor = Theme.Page;

            _handImage = LoadHandImage();
            BuildLayout();

            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += AnimTimer_Tick;

            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (_, _) => _status.RightText = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
            _clockTimer.Start();
            _status.RightText = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");

            Log("系統啟動，Demo 模式（未連接硬體）");
        }

        // ═════════════════════════════════════════════════════
        //  版面
        // ═════════════════════════════════════════════════════
        private void BuildLayout()
        {
            SuspendLayout();

            _header = new HeaderBar { Title = "靈巧手控制面板", Subtitle = "Dexterous Hand Control" };
            _header.SetConnection(false, "Demo mode");
            _status = new AppStatusBar { Message = "就緒" };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = Theme.Page };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Page,
                Margin = Padding.Empty,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            grid.Controls.Add(BuildHandCard(), 0, 0);
            grid.Controls.Add(BuildRightColumn(), 1, 0);
            body.Controls.Add(grid);

            // Dock 順序：先加 Fill，再加 Top/Bottom
            Controls.Add(body);
            Controls.Add(_status);
            Controls.Add(_header);

            ResumeLayout(true);
        }

        private Control BuildHandCard()
        {
            var card = new CardPanel
            {
                Title = "手部狀態",
                Subtitle = "Hand status",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 16, 0),
                Padding = new Padding(1, CardPanel.HeaderHeight + 1, 1, 1),
            };

            _hand = new HandView { Dock = DockStyle.Fill, Image = _handImage };
            _hand.TouchToggled += Hand_TouchToggled;

            _touchStrip = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Card, Padding = new Padding(0, 12, 16, 12) };
            _touchStrip.Paint += TouchStrip_Paint;

            var clear = new FlatButton { Text = "清除觸碰", Secondary = true, Dock = DockStyle.Right, Width = 96 };
            clear.Click += (_, _) =>
            {
                if (_hand.TouchedCount == 0) return;
                _hand.ClearTouches();
                Log("已清除所有觸碰點");
            };
            _touchStrip.Controls.Add(clear);

            // 先加 Fill，再加 Bottom，Dock 才會正確
            card.Controls.Add(_hand);
            card.Controls.Add(_touchStrip);
            return card;
        }

        private Control BuildRightColumn()
        {
            var col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                BackColor = Theme.Page,
            };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 336F));
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 196F));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            col.Controls.Add(BuildFlexCard(), 0, 0);
            col.Controls.Add(BuildPresetCard(), 0, 1);
            col.Controls.Add(BuildLogCard(), 0, 2);
            return col;
        }

        private Control BuildFlexCard()
        {
            var card = new CardPanel { Title = "手指彎曲度", Subtitle = "Finger flexion", Dock = DockStyle.Fill };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                BackColor = Theme.Card,
                Margin = Padding.Empty,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            for (int i = 0; i < 5; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            for (int i = 0; i < 5; i++)
            {
                int index = i;

                var name = new DualLabel
                {
                    Primary = Fingers[i].Zh,
                    Secondary = Fingers[i].En,
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                };

                _sliders[i] = new FlatSlider { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 8, 6) };
                _sliders[i].ValueChanged += (_, _) => OnSliderValueChanged(index);

                _valueLabels[i] = new Label
                {
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = Theme.Value,
                    ForeColor = Theme.Muted,
                    Text = "0 %",
                };

                table.Controls.Add(name, 0, i);
                table.Controls.Add(_sliders[i], 1, i);
                table.Controls.Add(_valueLabels[i], 2, i);
            }

            card.Controls.Add(table);
            return card;
        }

        private Control BuildPresetCard()
        {
            var card = new CardPanel { Title = "預設手勢", Subtitle = "Gesture presets", Dock = DockStyle.Fill };

            const int cols = 3;
            int rows = (Gestures.Length + cols - 1) / cols;
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = cols,
                RowCount = rows,
                BackColor = Theme.Card,
                Margin = Padding.Empty,
            };
            for (int c = 0; c < cols; c++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / cols));
            for (int r = 0; r < rows; r++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));

            for (int i = 0; i < Gestures.Length; i++)
            {
                int index = i;
                var btn = new FlatButton
                {
                    Text = Gestures[i].Zh,
                    Subtitle = Gestures[i].En,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(i % cols == 0 ? 0 : 5, 4, i % cols == cols - 1 ? 0 : 5, 4),
                };
                btn.Click += (_, _) => ApplyPreset(index);
                _presetButtons[i] = btn;
                table.Controls.Add(btn, i % cols, i / cols);
            }

            card.Controls.Add(table);
            return card;
        }

        private Control BuildLogCard()
        {
            var card = new CardPanel
            {
                Title = "事件紀錄",
                Subtitle = "Event log",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(20, CardPanel.HeaderHeight + 6, 12, 8),
            };

            _log = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false,
                SelectionMode = SelectionMode.None,
                Font = Theme.Small,
            };
            _log.DrawItem += Log_DrawItem;

            card.Controls.Add(_log);
            return card;
        }

        // ═════════════════════════════════════════════════════
        //  手指彎曲
        // ═════════════════════════════════════════════════════
        private void OnSliderValueChanged(int fingerIndex)
        {
            int value = _sliders[fingerIndex].Value;
            _flex[fingerIndex] = value;
            _valueLabels[fingerIndex].Text = $"{value} %";
            _valueLabels[fingerIndex].ForeColor = value > 0 ? Theme.Primary : Theme.Muted;
            _hand.SetFlex(fingerIndex, value);

            // 使用者手動拖曳 → 取消預設手勢選取、停止動畫
            if (_sliders[fingerIndex].IsUserInteracting)
            {
                StopAnimation();
                SetSelectedPreset(-1);
                _status.Message = $"{Fingers[fingerIndex].Zh} 彎曲度 {value}%";
            }
        }

        /// <summary>
        /// 套用預設手勢：滑桿以動畫過渡到目標值，並送出指令給硬體。
        /// </summary>
        private void ApplyPreset(int gestureIndex)
        {
            var g = Gestures[gestureIndex];
            SetSelectedPreset(gestureIndex);

            _animFrom = (int[])_flex.Clone();
            _animTo = (int[])g.Target.Clone();
            _animating = true;
            _animClock.Restart();
            _animTimer!.Start();

            SendToDevice(g.Target);
            _status.Message = $"執行手勢：{g.Zh}";
            Log($"執行手勢「{g.Zh}」 [{string.Join(", ", g.Target)}]");
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            double t = Math.Min(1.0, _animClock.Elapsed.TotalMilliseconds / AnimDurationMs);
            double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic

            for (int i = 0; i < 5; i++)
                _sliders[i].Value = (int)Math.Round(_animFrom[i] + (_animTo[i] - _animFrom[i]) * eased);

            if (t >= 1.0) StopAnimation();
        }

        private void StopAnimation()
        {
            if (!_animating) return;
            _animating = false;
            _animTimer?.Stop();
        }

        private void SetSelectedPreset(int index)
        {
            for (int i = 0; i < _presetButtons.Length; i++)
                _presetButtons[i].Selected = i == index;
        }

        /// <summary>
        /// TODO：在此串接實際的靈巧手通訊（Serial / TCP / SDK）。
        /// targets 為五指目標彎曲度 0~100（拇、食、中、無名、小）。
        /// </summary>
        private void SendToDevice(int[] targets)
        {
            // 例：_device.SetFingerPositions(targets);
        }

        // ═════════════════════════════════════════════════════
        //  觸碰
        // ═════════════════════════════════════════════════════
        /// <summary>
        /// 硬體回報觸碰時呼叫（可從背景執行緒呼叫）。
        /// finger：0=拇指…4=小指；segment：0=近端…末端=指尖。
        /// </summary>
        public void SetTouch(int finger, int segment, bool touched)
        {
            if (InvokeRequired) { BeginInvoke(() => SetTouch(finger, segment, touched)); return; }
            _hand.SetTouch(finger, segment, touched);
        }

        private void Hand_TouchToggled(object? sender, TouchEventArgs e)
        {
            _touchStrip.Invalidate();
            string where = $"{Fingers[e.Finger].Zh} 第 {e.Segment + 1} 節";
            if (e.Touched)
            {
                _status.Message = $"偵測到觸碰：{where}";
                Log($"觸碰 ● {where}");
            }
            else
            {
                _status.Message = $"觸碰解除：{where}";
            }
        }

        private void TouchStrip_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var p = _touchStrip;

            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, 0, p.Width, 0);

            // 總數
            int count = _hand.TouchedCount;
            int total = HandView.TotalSegments;
            int x = 20;
            TextRenderer.DrawText(g, Theme.Spaced("Touch points"), Theme.Caption, new Point(x, 12), Theme.Muted);
            var countText = count.ToString();
            TextRenderer.DrawText(g, countText, Theme.Value, new Point(x - 2, 27), count > 0 ? Theme.Touch : Theme.Text);
            var cw = TextRenderer.MeasureText(g, countText, Theme.Value).Width;
            TextRenderer.DrawText(g, $"/ {total}", Theme.Small, new Point(x + cw - 6, 31), Theme.Muted);
            x += 120;

            // 各指節狀態點
            int cy = p.Height / 2;
            for (int f = 0; f < HandView.FingerCount; f++)
            {
                TextRenderer.DrawText(g, Fingers[f].Short, Theme.BodyBold,
                    new Rectangle(x, 0, 20, p.Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += 22;
                for (int s = 0; s < HandView.SegmentCount(f); s++)
                {
                    var r = new Rectangle(x, cy - 5, 10, 10);
                    if (_hand.GetTouch(f, s))
                    {
                        using var b = new SolidBrush(Theme.Touch);
                        g.FillEllipse(b, r);
                    }
                    else
                    {
                        using var pen = new Pen(Theme.Border, 1.5f);
                        g.DrawEllipse(pen, r);
                    }
                    x += 14;
                }
                x += 16;
            }
        }

        // ═════════════════════════════════════════════════════
        //  事件紀錄
        // ═════════════════════════════════════════════════════
        private void Log(string message)
        {
            _log.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}\t{message}");
            while (_log.Items.Count > 200) _log.Items.RemoveAt(_log.Items.Count - 1);
        }

        private void Log_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, e.Bounds);

            var parts = (_log.Items[e.Index]?.ToString() ?? "").Split('\t', 2);
            var time = parts[0];
            var msg = parts.Length > 1 ? parts[1] : "";

            TextRenderer.DrawText(g, time, Theme.Caption, new Rectangle(e.Bounds.X, e.Bounds.Y, 64, e.Bounds.Height),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, msg, Theme.Small,
                new Rectangle(e.Bounds.X + 66, e.Bounds.Y, e.Bounds.Width - 66, e.Bounds.Height),
                msg.StartsWith("觸碰") ? Theme.Touch : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            using (var pen = new Pen(Color.FromArgb(240, 243, 247)))
                g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ═════════════════════════════════════════════════════
        //  資源
        // ═════════════════════════════════════════════════════
        /// <summary>從內嵌資源載入手部影像（不再依賴 D:\ 絕對路徑）。</summary>
        private static Image? LoadHandImage()
        {
            using var stream = typeof(FormHandDemo).Assembly.GetManifestResourceStream("RobotHandDemo.Assets.hand.png");
            if (stream == null) return null;
            using var temp = Image.FromStream(stream);
            return new Bitmap(temp);
        }
    }
}
