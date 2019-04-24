using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace VlcTest
{

    class VideoSourceProvider : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public VideoSourceProvider()
        {
            this.dispatcher = Dispatcher.CurrentDispatcher;
        }

        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        private string appEventId = "";

        private Task task = null;

        public volatile bool closing = false;
        private volatile bool rendering = false;
        private volatile bool playing = false;

        private EventWaitHandle displayEvent = null;
        private AutoResetEvent startupEvent = null;

        private InteropBitmap interopBitmap = null;
        private ImageSource videoSource = null;
        public ImageSource VideoSource
        {
            get
            {
                return this.videoSource;
            }

            private set
            {
                if (!Object.ReferenceEquals(this.videoSource, value))
                {
                    this.videoSource = value;
                    this.OnPropertyChanged(nameof(VideoSource));
                }
            }
        }

        private BlurEffect blurEffect = new BlurEffect { Radius = 0 };
        public BlurEffect BlurEffect
        {
            get
            {
                return this.blurEffect;
            }

            private set
            {
                if (!ReferenceEquals(this.blurEffect, value))
                {
                    this.blurEffect = value;
                    this.OnPropertyChanged(nameof(BlurEffect));
                }
            }
        }


        private bool isBusy = false;
        public bool IsBusy
        {
            get
            {
                return this.isBusy;
            }

            set
            {
                if (!ReferenceEquals(this.isBusy, value))
                {
                    this.isBusy = value;
                    this.OnPropertyChanged(nameof(isBusy));
                }
            }
        }

        private string banner = "";
        public string Banner
        {
            get
            {
                return this.banner;
            }

            set
            {
                if (!ReferenceEquals(this.banner, value))
                {
                    this.banner = value;
                    this.OnPropertyChanged(nameof(banner));
                }
            }
        }

        public void Setup(string eventId, string memoryId)
        {
            logger.Debug("VideoControl::Setup(...) " + eventId + " " + memoryId);

            if (task != null)
            {
                if (task.Status == TaskStatus.Running)
                {
                    Debug.WriteLine("task.Status == TaskStatus.Running");

                    this.Close();
                    task.Wait();
                }
            }

            closing = false;

            task = Task.Run(() =>
            {
                try
                {
                    logger.Debug("Render task BEGIN");

                    appEventId = eventId;
                    displayEvent = EventWaitHandle.OpenExisting(eventId);
                    startupEvent = new AutoResetEvent(false);

                    logger.Debug("Render control loop started...");
                    while (!closing)
                    {
                        logger.Debug("startupEvent.WaitOne()");

                        if (!startupEvent.WaitOne(3000))
                        {
                            if (!playing)
                            {
                                continue;
                            }
                        }

                        if (closing)
                        {
                            logger.Debug("closing == false");
                            break;
                        }


                        if (!SetupVideoSource(memoryId))
                        {
                            logger.Debug("SetupVideoSource() == false continue;");
                            Thread.Sleep(100);
                            continue;
                        }

                        rendering = true;

                        logger.Debug("Render loop started...");
                        while (rendering && !closing)
                        {
                            if (displayEvent.WaitOne(1000))
                            {
                                this.dispatcher.BeginInvoke(DispatcherPriority.Render,
                                    (Action)(() =>
                                    {
                                        interopBitmap?.Invalidate();
                                    }));
                            }
                        }

                        playing = false;

                        logger.Debug("Render loop stopped...");
                    }

                    logger.Debug("Render control loop stopped...");

                }
                catch (Exception ex)
                {
                    Debug.Fail(ex.Message);
                    logger.Error(ex);

                }
                finally
                {

                    logger.Debug("Render task END");

                    startupEvent?.Dispose();
                    startupEvent = null;

                    displayEvent?.Dispose();
                    displayEvent = null;

                    VideoSource = null;

                }
            });


        }


        private bool SetupVideoSource(string memoryId)
        {
            logger.Debug("SetupVideoSource(...) " + memoryId);

            bool res = false;
            MemoryMappedFile mmf = null;
            try
            {
                mmf = MemoryMappedFile.OpenExisting(memoryId);

                var args = new int[4];
                int headerSize = 1024;//args.Length * sizeof(int);

                int offset = 0;

                using (var header = mmf.CreateViewAccessor(offset, headerSize))
                {
                    header.ReadArray<int>(offset, args, 0, args.Length);

                    var result = string.Join(";", args);
                    Debug.WriteLine(result);

                }

                offset += headerSize;

                var width = args[0];
                var height = args[1];
                var isAlphaChannelEnabled = args[2];
                var pitch = args[3];

                var pixFmt = (isAlphaChannelEnabled == 1) ? PixelFormats.Bgra32 : PixelFormats.Bgr32;

                if (width > 0 && height > 0 && pitch > 0)
                {
                    this.dispatcher.Invoke(() =>
                    {
                        using (var handle = mmf.SafeMemoryMappedFileHandle)
                        {
                            var section = handle.DangerousGetHandle();
                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(section, width, height, pixFmt, pitch, offset);

                            this.VideoSource = interopBitmap;
                            res = true;
                        }
                    });
                }
            }
            finally
            {
                mmf?.Dispose();
            }

            return res;
        }


        public void StartDisplay()
        {
            logger.Debug("StartDisplay()");
            //Banner = "";
            //SetWait(true);
            playing = true;
            startupEvent?.Set();

        }

        public void StopDisplay()
        {
            logger.Debug("StopDisplay()");
            playing = false;

            rendering = false;
            displayEvent?.Set();


        }

        public void ClearDisplay()
        {
            logger.Debug("ClearDisplay() " + appEventId);
            //ErrorMessage = "";

            IsBusy = false;

            this.VideoSource = null;
        }

        public void Close()
        {

            logger.Debug("Close() " + appEventId);

            rendering = false;
            closing = true;
            playing = false;

            VideoSource = null;
            displayEvent?.Set();

            startupEvent?.Set();

        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
