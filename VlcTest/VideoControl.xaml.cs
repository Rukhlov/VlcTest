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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VlcTest
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class VideoControl : UserControl, INotifyPropertyChanged
    {
        public VideoControl()
        {
            InitializeComponent();
            this.DataContext = this;

            this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VideoSource)));
        }

        private EventWaitHandle syncEvent = null;

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
        IntPtr _ptr = IntPtr.Zero;

        public volatile string AppEventId = "";
        public void Start(string appId)
        {

            Task.Run(() =>
            {

                syncEvent = EventWaitHandle.OpenExisting(appId + "_event");
                syncEvent.WaitOne();


                MemoryMappedFile mmf = null;
                try
                {
                    int offset = 0;

                    mmf = MemoryMappedFile.OpenExisting(appId);

                    var args = new int[4];
                    int headerSize = args.Length * sizeof(int);

                    using (var headerView = mmf.CreateViewAccessor(offset, headerSize))
                    {
                        headerView.ReadArray<int>(offset, args, 0, args.Length);

                        var result = string.Join(";", args);
                        System.Diagnostics.Debug.WriteLine(result);

                    }

                    offset += headerSize;

                    var width = args[0];
                    var height = args[1];
                    var IsAlphaChannelEnabled = args[2];
                    var pitch = args[3];

                    var pixFmt = (IsAlphaChannelEnabled == 1) ? PixelFormats.Bgra32 : PixelFormats.Bgr32;

                    this.Dispatcher.Invoke(() =>
                    {
                        using (var mmfHandle = mmf.SafeMemoryMappedFileHandle)
                        {
                            var section = mmfHandle.DangerousGetHandle();
                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(section, width, height, pixFmt, pitch, offset);
                            this.VideoSource = interopBitmap;
                        }

                    });


                    //var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;


                    rendering = true;
                    while (rendering)
                    {
                        if (!syncEvent.WaitOne(5000))
                        {
                            //...
                        }

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                            (Action)(() =>
                            {
                                interopBitmap?.Invalidate();
                            }));


                    }
                }
                finally
                {
                    rendering = false;

                    syncEvent?.Dispose();
                    syncEvent = null;

                    mmf?.Dispose();

                    // VideoSource = null;
                }

            });


        }


        public void _Start(object[] args)
        {
            if (args != null && args.Length == 5)
            {
                var appId = args[0]?.ToString();
                var width = (int)args[1];
                var height = (int)args[2];

                //var IsAlphaChannelEnabled = (bool)args[3];

                var _fmt = (System.Drawing.Imaging.PixelFormat)args[3];

                PixelFormat pixFmt = PixelFormats.Bgr32;
                if (_fmt == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    pixFmt = PixelFormats.Bgra32;
                }

                var pitch = (int)args[4];
                //var eventName = (string)args[5];

                Task.Run(() =>
                {
                    MemoryMappedFile mmf = null;
                    try
                    {
                        mmf = MemoryMappedFile.OpenExisting(appId);

                        var handle = mmf.SafeMemoryMappedFileHandle.DangerousGetHandle();
                        this.Dispatcher.Invoke(() =>
                        {
                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(handle, width, height, pixFmt, pitch, 0);

                            this.VideoSource = interopBitmap;

                        });

                        var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;
                        syncEvent = EventWaitHandle.OpenExisting(appId + "_event");

                        rendering = true;
                        while (rendering)
                        {
                            if (!syncEvent.WaitOne(5000))
                            {
                                //...
                            }

                            this.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                                (Action)(() =>
                                {
                                    interopBitmap?.Invalidate();
                                }));


                        }
                    }
                    finally
                    {
                        rendering = false;

                        syncEvent?.Dispose();
                        syncEvent = null;

                        mmf?.Dispose();

                        // VideoSource = null;
                    }

                });

            }
        }

        private volatile bool rendering = false;
        public void Stop()
        {
            rendering = false;
            syncEvent?.Set();
        }

        public void Clear()
        {
            VideoSource = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }



    }

}
