﻿using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VlcTest
{
    public partial class MainForm : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public MainForm()
        {
            InitializeComponent();

            // CreateVideoForm();

            //videoForm.Visible = true;

            playbackService = new PlaybackService();
           // playbackService.Setup();

            playbackService.StateChanged += new Action<string, object[]>(communicationService_StateChanged);


            UpdateUi();

            timer.Interval = 1000;

            //this.Load += (o, a) =>
            //{
            //    logger = LogManager.GetCurrentClassLogger();
            //};

            timer.Tick += (o, a) =>
            {
                int currPosition = trackBarPosition.Value;
                //tick += 500;
                // UpdatePosition();

                if (currPosition != prevPosition)
                {
                    UpdatePosition();
                }
            };
        }


        private VideoForm videoForm = null;

        private Timer timer = new Timer();
        internal VideoControl videoControl = new VideoControl();

        private PlaybackService playbackService = null;


        private void communicationService_StateChanged(string state, object[] args)
        {

            //logger.Debug("communicationService_StateChanged(...) " + state);

            if(state == "UpdateUi")
            {
                UpdateUi();
            }
            else if(state == "SetupDisplay")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);

                var eventId = args?[0]?.ToString();
                var memoryId = args?[1]?.ToString();

                CreateVideoControl(eventId, memoryId);
            }
            else if (state == "StartDisplay")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);
                videoControl.StartDisplay();
            }
            else if (state == "StopDisplay")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);

                videoControl.StopDisplay();
            }
            else if (state == "Stopped")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);
                videoControl.ClearDisplay();
                UpdateUi();
            }
            else if (state == "LengthChanged")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);

                var val0 = args[0].ToString();
                SetMediaLength(val0);

            }
            else if (state == "Position")
            {
                var val0 = args[0].ToString();

                SetPosition(val0);
            }
            else if (state == "Closed")
            {
                logger.Debug("communicationService_StateChanged(...) " + state);

                videoControl.ClearDisplay();
                UpdateUi();
            }
        }


        internal void UpdateUi()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {

                    UpdateUi();

                }));
            }
            else
            {
                trackBarPosition.Enabled = !playbackService.IsStopped;
                if (playbackService.IsStopped || !playbackService.IsConnected)
                {
                    labelCurrentTime.Text = "--:--";
                    labelTotalTime.Text = "--:--";
                    trackBarPosition.Value = 0;
                }

                buttonPlay.Text = (playbackService.IsPaused || playbackService.IsStopped || !playbackService.IsConnected) ? "Play" : "Pause";

            }
        }

        public void SetupVideoForm()
        {
            this.Invoke((Action)(() =>
            {
                //CreateVideoForm();

                videoForm.Visible = true;


            }));

        }

        public void CreateVideoControl(string appId, string memoryId)
        {
            this.Invoke((Action)(() =>
            {
                //CreateVideoForm();

                videoControl = new VideoControl();

                videoControl.Setup(appId, memoryId);

                videoForm.InitVideoControl(videoControl);

                if (!videoForm.Visible)
                {
                    videoForm.Visible = true;
                }

            }));

        }

        private void CreateVideoForm()
        {
            if (videoForm == null || videoForm.IsDisposed || videoForm.Disposing)
            {

                videoForm = new VideoForm();

                //videoForm.InitVideoControl(videoControl);

                videoForm.FormClosing += (o, a) =>
                {
                    a.Cancel = true;
                    videoForm.Visible = false;
                    playbackService.Stop();

                   
                };
                videoForm.Visible = true;
                //videoForm.FormClosed += (o, a) =>
                //{
                //    PostMessage("Stop");
                //};
            }

            if (!videoForm.Visible)
            {
                videoForm.Visible = true;
            }

            //videoControl.Clear();
        }



       // private static Process CurrentProccess = Process.GetCurrentProcess();

        private void buttonStart_Click(object sender, EventArgs e)
        {

            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonStart_Click(...)");

            ////string fileName = currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";
            //string fileName = "";//currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";

            var fileName = this.textBox2.Text;

            PlayFile(fileName);

        }


        private void buttonDisconnect_Click(object sender, EventArgs e)
        {

            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonDisconnect_Click(...)");

            playbackService.Close();


        }




        private void SetPlaybackParameter(string command, object[] args = null)
        {
            if (playbackService == null)
            {
                return;
            }

            playbackService.ExecuteCommand(command, args);

        }



        private void buttonPlay_Click_1(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonPlay_Click_1(...)");

            //if (playbackStarting)
            //{
            //    Debug.WriteLine("processStarting " + playbackStarting);
            //    return;
            //}

            currentMediaFile = textBox2.Text;
            if (string.IsNullOrEmpty(currentMediaFile))
            {
                OpenFileDialog dlg = new OpenFileDialog();

                var result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    currentMediaFile = dlg.FileName;
                    textBox2.Text = currentMediaFile;
                }
            }

            currentMediaFile = textBox2.Text;
            PlayFile(currentMediaFile);
        }

        private void PlayFile(string uri, bool forse = false)
        {
            Debug.WriteLine("PlayFile(...)");

            CreateVideoForm();

            if (playbackService == null)
            {
                playbackService = new PlaybackService();
                playbackService.StateChanged += new Action<string, object[]>(communicationService_StateChanged);

            }

            playbackService?.Play(uri, forse);

        }


 
        private void buttonPause_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonPause_Click(...)");

            playbackService?.Pause();

        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonStop_Click(...)");

            playbackService?.Stop();

         }

        private string currentMediaFile = "";
        private void buttonOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {

                currentMediaFile = dlg.FileName;

                Debug.WriteLine(">>>>>>>>>> buttonOpenFile_Click(...) " + currentMediaFile);

                textBox2.Text = currentMediaFile;

                if (File.Exists(currentMediaFile))
                {
                    PlayFile(currentMediaFile, true);

                    //if (IsPaused || IsStopped)
                    //{
                    //    CreateVideoForm();

                    //    PostMessage("Play", new[] { currentMediaFile });
                    //}
                }
            }
        }

        private void checkBoxMute_CheckedChanged(object sender, EventArgs e)
        {
            playbackService.SetMute(checkBoxMute.Checked);

        }


        private int prevPosition = 0;
        private volatile bool mouseDown = false;
        private void trackBarPosition_MouseUp(object sender, MouseEventArgs e)
        {
            int currPosition = trackBarPosition.Value;

            timer.Enabled = false;
            mouseDown = false;

            if (currPosition != prevPosition)
            {
                UpdatePosition();
            }

            prevPosition = currPosition;
        }

        private void UpdatePosition()
        {
            var position = trackBarPosition.Value / (double)(trackBarPosition.Maximum - trackBarPosition.Minimum);

            long currTime = (long)(totalMediaLength * position);
            TimeSpan ts = TimeSpan.FromMilliseconds(currTime);

            labelCurrentTime.Text = ts.ToString("hh\\:mm\\:ss");

            playbackService.SetPosition(position);

           // SetPlaybackParameter("Position", new object[] { position });
        }

        internal long totalMediaLength = 0;
        public void SetMediaLength(string val0)
        {
            if (long.TryParse(val0, out totalMediaLength))
            {
                TimeSpan ts = TimeSpan.FromMilliseconds(totalMediaLength);

                Invoke(new Action(() =>
                {
                    labelTotalTime.Text = ts.ToString("hh\\:mm\\:ss");

                }));
            }
        }
        float mediaPosition = 0;
        public void SetPosition(string val0)
        {
            if (float.TryParse(val0, out mediaPosition))
            {
                long currTime = (long)(totalMediaLength * mediaPosition);
                TimeSpan ts = TimeSpan.FromMilliseconds(currTime);

                Invoke(new Action(() =>
                {

                    if (!mouseDown)
                    {
                        labelCurrentTime.Text = ts.ToString("hh\\:mm\\:ss");

                        var pos = (int)(mediaPosition * trackBarPosition.Maximum);
                        if (trackBarPosition.Value != pos)
                        {
                            trackBarPosition.Value = pos;
                        }
                    }
                }));
            }
        }

        private void trackBarPosition_MouseDown(object sender, MouseEventArgs e)
        {
            timer.Enabled = true;
            mouseDown = true;
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            if (mouseDown)
            {
                //var position = trackBarPosition.Value / (double)(trackBarPosition.Maximum - trackBarPosition.Minimum);
                //PostMessage("Position;" + position + ";");
            }
        }

        private void trackBarVolume_ValueChanged(object sender, EventArgs e)
        {
            var vol = (int)trackBarVolume.Value;

            playbackService.SetVolume(vol);
           

        }

        private void trackBarBlur_ValueChanged(object sender, EventArgs e)
        {
            var val = (int)trackBarBlur.Value;
            var effect = videoControl?.BlurEffect;
            if (effect != null)
            {
                effect.Radius = (double)val;
            }

            label1.Text = val.ToString();
            SetPlaybackParameter("Blur", new object[] { val });
        }

        private void labelCurrentTime_Click(object sender, EventArgs e)
        {

        }

        private void trackBarBrightness_ValueChanged(object sender, EventArgs e)
        {
            var val = trackBarBrightness.Value / 100.0; //(float)(trackBarBrightness.Maximum - trackBarBrightness.Minimum));

            //PostMessage("Brightness", new object[] { val });

            playbackService.SetVideoAdjustments(new object[] { "Brightness", val });

        }

        private void trackBarHue_ValueChanged(object sender, EventArgs e)
        {
            var val = (float)trackBarHue.Value;//(trackBarHue.Value / (float)(trackBarHue.Maximum - trackBarHue.Minimum));
                                               // PostMessage("Hue", new object[] { val });

            playbackService.SetVideoAdjustments(new object[] { "Hue", val });

        }

        private void trackBarContrast_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarContrast.Value / 100.0); // (float)(trackBarContrast.Maximum - trackBarContrast.Minimum));

            playbackService.SetVideoAdjustments(new object[] { "Contrast", val });

        }

        private void trackBarGamma_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarGamma.Value / 100.0); //(float)(trackBarGamma.Maximum - trackBarGamma.Minimum));

            playbackService.SetVideoAdjustments(new object[] { "Gamma", val });

        }

        private void trackBarSaturation_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarSaturation.Value / 100.0); // (float)(trackBarSaturation.Maximum - trackBarSaturation.Minimum));
                                                          //var val = (int)trackBarSaturation.Value;

            playbackService.SetVideoAdjustments(new object[] { "Saturation", val });
        }

        private void checkBoxVideoAdjustments_CheckedChanged(object sender, EventArgs e)
        {
            var enable = checkBoxVideoAdjustments.Checked ? 1 : 0;
            playbackService.SetVideoAdjustments(new object[] { "Enable", enable });

        }

        private void buttonResetVideoAdjustments_Click(object sender, EventArgs e)
        {
            SetPlaybackParameter("ResetVideoAdjustments");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //videoForm.Visible = true;
            SetPlaybackParameter("SwitchVisibilityState", new object[] { checkBox1.Checked });
        }

        private void checkBoxLoopPlayback_CheckedChanged(object sender, EventArgs e)
        {
            SetPlaybackParameter("SwitchLoopPlayback");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (playbackService != null)
            {
                playbackService.Close();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SetPlaybackParameter("GetStats");
        }



    }


}