using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VlcTest
{
    public partial class VideoControl : UserControl, INotifyPropertyChanged
    {
        public VideoControl()
        {
            InitializeComponent();
            this.DataContext = this;

           // this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VideoSource)));
        }

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

            set
            {
                if (!Object.ReferenceEquals(this.blurEffect, value))
                {
                    this.blurEffect = value;
                    this.OnPropertyChanged(nameof(BlurEffect));
                }
            }
        }

        public void Setup(string eventId, string memoryId)
        {
            Debug.WriteLine("VideoControl::Setup(...) " + eventId + " " + memoryId);

            if(task != null)
            {
                if(task.Status == TaskStatus.Running)
                {
                    Debug.WriteLine("task.Status == TaskStatus.Running");

                    this.Close();
                }
            }

            closing = false;

            task = Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine("Render task BEGIN");

                    appEventId = eventId;
                    displayEvent = EventWaitHandle.OpenExisting(eventId);
                    startupEvent = new AutoResetEvent(false);

                    Debug.WriteLine("Render control loop started...");
                    while (!closing)
                    {
                        Debug.WriteLine("startupEvent.WaitOne()");

                        if (!startupEvent.WaitOne(3000))
                        {
                            if (!playing)
                            {
                                continue;
                            }
                        }

                        if (closing)
                        {
                            Debug.WriteLine("closing == false");
                            break;
                        }


                        if (!SetupVideoSource(memoryId))
                        {
                            Debug.WriteLine("SetupVideoSource() == false continue;");
                            Thread.Sleep(100);
                            continue;
                        }

                        rendering = true;

                        Debug.WriteLine("Render loop started...");
                        while (rendering && !closing)
                        {
                            if (displayEvent.WaitOne(1000))
                            {
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                                    (Action)(() =>
                                    {
                                        interopBitmap?.Invalidate();
                                    }));
                            }
                        }

                        playing = false;

                        Debug.WriteLine("Render loop stopped...");
                    }

                    Debug.WriteLine("Render control loop stopped...");

                }
                catch (Exception ex)
                {
                    Debug.Fail(ex.Message);
                    Debug.WriteLine(ex);

                }
                finally
                {

                    Debug.WriteLine("Render task END");

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
            Debug.WriteLine("VideoControl::SetupVideoSource(...) " + memoryId);

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
                    this.Dispatcher.Invoke(() =>
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
            Debug.WriteLine("VideoControl::Play()");
            playing = true;
            startupEvent?.Set();
        }

        public void StopDisplay()
        {
            Debug.WriteLine("VideoControl::Stop()");
            playing = false;

            rendering = false;
            displayEvent?.Set();
        }

        public void Clear()
        {
            Debug.WriteLine("VideoControl::Clear() " + appEventId);

            this.VideoSource = null;
        }

        public void Close()
        {

            Debug.WriteLine("VideoControl::Close() " + appEventId);
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
