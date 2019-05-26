using NLog;
using System;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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

            var parentWindowHandle = Session.ParentWindowHandle;

            if (parentWindowHandle != IntPtr.Zero)
            {
                var windowHelper = new WindowInteropHelper(this);

                //NativeMethods.SetParent(windowHelper.Handle, parentWindow);

                //// Remove border and whatnot
                //NativeMethods.SetWindowLongA(windowHelper.Handle, NativeMethods.GWL_STYLE, NativeMethods.WS_VISIBLE);

                //// Move the window to overlay it on this window
                //NativeMethods.MoveWindow(windowHelper.Handle, 0, 0, (int)this.ActualWidth, (int)this.ActualHeight, true);

                windowHelper.Owner = parentWindowHandle;

                //this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                NativeMethods.HideMinimizeAndMaximizeButtons(windowHelper.Handle);

                //this.ShowInTaskbar = true;
                this.WindowStyle = WindowStyle.ToolWindow;

            }
            this.ShowInTaskbar = true;


            base.OnSourceInitialized(e);
        }

        protected override void OnInitialized(EventArgs e)
        {
            logger.Debug("OnInitialized(...)");

          
            //this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(Controller.VideoSource)));

            base.OnInitialized(e);
        }

        private bool _shown;
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_shown)
            {
                return;
            }
            _shown = true;

            var renderer = Controller?.Renderer;
            if (renderer != null)
            {
                renderer.Run(this.Video);
            }
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

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //if (!dragStarted)
            //{
            //    Controller?.PositionCommand.Execute(e.NewValue);
            //}
            
        }

        private bool dragStarted = false;
        private void PositionSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {

            dragStarted = true;
        }

        private void PositionSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            var value = ((Slider)sender).Value;
            Controller?.PositionCommand.Execute(value);

            dragStarted = false;
        }

        internal void UpdatePosition(double pos)
        {
            if (!dragStarted )//|| !mouseDown)
            {
                this.PositonSlider.Value = pos;
            }
        }

        private bool mouseDown = false;
        private void PositonSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseDown = true;
        }

        private void PositonSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {

            ////var value = ((Slider)sender).Value;

            //var slider = (Slider)sender;
            //var pos = e.GetPosition(slider);
            //double d = 1.0 / slider.ActualWidth * pos.X;
            //var p = slider.Maximum * d;
            ////newPosidion = p;

            //Controller?.PositionCommand.Execute(p);

            mouseDown = false;
        }

        private double newPosidion = 0;
        private void PositonSlider_MouseMove(object sender, MouseEventArgs e)
        {
            //if(e.LeftButton == MouseButtonState.Pressed)
            //{
            //    var slider = (Slider)sender;
            //    var pos = e.GetPosition(slider);
            //    double d = 1.0 / slider.ActualWidth * pos.X;
            //    var p = slider.Maximum * d;
            //    newPosidion = p;
            //}
        }
    }




}
