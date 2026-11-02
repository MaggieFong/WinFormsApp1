using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RobotHandDemo
{
    public partial class FormHandDemo : Form
    {
        private readonly int[] fingerFlexion = new int[5] { 0, 0, 0, 0, 0 };
        private Image robotHandImg;
        public FormHandDemo()
        {
            InitializeComponent();
            robotHandImg = Image.FromFile(@"D:\Robot\Code\WinFormsApp1\WinFormsApp1\bin\Debug\net8.0-windows\Resource\hand.png");
            canvasPanel.Paint += CanvasPanel_Paint;
        }

        private void OnSliderValueChanged(int fingerIndex, int value)
        {
            fingerFlexion[fingerIndex] = value;
            canvasPanel.Invalidate();
        }

        private void ApplyPreset(string preset)
        {
            int[] targets = preset switch
            {
                "±i¶}" => new int[] { 0, 0, 0, 0, 0 },
                "´¤®±" => new int[] { 100, 100, 100, 100, 100 },
                "¤ñÆg" => new int[] { 0, 100, 100, 100, 100 },
                "°Å¤M(YA)" => new int[] { 100, 0, 0, 100, 100 },
                "OK" => new int[] { 85, 85, 0, 0, 0 },
                _ => new int[] { 0, 0, 0, 0, 0 }
            };

            for (int i = 0; i < 5; i++)
            {
                sliders[i].Value = targets[i];
                fingerFlexion[i] = targets[i];
            }
            canvasPanel.Invalidate();
        }

        //private void CanvasPanel_Paint(object sender, PaintEventArgs e)
        //{
        //    Graphics g = e.Graphics;
        //    g.SmoothingMode = SmoothingMode.AntiAlias;

        //    Rectangle palmRect = new Rectangle(140, 240, 180, 130);

        //    using (SolidBrush palmBrush = new SolidBrush(Color.FromArgb(45, 52, 54)))
        //    using (Pen borderPen = new Pen(Color.Cyan, 2.5f))
        //    {
        //        g.FillRoundedRectangle(palmBrush, palmRect, 20);
        //        g.DrawRoundedRectangle(borderPen, palmRect, 20);

        //        Rectangle wrist = new Rectangle(180, 370, 100, 60);
        //        g.FillRectangle(palmBrush, wrist);
        //        g.DrawRectangle(borderPen, wrist);
        //    }

        //    Point[] baseJoints = new Point[]
        //    {
        //        new Point(145, 310),
        //        new Point(165, 240),
        //        new Point(210, 235),
        //        new Point(255, 240),
        //        new Point(295, 255)
        //    };

        //    float[] baseAngles = { -135f, -95f, -88f, -80f, -65f };
        //    float[] fingerLengths = { 45f, 60f, 68f, 62f, 50f };

        //    for (int i = 0; i < 5; i++)
        //    {
        //        DrawFinger(g, baseJoints[i], baseAngles[i], fingerLengths[i], fingerFlexion[i]);
        //    }
        //}
        private void CanvasPanel_Paint(object sender, PaintEventArgs e)
        {
            var panelSize = canvasPanel.ClientSize;

            float ratioX = (float)panelSize.Width / robotHandImg.Width;
            float ratioY = (float)panelSize.Height / robotHandImg.Height;

            float ratio = Math.Min(ratioX, ratioY);

            int drawWidth = (int)(robotHandImg.Width * ratio);
            int drawHeight = (int)(robotHandImg.Height * ratio);

            int x = (panelSize.Width - drawWidth) / 2;
            int y = (panelSize.Height - drawHeight) / 2;

            e.Graphics.DrawImage(
                robotHandImg,
                x,
                y,
                drawWidth,
                drawHeight);
        }

        private void DrawFinger(Graphics g, Point startPoint, float baseAngleDeg, float totalLength, int flexPercent)
        {
            float flexRatio = flexPercent / 100.0f;
            float segLen = totalLength / 3.0f;
            float currentAngle = baseAngleDeg;

            using Pen bonePen = new Pen(Color.FromArgb(0, 210, 255), 5.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            PointF currentPt = startPoint;

            for (int seg = 0; seg < 3; seg++)
            {
                currentAngle += (flexRatio * 28f);
                float effectiveLen = segLen * (1.0f - flexRatio * 0.45f);
                double rad = currentAngle * Math.PI / 180.0;

                PointF nextPt = new PointF(
                    currentPt.X + (float)(effectiveLen * Math.Cos(rad)),
                    currentPt.Y + (float)(effectiveLen * Math.Sin(rad))
                );

                g.DrawLine(bonePen, currentPt, nextPt);
                g.FillEllipse(Brushes.DeepSkyBlue, currentPt.X - 5, currentPt.Y - 5, 10, 10);
                currentPt = nextPt;
            }

            g.FillEllipse(Brushes.SpringGreen, currentPt.X - 4, currentPt.Y - 4, 8, 8);
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using GraphicsPath path = RoundedRect(bounds, radius);
            g.FillPath(brush, path);
        }

        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
        {
            using GraphicsPath path = RoundedRect(bounds, radius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0) { path.AddRectangle(bounds); return path; }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
