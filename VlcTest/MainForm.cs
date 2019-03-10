using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using VlcContracts;

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

            checkBoxLoopPlayback.Checked = playbackOptions.LoopPlayback;
            checkBoxMute.Checked = playbackOptions.IsMute;
            trackBarVolume.Value = playbackOptions.Volume;
            trackBarBlur.Value = playbackOptions.BlurRadius;
            checkBoxVideoAdjustments.Checked = playbackOptions.VideoAdjustmentsEnabled;

            labelVolume.Text = playbackOptions.Volume.ToString();

            CreateVideoForm(false);



            speedComboBox.Items.Add(new ComboboxItem { Text = "x0.125", Value = 0.125f });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x0.25" ,Value = 0.25f });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x0.5" ,Value = 0.5f });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x1" ,Value = 1 });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x2" ,Value = 2 });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x4" ,Value = 4 });
            speedComboBox.Items.Add(new ComboboxItem{Text = "x8" ,Value = 8 });
            speedComboBox.SelectedIndex = 3;

            //videoControl = new VideoControl();

            //videoForm.InitVideoControl(videoControl);

        }
        public class ComboboxItem
        {
            public string Text { get; set; }
            public float Value { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private VideoForm videoForm = null;

        private Timer timer = new Timer();
        internal VideoControl videoControl = null;

        private PlaybackSession playbackSession = null;
        private static PlaybackService _playbackService = null;

        private PlaybackOptions playbackOptions = new PlaybackOptions
        {
            IsMute = false,
            Volume = 80,
            BlurRadius=0,
            LoopPlayback = false,
            VideoAdjustmentsEnabled = false,
            VideoContrast = 100,
        };

        internal PlaybackService playbackService
        {
            get
            {
                if(_playbackService == null)
                {
                    _playbackService = new PlaybackService(playbackOptions);
  
                    _playbackService.StateChanged += playbackService_StateChanged;
                    _playbackService.Opened += playbackService_Opened;
                    _playbackService.ReadyToPlay += playbackService_ReadyToPlay;
                    _playbackService.Closed += playbackService_Closed;

                    _playbackService.PlaybackStartDisplay += playbackService_PlaybackStartDisplay;
                    _playbackService.PlaybackStopDisplay += playbackService_PlaybackStopDisplay;

                    _playbackService.PlaybackPositionChanged += playbackService_PlaybackPositionChanged;
                    _playbackService.PlaybackLengthChanged += playbackService_PlaybackLengthChanged;
                }

                return _playbackService;
            }
        }



        private void playbackService_PlaybackPositionChanged(float position)
        {
           // if (mediaPosition != position)
            {
                mediaPosition = position;
                long currTime = (long)(totalMediaLength * mediaPosition);
                TimeSpan ts = TimeSpan.FromMilliseconds(currTime);

                Invoke(new Action(() =>
                {

                    if (!mouseDown)
                    {
                        labelCurrentTime.Text = ts.ToString("hh\\:mm\\:ss");

                        var pos = (int)(mediaPosition * trackBarPosition.Maximum);
                        if(pos > trackBarPosition.Maximum)
                        {
                            pos = trackBarPosition.Maximum;
                        }
                        else if(pos< trackBarPosition.Minimum)
                        {
                            pos = trackBarPosition.Minimum;
                        }

                        if (trackBarPosition.Value != pos)
                        {
                            trackBarPosition.Value = pos;
                        }
                    }
                }));
            }
        }

        private void playbackService_PlaybackLengthChanged(long len)
        {
         //   if (totalMediaLength != len)
            {
                totalMediaLength = len;
                TimeSpan ts = TimeSpan.FromMilliseconds(totalMediaLength);

                Invoke(new Action(() =>
                {
                    labelTotalTime.Text = ts.ToString("hh\\:mm\\:ss");

                }));
            }
        }

        private void playbackService_PlaybackStartDisplay()
        {
            logger.Debug("playbackService_PlaybackStartDisplay(...)");

            //videoControl.SetWait(true);
            videoControl.StartDisplay();
        }

        private void playbackService_PlaybackStopDisplay()
        {
            logger.Debug("playbackService_PlaybackStopDisplay(...)");

            videoControl.StopDisplay();
        }

        private void playbackService_Opened(object arg1, object[] arg2)
        {

        }

        private void playbackService_ReadyToPlay(object arg1, object[] arg2)
        {
            var eventId = playbackSession.EventSyncId;
            var memoryId = playbackSession.MemoryBufferId;

            CreateVideoControl(eventId, memoryId);


        }

        private void playbackService_StateChanged(ServiceState newState, ServiceState oldState)
        {
            if(newState == ServiceState.Opened)
            {
                if (playbackSession != null)
                {
                    playbackSession.StateChanged -= PlaybackSession_StateChanged;
                }

                playbackSession = playbackService.Session;
               
                playbackSession.StateChanged += PlaybackSession_StateChanged;
            }
            else if(newState == ServiceState.Closed)
            {
                //...
            }
        }

        private void PlaybackSession_StateChanged(PlaybackState state, PlaybackState old)
        {
            if(state == PlaybackState.Opening)
            {
                logger.Debug("videoControl.SetWait(true)");
                videoControl.IsBusy = true;
            }
            else if( state == PlaybackState.Playing)
            {

                UpdateUi();
                logger.Debug("videoControl.SetWait(false)");
                videoControl.IsBusy = false;
                // videoControl.SetWait(false);
            }
            else if (state == PlaybackState.Paused)
            {
                UpdateUi();
            }
            else if (state == PlaybackState.Stopped)
            {
                logger.Debug("PlaybackSession_StateChanged(...) " + state);
                videoControl.ClearDisplay();
                UpdateUi();
            }
            else
            {

            }

        }

        private void playbackService_Closed(object arg1, object[] arg2)
        {
            logger.Debug("playbackService_Closed(...)");

            videoControl.ClearDisplay();
            UpdateUi();
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
                var session = playbackService?.Session;
                bool inPlayingMode = false;
                if (session != null)
                {
                    inPlayingMode = (playbackSession.State == PlaybackState.Playing
                        || playbackSession.State == PlaybackState.Paused);
                }

                trackBarPosition.Enabled = inPlayingMode;
                if (!inPlayingMode)
                {
                    labelCurrentTime.Text = "--:--";
                    labelTotalTime.Text = "--:--";
                    trackBarPosition.Value = 0;
                }

                buttonPlay.Text = (session?.State == PlaybackState.Playing) ? "Pause" : "Play";

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

                // videoControl = new VideoControl();

                videoControl.IsBusy = true;

                //videoControl.SetWait(true);

                videoControl.Setup(appId, memoryId);
                videoControl.BlurEffect.Radius = playbackOptions.BlurRadius;

                //videoForm.InitVideoControl(videoControl);

                if (!videoForm.Visible)
                {
                    videoForm.Visible = true;
                }

            }));

        }

        private void CreateVideoForm(bool visible = true)
        {
            if (videoForm == null || videoForm.IsDisposed || videoForm.Disposing)
            {

                videoForm = new VideoForm();


                videoControl = new VideoControl();
                videoControl.IsBusy = true;

                //videoControl.SetWait(true);
                videoForm.InitVideoControl(videoControl);

                //videoForm.InitVideoControl(videoControl);

                videoForm.FormClosing += (o, a) =>
                {
                    a.Cancel = true;
                    videoForm.Visible = false;
                    playbackService.Stop();

                   
                };
                videoForm.Visible = visible;
                //videoForm.FormClosed += (o, a) =>
                //{
                //    PostMessage("Stop");
                //};
            }

            if (!videoForm.Visible)
            {
                videoForm.Visible = visible;
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

            playbackService.RunPlaybackCommand(command, args);

        }



        private void buttonPlay_Click_1(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonPlay_Click_1(...)");

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

            playbackService.Play(uri, forse);

        }


 
        private void buttonPause_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonPause_Click(...)");

            playbackService.Pause();

        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonStop_Click(...)");

            playbackService.Stop();

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
            bool isMute = checkBoxMute.Checked;
            if (playbackOptions.IsMute != isMute)
            {
                playbackOptions.IsMute = isMute;

                playbackService.SetMute(playbackOptions.IsMute);
            }

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

            Debug.WriteLine("trackBarVolume_ValueChanged(...) " + playbackOptions.Volume + " " + vol);

            if (playbackOptions.Volume != vol)
            {
                labelVolume.Text = vol.ToString();
                playbackOptions.Volume = vol;

                playbackService.SetVolume(playbackOptions.Volume);
            }

        }

        private void trackBarBlur_ValueChanged(object sender, EventArgs e)
        {
            var blurRadius = (int)trackBarBlur.Value;

            if (playbackOptions.BlurRadius != blurRadius)
            {
                playbackOptions.BlurRadius = blurRadius;

                var effect = videoControl?.BlurEffect;
                if (effect != null)
                {
                    effect.Radius = (double)playbackOptions.BlurRadius; //(double)blurRadius;

                    label1.Text = playbackOptions.BlurRadius.ToString();
                    SetPlaybackParameter("Blur", new object[] { playbackOptions.BlurRadius });
                }
            }
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
            bool loopPlayback = checkBoxLoopPlayback.Checked;

            if (playbackOptions.LoopPlayback != loopPlayback)
            {
                playbackOptions.LoopPlayback = loopPlayback;

                SetPlaybackParameter("SetLoopPlayback", new object[] { playbackOptions.LoopPlayback });
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (playbackService != null)
            {
                playbackService.Close();
            }
        }


        private void speedComboBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var obj = speedComboBox.SelectedItem;
            if (obj != null)
            {
                var item = obj as ComboboxItem;
                if (item != null)
                {
                    var rate = (float)item.Value;

                    playbackService.SetRate(rate);
                }
            }
        }
    }


}