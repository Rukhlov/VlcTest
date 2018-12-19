using NLog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using VlcContracts;

namespace VlcPlayer
{
    public partial class VideoWindow : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public VideoWindow()
        {
            InitializeComponent();

            //this.DataContext = this;

            //this.Video.SetBinding(EffectProperty, new Binding(nameof(BlurEffect)));
            //this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VideoSource)));
            //this.BlurLabel.SetBinding(ContentProperty, new Binding(nameof(BlurRadius)));
            //this.StatusLabel.SetBinding(ContentProperty, new Binding(nameof(MediaState)));
            //this.StatsLabel.SetBinding(ContentProperty, new Binding(nameof(Stats)));

        }

        private WindowInteropHelper windowHelper = null;
        protected override void OnSourceInitialized(EventArgs e)
        {
            var parentWindow = App.ParentWindowHandle;
            if (parentWindow != IntPtr.Zero)
            {
                windowHelper = new WindowInteropHelper(this);

                //NativeMethods.SetParent(windowHelper.Handle, parentWindow);

                //// Remove border and whatnot
                //NativeMethods.SetWindowLongA(windowHelper.Handle, NativeMethods.GWL_STYLE, NativeMethods.WS_VISIBLE);

                //// Move the window to overlay it on this window
                //NativeMethods.MoveWindow(windowHelper.Handle, 0, 0, (int)this.ActualWidth, (int)this.ActualHeight, true);

                windowHelper.Owner = parentWindow;

                //this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                NativeMethods.HideMinimizeAndMaximizeButtons(windowHelper.Handle);

                this.ShowInTaskbar = true;
                this.WindowStyle = WindowStyle.ToolWindow;

            }

            base.OnSourceInitialized(e);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

        }

        protected override void OnClosing(CancelEventArgs e)
        {
            //closing = true;
            //syncEvent.Set();

            base.OnClosing(e);
        }

        private PlaybackHost playback = null;
        internal PlaybackHost Playback
        {
            get
            {
                if(playback== null)
                {
                    playback = this.DataContext as PlaybackHost;
                }
                return playback;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Playback?.Close();

            //Environment.Exit(0);

        }

        private void Video_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Playback?.IncrBlurRadius(e.Delta);
        }

        private void Window_PreviewKeyDown(object o, KeyEventArgs a)
        {
            if (a.Key == Key.P)
            {
                //currentMri = @"http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_480p_surround-fix.avi";
                //currentMri = @"rtsp://192.168.10.187:554/ONVIF/MediaInput?profile=";
                //var mri = @"https://www.youtube.com/watch?v=moFOUPnWEvo";

                //var mri = App.CurrentDirectory + @"\Test\AV_60Sec_30Fps.mkv";
                //(this.DataContext as PlaybackHost)?.Play(mri);

            }
            //else if (a.Key == Key.S)
            //{
            //    (this.DataContext as PlaybackHost)?.Stop();
            //}
            //else if (a.Key == Key.Q)
            //{
            //    (this.DataContext as PlaybackHost)?.Close();
            //}
        }

        internal void SwitchVisibilityState()
        {
            if (this.Visibility == Visibility.Hidden || this.Visibility == Visibility.Collapsed)
            {
                this.Visibility = Visibility.Visible;
            }
            else
            {
                this.Visibility = Visibility.Hidden;
            }
        }

    }


}
