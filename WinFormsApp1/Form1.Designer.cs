namespace RobotHandDemo
{
    partial class FormHandDemo
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                _animTimer?.Dispose();
                _clockTimer?.Dispose();
                _handImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 只放表單本身的屬性；所有版面元件在 Form1.cs 的 BuildLayout() 以程式建立，
        /// 方便統一套用 Theme 並支援視窗縮放。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();
            //
            // FormHandDemo
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.MinimumSize = new System.Drawing.Size(1100, 760);
            this.DoubleBuffered = true;
            this.Name = "FormHandDemo";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "靈巧手控制面板 - Dexterous Hand Control";
            this.ResumeLayout(false);
        }
    }
}
