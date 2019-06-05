using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Windows.Input;

using System.Linq;

using VlcContracts;
using System.Drawing;
using System.Collections.Generic;

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
            speedComboBox.Items.Add(new ComboboxItem { Text = "x0.25", Value = 0.25f });
            speedComboBox.Items.Add(new ComboboxItem { Text = "x0.5", Value = 0.5f });
            speedComboBox.Items.Add(new ComboboxItem { Text = "x1", Value = 1 });
            speedComboBox.Items.Add(new ComboboxItem { Text = "x2", Value = 2 });
            speedComboBox.Items.Add(new ComboboxItem { Text = "x4", Value = 4 });
            speedComboBox.Items.Add(new ComboboxItem { Text = "x8", Value = 8 });
            speedComboBox.SelectedIndex = 3;

            //videoControl = new VideoControl();

            //videoForm.InitVideoControl(videoControl);



            //trackBarPosition.DataBindings.Add(new Binding("Value", playbackService.Session, "Position", true, DataSourceUpdateMode.OnPropertyChanged ));
            //label2.DataBindings.Add(new Binding("Text", playbackService.Session, "Position", false, DataSourceUpdateMode.OnPropertyChanged));

            // label2.DataBindings.Add(new Binding("Text", playbackService.Session, "Position"));

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

        private VideoSourceProvider videoProvider = null;

        private PlaybackSession playbackSession = null;
        private static PlaybackService _playbackService = null;



        private PlaybackOptions playbackOptions = new PlaybackOptions
        {
            IsMute = false,
            Volume = 80,
            BlurRadius = 0,
            LoopPlayback = false,
            VideoAdjustmentsEnabled = false,
            VideoContrast = 100,
        };

        internal PlaybackService playbackService
        {
            get
            {
                if (_playbackService == null)
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


        private void CreateVideoForm(bool visible = true)
        {
            if (videoForm == null || videoForm.IsDisposed || videoForm.Disposing)
            {

                videoForm = new VideoForm();
                videoForm.FormClosing += VideoForm_FormClosing;

                if (videoProvider == null)
                {
                    videoProvider = new VideoSourceProvider();
                }

                videoForm.SetUp(new VideoControl
                {
                    DataContext = videoProvider
                });

                videoForm.Visible = visible;
            }

        }

        private void VideoForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;

            videoForm.Visible = false;
            playbackService?.Stop();
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
                        if (pos > trackBarPosition.Maximum)
                        {
                            pos = trackBarPosition.Maximum;
                        }
                        else if (pos < trackBarPosition.Minimum)
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

            videoProvider.StartDisplay();
        }

        private void playbackService_PlaybackStopDisplay()
        {
            logger.Debug("playbackService_PlaybackStopDisplay(...)");

            videoProvider.StopDisplay();
        }

        private void playbackService_Opened(object arg1, object[] arg2)
        {

        }

        private void playbackService_ReadyToPlay(object arg1, object[] arg2)
        {
            videoProvider.IsBusy = true;

            var eventId = playbackSession.EventSyncId;
            var memoryId = playbackSession.MemoryBufferId;

            videoProvider.Setup(eventId);

            this.Invoke((Action)(() =>
            {
                videoProvider.BlurEffect.Radius = playbackOptions.BlurRadius;
                if (!videoForm.Visible)
                {
                    videoForm.Visible = true;
                }
            }));

        }

        private void playbackService_StateChanged(ServiceState newState, ServiceState oldState)
        {
            if (newState == ServiceState.Opened)
            {
                if (playbackSession != null)
                {
                    playbackSession.StateChanged -= PlaybackSession_StateChanged;
                }

                playbackSession = playbackService.Session;

                playbackSession.StateChanged += PlaybackSession_StateChanged;
            }
            else if (newState == ServiceState.Closed)
            {
                //...
            }
        }

        private void PlaybackSession_StateChanged(PlaybackState state, PlaybackState old)
        {
            if (state == PlaybackState.Opening)
            {
                logger.Debug("PlaybackSession_StateChanged(...) " + state);

                videoProvider.Banner = "";
                videoProvider.IsBusy = true;
            }
            else if (state == PlaybackState.Playing)
            {
                logger.Debug("PlaybackSession_StateChanged(...) " + state);
                UpdateUi();

                videoProvider.IsBusy = false;
                videoProvider.Banner = "";

            }
            else if (state == PlaybackState.Paused)
            {
                logger.Debug("PlaybackSession_StateChanged(...) " + state);
                videoProvider.IsBusy = false;

                UpdateUi();
            }
            else if (state == PlaybackState.Stopped)
            {
                logger.Debug("PlaybackSession_StateChanged(...) " + state);
                videoProvider.IsBusy = true;
                videoProvider.ClearDisplay();

                UpdateUi();
            }
            else
            {

            }

        }

        private void playbackService_Closed(object arg1, object[] arg2)
        {
            logger.Debug("playbackService_Closed(...)");
            var exitCode = 0;
            if (playbackService != null)
            {
                exitCode = playbackService.StatusCode;
                if (exitCode != 0)
                {
                    videoProvider.Banner = "ErrorCode: " + exitCode;

                }
                else
                {
                    videoProvider.Banner = "";
                }
            }

            videoProvider.ClearDisplay();
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
                    inPlayingMode = (session.State == PlaybackState.Playing
                        || session.State == PlaybackState.Paused);
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



        // private static Process CurrentProccess = Process.GetCurrentProcess();

        private void buttonStart_Click(object sender, EventArgs e)
        {

            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonStart_Click(...)");

            ////string fileName = currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";
            //string fileName = "";//currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";

            string fileName = this.textBox2.Text;

            PlayCommand.Execute(fileName);
        }


        private void buttonDisconnect_Click(object sender, EventArgs e)
        {

            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonDisconnect_Click(...)");

            playbackService.Close();

            // videoProvider.Close();

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

            PlayCommand.Execute(currentMediaFile);

            // PlayFile(currentMediaFile);
        }

        private void PlayFile(params object[] args)
        {
            string uri = "";
            bool force = false;

            if (args != null)
            {

            }
        }

        private void PlayFile(string uri, bool forse = false)
        {
            Debug.WriteLine("PlayFile(...)");

            videoProvider.IsBusy = true;

            if (!videoForm.Visible)
            {
                videoForm.Visible = true;
            }

            playbackService.Play(uri, forse);

        }



        private void buttonPause_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonPause_Click(...)");

            PauseCommand.Execute(null);
            //playbackService.Pause();

        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>> buttonStop_Click(...)");

            StopCommand.Execute(null);

        }

        private string currentMediaFile = "";
        private void buttonOpenFile_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(">>>>>>>>>> buttonOpenFile_Click(...) " + currentMediaFile);

            OpenFileDialog dlg = new OpenFileDialog();

            var result = dlg.ShowDialog();

            if (result == DialogResult.OK)
            {
                currentMediaFile = dlg.FileName;

                textBox2.Text = currentMediaFile;

                if (File.Exists(currentMediaFile))
                {
                    PlayCommand.Execute(new object[] { currentMediaFile, true });

                    //PlayFile(currentMediaFile, true);
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

                var effect = videoProvider?.BlurEffect;
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

        private System.Windows.Input.ICommand playCommand = null;
        public System.Windows.Input.ICommand PlayCommand
        {
            get
            {
                if (playCommand == null)
                {
                    playCommand = new PlaybackCommand(o =>
                    {
                        string uri = "";
                        bool force = false;

                        if (o is string)
                        {
                            uri = o.ToString();
                        }
                        else
                        {
                            if (o != null)
                            {
                                var args = o as object[];
                                if (args != null && args.Length > 1)
                                {
                                    uri = args[0].ToString();
                                    force = (bool)args[1];
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(uri))
                        {
                            PlayFile(uri, force);
                        }

                    });

                }
                return playCommand;
            }
        }


        private System.Windows.Input.ICommand stopCommand = null;
        public System.Windows.Input.ICommand StopCommand
        {
            get
            {
                if (stopCommand == null)
                {
                    stopCommand = new PlaybackCommand(o =>
                    {
                        if (playbackService != null)
                        {
                            playbackService.Stop();
                        }
                    });
                }
                return stopCommand;
            }
        }

        private System.Windows.Input.ICommand pauseCommand = null;
        public System.Windows.Input.ICommand PauseCommand
        {
            get
            {
                if (pauseCommand == null)
                {
                    pauseCommand = new PlaybackCommand(o =>
                    {
                        if (playbackService != null)
                        {
                            playbackService.Pause();
                        }

                    });

                }
                return pauseCommand;
            }
        }


        class TransparentForm : Form
        {
            public TransparentForm() { }


            public enum GWL
            {
                ExStyle = -20
            }

            public enum WS_EX
            {
                Transparent = 0x20,
                Layered = 0x80000
            }

            public enum LWA
            {
                ColorKey = 0x1,
                Alpha = 0x2
            }

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
            public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
            public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
            public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);

            //protected override void OnShown(EventArgs e)
            //{
            //    base.OnShown(e);

            //    int wl = GetWindowLong(this.Handle, GWL.ExStyle);
            //    wl = wl | 0x80000 | 0x20;
            //    SetWindowLong(this.Handle, GWL.ExStyle, wl);
            //    //SetLayeredWindowAttributes(this.Handle, 0, 128, LWA.Alpha);
            //}


            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x80000 /* WS_EX_LAYERED */ | 0x20 /* WS_EX_TRANSPARENT */ | 0x80/* WS_EX_TOOLWINDOW */;
                    return cp;
                }
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                const int HTTRANSPARENT = -1;

                if (m.Msg == (int)WM_NCHITTEST)
                {
                    m.Result = (IntPtr)HTTRANSPARENT;
                }
                else
                {
                    base.WndProc(ref m);
                }
            }

        }

        class ScreenItem
        {
            public string Caption { get; set; }
            public Screen Screen { get; set; }

        }


        private TransparentForm f = null;
        private void button7_Click(object sender, EventArgs e)
        {

            var allscreens = Screen.AllScreens;

            List<ScreenItem> items = new List<ScreenItem>();
            if (allscreens != null)
            {
                items.Add(new ScreenItem { Caption = "None" });
                foreach (var s in allscreens)
                {
                    items.Add(new ScreenItem { Caption = s.DeviceName, Screen = s });
                }
            }

            comboBox1.DisplayMember = "Caption";
            comboBox1.DataSource = items;

            //FadeScreen();

        }

        private void FadeScreen(Screen screen)
        {
            if (screen == null)
            {
                if(f != null)
                {
                    f.Visible = false;         
                }

                return;
            }


            //Screen screen = null;
            //var screens = Screen.AllScreens;
            //if (screens.Length > 1)
            //{
            //    screen = screens.FirstOrDefault(s => !s.Primary);
            //}


            Rectangle bounds = screen?.WorkingArea ?? new Rectangle(10, 10, 100, 100);

            if (f == null)
            {
                f = new TransparentForm();
                f.Opacity = 0.5;
                f.BackColor = System.Drawing.Color.Black;
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                //f.Bounds = bounds;
                //f.Owner = this;
                f.ShowInTaskbar = false;
                f.TopMost = true;
            }

            if (f != null)
            {
                f.Bounds = bounds;
                var opacity = f.Opacity * (trackBar1.Maximum - trackBar1.Minimum);

                trackBar1.Value = (int)opacity;

                f.Visible = true;//!f.Visible;
            }
        }

        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            var obj = comboBox1.SelectedItem;

            if (obj != null)
            {
                ScreenItem screenitem = obj as ScreenItem;

                var screen = screenitem?.Screen;
                FadeScreen(screen);


            }

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if (f != null)
            {

                var opacity = (double)(trackBar1.Value) / (double)(trackBar1.Maximum - trackBar1.Minimum);

                if (opacity > 0.99)
                {
                    opacity = 0.99;//0.99;

                }
                else if (opacity < 0)
                {
                    opacity = 0;
                }

                label3.Text = opacity.ToString();
                f.Opacity = opacity;

            }
        }





        private void MainForm_Load(object sender, EventArgs e)
        {

        }


        AudioMixerManager audioVolumeManager = null;
        private void button8_Click(object sender, EventArgs e)
        {
            if (audioVolumeManager != null)
            {
                
            }

            audioVolumeManager = new AudioMixerManager();

            if (audioVolumeManager != null)
            {
                audioVolumeManager.Update();

                var items = audioVolumeManager.Items;

                if (items.Count > 0)
                {
                    audioSessionsComboBox.DataSource = items;
                    audioSessionsComboBox.DisplayMember = "Name";
                    audioSessionsComboBox.Refresh();
                }

            }


        }
        private AudioMixerItem mixerItem = null;

        private void audioSessionsComboBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var selectedValue = audioSessionsComboBox.SelectedValue;
            if (selectedValue != null)
            {
                if (mixerItem != null)
                {
                    mixerItem.VolumeChanged -= mixerItem_VolumeChanged;
                    mixerItem.MuteChanged -= mixerItem_MuteChanged;


                }
                mixerItem = selectedValue as AudioMixerItem;

                if (mixerItem != null)
                {
                   // mixerItem.Setup();
                    mixerItem.VolumeChanged += mixerItem_VolumeChanged;
                    mixerItem.MuteChanged += mixerItem_MuteChanged;

                    trackBar2.Value = mixerItem.Volume;
                    checkBoxMute2.Checked = mixerItem.Mute;
                }
            }

        }


        private void mixerResetButton_Click(object sender, EventArgs e)
        {

            if (audioVolumeManager != null)
            {
                audioVolumeManager.Reset();
            }
        }


        private void mixerItem_VolumeChanged(int volume)
        {

            //var vol = (trackBar2.Maximum - trackBar2.Minimum) * e.Volume;

            if (!trackBar2MouseDown)
            {
                trackBar2.Invoke(new Action(
                    () => 
                    {
                        trackBar2.Value = volume;
                    }));
            }

        }

        private void mixerItem_MuteChanged(bool mute)
        {

            checkBoxMute2.Invoke(new Action(
                    () =>
                    {
                        checkBoxMute2.Checked = mute;
                    }));

        }



        private void checkBoxMute2_CheckedChanged(object sender, EventArgs e)
        {
            var controller = mixerItem;
            if (controller != null)
            {
                controller.Mute = checkBoxMute2.Checked;
            }

            //audioSessionController?.Mute = checkBoxMute2.Checked;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
           var volume = trackBar2.Value / 100.0f;
            var controller = mixerItem;
            if (controller != null)
            {
                controller.Volume = trackBar2.Value;
            }

        }
        private bool trackBar2MouseDown = false;
        private void trackBar2_MouseDown(object sender, MouseEventArgs e)
        {
            trackBar2MouseDown = true;
        }

        private void trackBar2_MouseUp(object sender, MouseEventArgs e)
        {

            trackBar2MouseDown = false;

        }




        private void audioSessionsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }


    }


    public class MuteEventArgs : EventArgs
    {
        public MuteEventArgs(bool muted)
        {
            Muted = muted;
        }

        public bool Muted { get; private set; }
    }

    public class VolumeEventArgs : EventArgs
    {
        public VolumeEventArgs(int volume)
        {
            Volume = volume;
        }

        public int Volume { get; private set; }
    }



    class PlaybackController
    {
        private VideoSourceProvider videoProvider = null;

        private PlaybackSession playbackSession = null;
        public PlaybackSession Session
        {
            get
            {
                return Service.Session;
            }
        }

        public PlaybackService Service
        {
            get
            {
                if (playbackService == null)
                {
                    //playbackService = new PlaybackService(playbackOptions);

                    //playbackService.StateChanged += playbackService_StateChanged;
                    //playbackService.Opened += playbackService_Opened;
                    //playbackService.ReadyToPlay += playbackService_ReadyToPlay;
                    //playbackService.Closed += playbackService_Closed;

                    //playbackService.PlaybackStartDisplay += playbackService_PlaybackStartDisplay;
                    //playbackService.PlaybackStopDisplay += playbackService_PlaybackStopDisplay;

                    //playbackService.PlaybackPositionChanged += playbackService_PlaybackPositionChanged;
                    //playbackService.PlaybackLengthChanged += playbackService_PlaybackLengthChanged;
                }

                return playbackService;
            }
        }

        private PlaybackService playbackService = null;



    }

    class PlaybackCommand : System.Windows.Input.ICommand
    {

        public PlaybackCommand(Action<object> execute)
            : this(execute, null) { }

        public PlaybackCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }


        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute != null ? _canExecute(parameter) : true;
        }

        public void Execute(object parameter)
        {
            if (_execute != null)
                _execute(parameter);
        }

        public void OnCanExecuteChanged()
        {
            CanExecuteChanged(this, EventArgs.Empty);
        }

        private readonly Action<object> _execute = null;
        private readonly Predicate<object> _canExecute = null;
    }


}