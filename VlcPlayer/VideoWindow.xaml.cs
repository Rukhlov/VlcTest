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


        private VideoSourceProvider videoSourceProvider = null;

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

                this.ShowInTaskbar = true;
                this.WindowStyle = WindowStyle.ToolWindow;

            }
            

            videoSourceProvider = new VideoSourceProvider(this.Video);
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

            var videoBuffer = Controller?.VideoBuffer;
            if (videoBuffer != null)
            {

                videoSourceProvider.Run(videoBuffer);
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

            videoSourceProvider?.Close();
        }

        private void Video_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Controller?.IncrBlurRadius(e.Delta);
        }

    }


    class VideoSourceProvider
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Dispatcher dispatcher = null;
        private readonly Image image = null;

        public VideoSourceProvider(Image image)
        {
            this.image = image;
            this.dispatcher = image.Dispatcher;
        }

        private InteropBitmap interopBitmap = null;

        private SharedBuffer videoBuffer = null;

        public void Run(SharedBuffer buffer)
        {

            if(buffer == null)
            {
                return;
            }

            this.videoBuffer = buffer;

            logger.Debug("Run(...) " + videoBuffer.Name);

            closing = false;

            Task task = Task.Run(() =>
            {
                logger.Debug("Render task started...");

                //sharedBuf = new SharedBuffer(ExchangeId.ToString("N"));

                try
                {

                    buffer.WaitForSignal();

                    bool started = false;

                    VideoBufferInfo vi = new VideoBufferInfo();

                    while (!closing)
                    {

                        vi = buffer.ReadData<VideoBufferInfo>();

                        if (vi.State ==  VideoBufferState.Display)
                        {
                            if (!started)
                            {

                                try
                                {
                                    int width = vi.Width;
                                    int height = vi.Height;
                                    var fmt = vi.PixelFormat;
                                    var pitches = (int)vi.Pitches;
                                    var offset = vi.DataOffset;

                                    dispatcher.Invoke(() =>
                                    {
                                        logger.Debug("Video params: " + vi.ToString());

                                        if (width > 0 && height > 0 && pitches > 0) // TODO: validate...
                                        {
        
                                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(buffer.Section, width, height, fmt, pitches, offset);

                                            image.Source = interopBitmap;

                                            started = true;

                                        }
                                    });
                                }
                                catch (Exception ex)
                                {

                                    logger.Error(ex);
                                    started = false;
                                }


                                if (!started)
                                {
                                    //...
                                    //syncEvent.WaitOne(1000);

                                }
                            }


                            dispatcher.BeginInvoke(DispatcherPriority.Render,
                              (Action)(() =>
                              {
                                  interopBitmap?.Invalidate();

                              }));


                        }
                        else
                        {
                            started = false;

                            dispatcher.Invoke(() =>
                            {
                                image.Source = null;
                            });
                        }

                        buffer.WaitForSignal(1000);
                    }

                    //buffer.Dispose();

                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }

                logger.Debug("Render task ended...");

            });

        }


        /*
        public void _Run(Guid ExchangeId)
        {
            closing = false;

            logger.Debug("Run(...) " + ExchangeId);

            Task task = Task.Run(() =>
            {
                logger.Debug("Render task started...");

                string mutexId = ExchangeId.ToString("N") + "-vid-mutex";
                Mutex mutex = null;
                if(!Mutex.TryOpenExisting(mutexId, out mutex))
                {
                    bool created = false;
                    mutex = new Mutex(false, mutexId, out created);
                }

                string eventId = ExchangeId.ToString("N") + "-event";

                bool result  = EventWaitHandle.TryOpenExisting(eventId, out syncEvent);
                if (!result)
                {
                    try
                    {
                        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                        var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;
                        var rule = new EventWaitHandleAccessRule(users, rights, AccessControlType.Allow);

                        var security = new EventWaitHandleSecurity();
                        security.AddAccessRule(rule);
                        bool created;

                        syncEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventId, out created, security);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                        //...
                    }
                }

                try
                {
                    syncEvent.WaitOne();

                    string memoryId = ExchangeId.ToString("N") + "-vid";

                    MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(memoryId);

                    MemoryMappedViewAccessor mmv = mmf.CreateViewAccessor();

                    bool started = false;

                    int position = 0;
                    VideoBufferInfo vi = new VideoBufferInfo();    
                    
                    while (!closing)
                    {
   
                        byte signal = mmv.ReadByte(position);

                        if (signal == 1)
                        {
                            if (!started)
                            {
                                mutex.WaitOne();
                                mmv.Read(position, out vi);
                                mutex.ReleaseMutex();

                                try
                                {
                                    int width = vi.Width;
                                    int height = vi.Height;
                                    var fmt = vi.PixelFormat;
                                    var pitches = (int)vi.Pitches;
                                    var offset = 1024;

                                    dispatcher.Invoke(() =>
                                    {
                                        logger.Debug("Video params: " + string.Join(" ", width, height, fmt, pitches, offset));

                                        if (width > 0 && height > 0 && pitches > 0) // TODO: validate...
                                        {

                                            var safeHanlde = mmf.SafeMemoryMappedFileHandle;

                                            IntPtr section = safeHanlde.DangerousGetHandle();
                                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(section, width, height, fmt, pitches, offset);

                                            image.Source = interopBitmap;

                                            started = true;

                                        }
                                    });
                                }
                                catch (Exception ex)
                                {

                                    logger.Error(ex);
                                    started = false;
                                }

      
                                if(!started)
                                {
                                    //...
                                    //syncEvent.WaitOne(1000);

                                }
                            }


                            dispatcher.BeginInvoke(DispatcherPriority.Render,
                              (Action)(() =>
                              {
                                  interopBitmap?.Invalidate();

                              }));


                        }
                        else
                        {
                            started = false;
                        }

                        syncEvent.WaitOne(1000);
                    }


                    mmf?.Dispose();
                    mmv?.Dispose();

                    syncEvent?.Dispose();

                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }

                logger.Debug("Render task ended...");

            });

        }*/



        private bool closing = false;
        public void Close()
        {

            logger.Debug("Close()");

            closing = true;

            videoBuffer?.Pulse();

            //syncEvent?.Set();
        }




        //public void _UpdateVideoSource(IntPtr buf, int length, int stride)
        //{
        //    //CopyMemory(_videoSource.BackBuffer, buf, (uint)(_videoSource.PixelWidth * _videoSource.PixelHeight * 4));

        //    this.dispatcher.BeginInvoke(DispatcherPriority.Render,
        //      (Action)(() =>
        //      {
        //          //interopBitmap?.Invalidate();
        //          var rect = new Int32Rect(0, 0, _videoSource.PixelWidth, _videoSource.PixelHeight);

        //          _videoSource.Lock();
        //          //_VideoSource.BackBuffer = buf;
        //          //CopyMemory(_videoSource.BackBuffer, buf, (uint)(_videoSource.PixelWidth * _videoSource.PixelHeight * 3));

        //           _VideoSource.WritePixels(rect, buf, length, stride);
        //          _videoSource.AddDirtyRect(rect);
        //          _videoSource.Unlock();
        //      }));
        //}

    }


}
