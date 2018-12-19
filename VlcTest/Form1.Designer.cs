namespace VlcTest
{
    partial class Form1
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.trackBarPosition = new System.Windows.Forms.TrackBar();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.buttonPlay = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.buttonOpenFile = new System.Windows.Forms.Button();
            this.buttonDisconnect = new System.Windows.Forms.Button();
            this.trackBarVolume = new System.Windows.Forms.TrackBar();
            this.checkBoxMute = new System.Windows.Forms.CheckBox();
            this.trackBarBlur = new System.Windows.Forms.TrackBar();
            this.label1 = new System.Windows.Forms.Label();
            this.labelTotalTime = new System.Windows.Forms.Label();
            this.labelCurrentTime = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.trackBarBrightness = new System.Windows.Forms.TrackBar();
            this.trackBarHue = new System.Windows.Forms.TrackBar();
            this.trackBarContrast = new System.Windows.Forms.TrackBar();
            this.trackBarGamma = new System.Windows.Forms.TrackBar();
            this.trackBarSaturation = new System.Windows.Forms.TrackBar();
            this.buttonResetVideoAdjustments = new System.Windows.Forms.Button();
            this.checkBoxVideoAdjustments = new System.Windows.Forms.CheckBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.checkBoxLoopPlayback = new System.Windows.Forms.CheckBox();
            this.button4 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.elementHost1 = new System.Windows.Forms.Integration.ElementHost();
            this.videoControl = new VlcTest.VideoControl();
            this.button6 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarPosition)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarBlur)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarBrightness)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarHue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarContrast)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarGamma)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarSaturation)).BeginInit();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.Location = new System.Drawing.Point(12, 402);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox1.Size = new System.Drawing.Size(755, 144);
            this.textBox1.TabIndex = 5;
            // 
            // buttonConnect
            // 
            this.buttonConnect.Location = new System.Drawing.Point(12, 5);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(75, 23);
            this.buttonConnect.TabIndex = 6;
            this.buttonConnect.Text = "Start";
            this.buttonConnect.UseVisualStyleBackColor = true;
            this.buttonConnect.Click += new System.EventHandler(this.buttonPlay_Click);
            // 
            // trackBarPosition
            // 
            this.trackBarPosition.AutoSize = false;
            this.trackBarPosition.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trackBarPosition.LargeChange = 10;
            this.trackBarPosition.Location = new System.Drawing.Point(31, 3);
            this.trackBarPosition.Maximum = 1000;
            this.trackBarPosition.Name = "trackBarPosition";
            this.trackBarPosition.Size = new System.Drawing.Size(558, 20);
            this.trackBarPosition.TabIndex = 7;
            this.trackBarPosition.TickFrequency = 10;
            this.trackBarPosition.Scroll += new System.EventHandler(this.trackBar1_Scroll);
            this.trackBarPosition.ValueChanged += new System.EventHandler(this.trackBar1_ValueChanged);
            this.trackBarPosition.MouseDown += new System.Windows.Forms.MouseEventHandler(this.trackBarPosition_MouseDown);
            this.trackBarPosition.MouseUp += new System.Windows.Forms.MouseEventHandler(this.trackBarPosition_MouseUp);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(666, 5);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(101, 23);
            this.button1.TabIndex = 8;
            this.button1.Text = "GetYoutubeLink";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(98, 46);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(326, 20);
            this.textBox2.TabIndex = 9;
            this.textBox2.Text = "https://www.youtube.com/watch?v=7G_fYgW5Tys";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(330, 340);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(129, 23);
            this.button2.TabIndex = 12;
            this.button2.Text = "SetBinding";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(330, 369);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(129, 23);
            this.button3.TabIndex = 13;
            this.button3.Text = "Invalidate";
            this.button3.UseVisualStyleBackColor = true;
            //this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // buttonPlay
            // 
            this.buttonPlay.Location = new System.Drawing.Point(267, 72);
            this.buttonPlay.Name = "buttonPlay";
            this.buttonPlay.Size = new System.Drawing.Size(75, 23);
            this.buttonPlay.TabIndex = 14;
            this.buttonPlay.Text = "Play";
            this.buttonPlay.UseVisualStyleBackColor = true;
            this.buttonPlay.Click += new System.EventHandler(this.buttonPlay_Click_1);
            // 
            // buttonStop
            // 
            this.buttonStop.Location = new System.Drawing.Point(348, 72);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(75, 23);
            this.buttonStop.TabIndex = 16;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // buttonOpenFile
            // 
            this.buttonOpenFile.Location = new System.Drawing.Point(99, 72);
            this.buttonOpenFile.Name = "buttonOpenFile";
            this.buttonOpenFile.Size = new System.Drawing.Size(82, 23);
            this.buttonOpenFile.TabIndex = 17;
            this.buttonOpenFile.Text = "Open File";
            this.buttonOpenFile.UseVisualStyleBackColor = true;
            this.buttonOpenFile.Click += new System.EventHandler(this.buttonOpenFile_Click);
            // 
            // buttonDisconnect
            // 
            this.buttonDisconnect.Location = new System.Drawing.Point(94, 5);
            this.buttonDisconnect.Name = "buttonDisconnect";
            this.buttonDisconnect.Size = new System.Drawing.Size(87, 23);
            this.buttonDisconnect.TabIndex = 18;
            this.buttonDisconnect.Text = "Close";
            this.buttonDisconnect.UseVisualStyleBackColor = true;
            this.buttonDisconnect.Click += new System.EventHandler(this.buttonDisconnect_Click);
            // 
            // trackBarVolume
            // 
            this.trackBarVolume.AutoSize = false;
            this.trackBarVolume.LargeChange = 10;
            this.trackBarVolume.Location = new System.Drawing.Point(532, 171);
            this.trackBarVolume.Maximum = 100;
            this.trackBarVolume.Name = "trackBarVolume";
            this.trackBarVolume.Size = new System.Drawing.Size(89, 20);
            this.trackBarVolume.TabIndex = 19;
            this.trackBarVolume.TickFrequency = 10;
            this.trackBarVolume.Value = 100;
            this.trackBarVolume.Scroll += new System.EventHandler(this.trackBarVolume_Scroll);
            this.trackBarVolume.ValueChanged += new System.EventHandler(this.trackBarVolume_ValueChanged);
            // 
            // checkBoxMute
            // 
            this.checkBoxMute.AutoSize = true;
            this.checkBoxMute.Location = new System.Drawing.Point(541, 197);
            this.checkBoxMute.Name = "checkBoxMute";
            this.checkBoxMute.Size = new System.Drawing.Size(50, 17);
            this.checkBoxMute.TabIndex = 20;
            this.checkBoxMute.Text = "Mute";
            this.checkBoxMute.UseVisualStyleBackColor = true;
            this.checkBoxMute.CheckedChanged += new System.EventHandler(this.checkBoxMute_CheckedChanged);
            // 
            // trackBarBlur
            // 
            this.trackBarBlur.AutoSize = false;
            this.trackBarBlur.Location = new System.Drawing.Point(735, 193);
            this.trackBarBlur.Maximum = 100;
            this.trackBarBlur.Name = "trackBarBlur";
            this.trackBarBlur.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.trackBarBlur.Size = new System.Drawing.Size(20, 136);
            this.trackBarBlur.TabIndex = 21;
            this.trackBarBlur.TickFrequency = 10;
            this.trackBarBlur.ValueChanged += new System.EventHandler(this.trackBarBlur_ValueChanged);
            // 
            // label1
            // 
            this.label1.AutoEllipsis = true;
            this.label1.Location = new System.Drawing.Point(735, 171);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(30, 19);
            this.label1.TabIndex = 22;
            this.label1.Text = "--";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelTotalTime
            // 
            this.labelTotalTime.AutoSize = true;
            this.labelTotalTime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTotalTime.Location = new System.Drawing.Point(595, 3);
            this.labelTotalTime.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.labelTotalTime.Name = "labelTotalTime";
            this.labelTotalTime.Size = new System.Drawing.Size(22, 23);
            this.labelTotalTime.TabIndex = 23;
            this.labelTotalTime.Text = "--:--";
            // 
            // labelCurrentTime
            // 
            this.labelCurrentTime.AutoSize = true;
            this.labelCurrentTime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelCurrentTime.Location = new System.Drawing.Point(3, 3);
            this.labelCurrentTime.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.labelCurrentTime.Name = "labelCurrentTime";
            this.labelCurrentTime.Size = new System.Drawing.Size(22, 23);
            this.labelCurrentTime.TabIndex = 24;
            this.labelCurrentTime.Text = "--:--";
            this.labelCurrentTime.Click += new System.EventHandler(this.labelCurrentTime_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.labelTotalTime, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelCurrentTime, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.trackBarPosition, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(48, 125);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(620, 26);
            this.tableLayoutPanel1.TabIndex = 25;
            // 
            // trackBarBrightness
            // 
            this.trackBarBrightness.AutoSize = false;
            this.trackBarBrightness.LargeChange = 10;
            this.trackBarBrightness.Location = new System.Drawing.Point(94, 205);
            this.trackBarBrightness.Maximum = 200;
            this.trackBarBrightness.Name = "trackBarBrightness";
            this.trackBarBrightness.Size = new System.Drawing.Size(127, 20);
            this.trackBarBrightness.TabIndex = 26;
            this.trackBarBrightness.TickFrequency = 10;
            this.trackBarBrightness.Value = 100;
            this.trackBarBrightness.ValueChanged += new System.EventHandler(this.trackBarBrightness_ValueChanged);
            // 
            // trackBarHue
            // 
            this.trackBarHue.AutoSize = false;
            this.trackBarHue.LargeChange = 10;
            this.trackBarHue.Location = new System.Drawing.Point(94, 257);
            this.trackBarHue.Maximum = 360;
            this.trackBarHue.Name = "trackBarHue";
            this.trackBarHue.Size = new System.Drawing.Size(127, 20);
            this.trackBarHue.TabIndex = 27;
            this.trackBarHue.TickFrequency = 10;
            this.trackBarHue.ValueChanged += new System.EventHandler(this.trackBarHue_ValueChanged);
            // 
            // trackBarContrast
            // 
            this.trackBarContrast.AutoSize = false;
            this.trackBarContrast.LargeChange = 10;
            this.trackBarContrast.Location = new System.Drawing.Point(94, 231);
            this.trackBarContrast.Maximum = 200;
            this.trackBarContrast.Name = "trackBarContrast";
            this.trackBarContrast.Size = new System.Drawing.Size(127, 20);
            this.trackBarContrast.TabIndex = 28;
            this.trackBarContrast.TickFrequency = 10;
            this.trackBarContrast.Value = 100;
            this.trackBarContrast.ValueChanged += new System.EventHandler(this.trackBarContrast_ValueChanged);
            // 
            // trackBarGamma
            // 
            this.trackBarGamma.AutoSize = false;
            this.trackBarGamma.LargeChange = 10;
            this.trackBarGamma.Location = new System.Drawing.Point(94, 283);
            this.trackBarGamma.Maximum = 1000;
            this.trackBarGamma.Name = "trackBarGamma";
            this.trackBarGamma.Size = new System.Drawing.Size(127, 20);
            this.trackBarGamma.TabIndex = 29;
            this.trackBarGamma.TickFrequency = 100;
            this.trackBarGamma.Value = 10;
            this.trackBarGamma.ValueChanged += new System.EventHandler(this.trackBarGamma_ValueChanged);
            // 
            // trackBarSaturation
            // 
            this.trackBarSaturation.AutoSize = false;
            this.trackBarSaturation.LargeChange = 10;
            this.trackBarSaturation.Location = new System.Drawing.Point(94, 309);
            this.trackBarSaturation.Maximum = 300;
            this.trackBarSaturation.Name = "trackBarSaturation";
            this.trackBarSaturation.Size = new System.Drawing.Size(127, 20);
            this.trackBarSaturation.TabIndex = 30;
            this.trackBarSaturation.TickFrequency = 10;
            this.trackBarSaturation.Value = 100;
            this.trackBarSaturation.ValueChanged += new System.EventHandler(this.trackBarSaturation_ValueChanged);
            // 
            // buttonResetVideoAdjustments
            // 
            this.buttonResetVideoAdjustments.Location = new System.Drawing.Point(144, 340);
            this.buttonResetVideoAdjustments.Name = "buttonResetVideoAdjustments";
            this.buttonResetVideoAdjustments.Size = new System.Drawing.Size(77, 23);
            this.buttonResetVideoAdjustments.TabIndex = 31;
            this.buttonResetVideoAdjustments.Text = "Reset";
            this.buttonResetVideoAdjustments.UseVisualStyleBackColor = true;
            this.buttonResetVideoAdjustments.Click += new System.EventHandler(this.buttonResetVideoAdjustments_Click);
            // 
            // checkBoxVideoAdjustments
            // 
            this.checkBoxVideoAdjustments.AutoSize = true;
            this.checkBoxVideoAdjustments.Location = new System.Drawing.Point(98, 182);
            this.checkBoxVideoAdjustments.Name = "checkBoxVideoAdjustments";
            this.checkBoxVideoAdjustments.Size = new System.Drawing.Size(83, 17);
            this.checkBoxVideoAdjustments.TabIndex = 32;
            this.checkBoxVideoAdjustments.Text = "Adjustments";
            this.checkBoxVideoAdjustments.UseVisualStyleBackColor = true;
            this.checkBoxVideoAdjustments.CheckedChanged += new System.EventHandler(this.checkBoxVideoAdjustments_CheckedChanged);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(615, 72);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(130, 17);
            this.checkBox1.TabIndex = 33;
            this.checkBox1.Text = "PlayerWindowVisibility";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // checkBoxLoopPlayback
            // 
            this.checkBoxLoopPlayback.AutoSize = true;
            this.checkBoxLoopPlayback.Location = new System.Drawing.Point(267, 101);
            this.checkBoxLoopPlayback.Name = "checkBoxLoopPlayback";
            this.checkBoxLoopPlayback.Size = new System.Drawing.Size(94, 17);
            this.checkBoxLoopPlayback.TabIndex = 34;
            this.checkBoxLoopPlayback.Text = "LoopPlayback";
            this.checkBoxLoopPlayback.UseVisualStyleBackColor = true;
            this.checkBoxLoopPlayback.CheckedChanged += new System.EventHandler(this.checkBoxLoopPlayback_CheckedChanged);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(267, 202);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(87, 23);
            this.button4.TabIndex = 35;
            this.button4.Text = "CloseChannel";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(372, 205);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(87, 23);
            this.button5.TabIndex = 36;
            this.button5.Text = "GetStats";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // elementHost1
            // 
            this.elementHost1.Location = new System.Drawing.Point(483, 243);
            this.elementHost1.Name = "elementHost1";
            this.elementHost1.Size = new System.Drawing.Size(228, 140);
            this.elementHost1.TabIndex = 37;
            this.elementHost1.Text = "elementHost1";
            this.elementHost1.Child = this.videoControl;
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(372, 306);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(87, 23);
            this.button6.TabIndex = 38;
            this.button6.Text = "Start";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(779, 558);
            this.Controls.Add(this.button6);
            this.Controls.Add(this.elementHost1);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.checkBoxLoopPlayback);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.checkBoxVideoAdjustments);
            this.Controls.Add(this.buttonResetVideoAdjustments);
            this.Controls.Add(this.trackBarSaturation);
            this.Controls.Add(this.trackBarGamma);
            this.Controls.Add(this.trackBarContrast);
            this.Controls.Add(this.trackBarHue);
            this.Controls.Add(this.trackBarBrightness);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.trackBarBlur);
            this.Controls.Add(this.checkBoxMute);
            this.Controls.Add(this.trackBarVolume);
            this.Controls.Add(this.buttonDisconnect);
            this.Controls.Add(this.buttonOpenFile);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonPlay);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.buttonConnect);
            this.Controls.Add(this.textBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.trackBarPosition)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarBlur)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarBrightness)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarHue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarContrast)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarGamma)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarSaturation)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.TrackBar trackBarPosition;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox textBox2;
       // private UserControl1 userControl11;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button buttonPlay;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Button buttonOpenFile;
        private System.Windows.Forms.Button buttonDisconnect;
        private System.Windows.Forms.TrackBar trackBarVolume;
        private System.Windows.Forms.CheckBox checkBoxMute;
        private System.Windows.Forms.TrackBar trackBarBlur;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelTotalTime;
        private System.Windows.Forms.Label labelCurrentTime;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TrackBar trackBarBrightness;
        private System.Windows.Forms.TrackBar trackBarHue;
        private System.Windows.Forms.TrackBar trackBarContrast;
        private System.Windows.Forms.TrackBar trackBarGamma;
        private System.Windows.Forms.TrackBar trackBarSaturation;
        private System.Windows.Forms.Button buttonResetVideoAdjustments;
        private System.Windows.Forms.CheckBox checkBoxVideoAdjustments;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.CheckBox checkBoxLoopPlayback;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.Integration.ElementHost elementHost1;
        private VideoControl videoControl;
        private System.Windows.Forms.Button button6;
    }
}

