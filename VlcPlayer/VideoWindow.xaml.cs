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
        }

        public VideoWindow(PlaybackController controller) : this()
        {
            this.DataContext = controller;

            //this.Video.SetBinding(EffectProperty, new Binding(nameof(BlurEffect)));
            //this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VideoSource)));
            //this.BlurLabel.SetBinding(ContentProperty, new Binding(nameof(BlurRadius)));
            //this.StatusLabel.SetBinding(ContentProperty, new Binding(nameof(MediaState)));
            //this.StatsLabel.SetBinding(ContentProperty, new Binding(nameof(Stats)));

        }

        private PlaybackController controller = null;
        internal PlaybackController Controller
        {
            get
            {
                if (controller == null)
                {
                    controller = this.DataContext as PlaybackController;
                }
                return controller;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            var parentWindow = Program.ParentWindowHandle;
            if (parentWindow != IntPtr.Zero)
            {
                var windowHelper = new WindowInteropHelper(this);

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
            logger.Debug("OnInitialized(...)");

            base.OnInitialized(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            logger.Debug("OnClosing(...)");

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            logger.Debug("OnClosed(...)");

            base.OnClosed(e);

            Controller?.QuitCommand.Execute(null);
        }

        private void Video_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Controller?.IncrBlurRadius(e.Delta);
        }
    }


}
