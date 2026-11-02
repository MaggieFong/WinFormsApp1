using WinFormsApp1;

namespace RobotHandDemo
{
    partial class FormHandDemo
    {
        private System.ComponentModel.IContainer components = null;
        private DoubleBufferedPanel canvasPanel;
        private System.Windows.Forms.Panel controlPanel;
        private System.Windows.Forms.TrackBar[] sliders;
        private System.Windows.Forms.Label[] fingerLabels;
        private System.Windows.Forms.Label presetLabel;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnFist;
        private System.Windows.Forms.Button btnThumbUp;
        private System.Windows.Forms.Button btnScissors;
        private System.Windows.Forms.Button btnOK;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.canvasPanel = new RobotHandDemo.DoubleBufferedPanel();
            this.controlPanel = new System.Windows.Forms.Panel();
            this.presetLabel = new System.Windows.Forms.Label();
            this.btnOpen = new System.Windows.Forms.Button();
            this.btnFist = new System.Windows.Forms.Button();
            this.btnThumbUp = new System.Windows.Forms.Button();
            this.btnScissors = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.sliders = new System.Windows.Forms.TrackBar[5];
            this.fingerLabels = new System.Windows.Forms.Label[5];
            this.controlPanel.SuspendLayout();
            this.SuspendLayout();

            // 
            // canvasPanel
            // 
            //this.canvasPanel.BackColor = System.Drawing.Color.FromArgb(20, 20, 25);
            this.canvasPanel.BackColor = Color.FromArgb(0, 58, 145);
            this.canvasPanel.Location = new System.Drawing.Point(20, 20);
            this.canvasPanel.Name = "canvasPanel";
            this.canvasPanel.Size = new System.Drawing.Size(460, 480);
            this.canvasPanel.TabIndex = 0;
            this.canvasPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.CanvasPanel_Paint);

            // 
            // controlPanel
            // 
            this.controlPanel.BackColor = System.Drawing.Color.FromArgb(40, 40, 48);
            this.controlPanel.Location = new System.Drawing.Point(500, 20);
            this.controlPanel.Name = "controlPanel";
            this.controlPanel.Size = new System.Drawing.Size(290, 480);
            this.controlPanel.TabIndex = 1;

            // 
            // 滑桿與標籤動態配置 (五指)
            // 
            string[] fingerNames = { "拇指 (Thumb)", "食指 (Index)", "中指 (Middle)", "無名指 (Ring)", "小指 (Pinky)" };
            int startY = 15;

            for (int i = 0; i < 5; i++)
            {
                int index = i;

                this.fingerLabels[i] = new System.Windows.Forms.Label();
                this.fingerLabels[i].AutoSize = true;
                this.fingerLabels[i].Font = new System.Drawing.Font("Microsoft JhengHei", 9F, System.Drawing.FontStyle.Bold);
                this.fingerLabels[i].ForeColor = System.Drawing.Color.Gainsboro;
                this.fingerLabels[i].Location = new System.Drawing.Point(15, startY);
                this.fingerLabels[i].Name = $"lblFinger{i}";
                this.fingerLabels[i].Text = fingerNames[i];
                this.controlPanel.Controls.Add(this.fingerLabels[i]);

                this.sliders[i] = new System.Windows.Forms.TrackBar();
                this.sliders[i].Location = new System.Drawing.Point(15, startY + 20);
                this.sliders[i].Maximum = 100;
                this.sliders[i].Minimum = 0;
                this.sliders[i].Name = $"tbFinger{i}";
                this.sliders[i].Size = new System.Drawing.Size(260, 45);
                this.sliders[i].TickFrequency = 20;
                this.sliders[i].ValueChanged += (s, e) => this.OnSliderValueChanged(index, this.sliders[index].Value);
                this.controlPanel.Controls.Add(this.sliders[i]);

                startY += 65;
            }

            // 
            // presetLabel
            // 
            this.presetLabel.AutoSize = true;
            this.presetLabel.Font = new System.Drawing.Font("Microsoft JhengHei", 9F, System.Drawing.FontStyle.Bold);
            this.presetLabel.ForeColor = System.Drawing.Color.DeepSkyBlue;
            this.presetLabel.Location = new System.Drawing.Point(15, startY + 5);
            this.presetLabel.Name = "presetLabel";
            this.presetLabel.Text = "預設姿態:";
            this.controlPanel.Controls.Add(this.presetLabel);

            // 
            // 姿態按鈕配置
            // 
            Button[] buttons = { this.btnOpen, this.btnFist, this.btnThumbUp, this.btnScissors, this.btnOK };
            string[] btnTexts = { "張開", "握拳", "比讚", "剪刀(YA)", "OK" };
            int btnX = 15;
            int btnY = startY + 30;

            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i] = new System.Windows.Forms.Button();
                buttons[i].BackColor = System.Drawing.Color.FromArgb(60, 60, 75);
                buttons[i].FlatStyle = System.Windows.Forms.FlatStyle.Flat;
                buttons[i].ForeColor = System.Drawing.Color.White;
                buttons[i].Location = new System.Drawing.Point(btnX, btnY);
                buttons[i].Name = $"btnPreset_{i}";
                buttons[i].Size = new System.Drawing.Size(80, 32);
                buttons[i].Text = btnTexts[i];
                buttons[i].UseVisualStyleBackColor = false;

                string gesture = btnTexts[i];
                buttons[i].Click += (s, e) => this.ApplyPreset(gesture);
                this.controlPanel.Controls.Add(buttons[i]);

                btnX += 88;
                if (btnX + 80 > this.controlPanel.Width)
                {
                    btnX = 15;
                    btnY += 40;
                }
            }

            // 
            // FormHandDemo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(30, 30, 35);
            this.ClientSize = new System.Drawing.Size(814, 521);
            this.Controls.Add(this.controlPanel);
            this.Controls.Add(this.canvasPanel);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "FormHandDemo";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "機器人手掌控制面板 - Robot Hand Demo";
            this.controlPanel.ResumeLayout(false);
            this.controlPanel.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
