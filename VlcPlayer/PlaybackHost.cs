using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace VlcPlayer
{
    public class PlaybackHost : INotifyPropertyChanged
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly Dispatcher dispatcher = null;

        public PlaybackHost()
        {
            this.dispatcher = Dispatcher.CurrentDispatcher;

            this.vlcPlayback = new VlcPlayback();
        }
        
        public PlaybackHost(CommandLineOptions options) : this()
        {
            if (options != null)
            {
                vlcPlayback.MediaAddr = options.FileName;
                this.RemoteAddr = options.ServerAddr;
                this.ParentId = options.ParentId;
                this.WindowHandle = options.WindowHandle;
            }
        }

        private VlcPlayback vlcPlayback = null;
        private CommunicationClient communicationClient = null;

        public VideoSourceProvider VideoSourceProvider { get; private set; }


        private ICommand playCommand = null;
        public ICommand PlayCommand
        {
            get
            {
                if (playCommand == null)
                {

                    playCommand = new PlaybackCommand(obj =>
                    {
                        var fileName = "";
                        if (obj != null)
                        {
                            object[] args = obj as object[];
                            if (args != null && args.Length>0)
                            {
                                fileName = args[0]?.ToString();
                            }
                            
                        }
                        vlcPlayback.Play(fileName);
                        //EnqueueCommand("Play", new[] { this.MediaAddr });

                    });

                }
                return playCommand;
            }
        }

        private ICommand stopCommand = null;
        public ICommand StopCommand
        {
            get
            {
                if (stopCommand == null)
                {
                    stopCommand = new PlaybackCommand(p => vlcPlayback.Stop());
                }
                return stopCommand;
            }
        }


        private ICommand pauseCommand = null;
        public ICommand PauseCommand
        {
            get
            {
                if (pauseCommand == null)
                {
                    pauseCommand = new PlaybackCommand(p => vlcPlayback.Pause());
                }
                return pauseCommand;
            }
        }

        private ICommand quitCommand = null;
        public ICommand QuitCommand
        {
            get
            {
                if (quitCommand == null)
                {
                    quitCommand = new PlaybackCommand(p =>
                    {
                        Task.Run(() => vlcPlayback.Close());

                    });
                }
                return quitCommand;
            }
        }


        private ICommand openFileCommand = null;
        public ICommand OpenFileCommand
        {
            get
            {
                if (openFileCommand == null)
                {
                    openFileCommand = new PlaybackCommand(p =>
                    {
                        OpenFileDialog ofd = new OpenFileDialog
                        {
                            CheckFileExists = true,
                        };

                        if ((bool)ofd.ShowDialog())
                        {
                            PlayCommand.Execute(new[] { ofd.FileName });
                        }

                    });
                }
                return openFileCommand;
            }
        }



        private ICommand muteCommand = null;
        public ICommand MuteCommand
        {
            get
            {
                if (muteCommand == null)
                {
                    muteCommand = new PlaybackCommand(p =>
                    {
                        object[] args = null;
                        if (p != null)
                        {
                            args = p as object[];
                        }
                        vlcPlayback.Mute(args);
                    });
                }
                return muteCommand;
            }
        }

        private ICommand incrVolCommand = null;
        public ICommand IncrVolCommand
        {
            get
            {
                if (incrVolCommand == null)
                {
                    incrVolCommand = new PlaybackCommand(p =>
                    {
                        vlcPlayback.SetVolume(101);
                    });
                }
                return incrVolCommand;
            }
        }

        private ICommand decrVolCommand = null;
        public ICommand DecrVolCommand
        {
            get
            {
                if (decrVolCommand == null)
                {
                    decrVolCommand = new PlaybackCommand(p =>
                    {
                        vlcPlayback.SetVolume(-101);
                       
                    });
                }
                return decrVolCommand;
            }
        }

        private VideoWindow videoWindow = null;
        public VideoWindow VideoWindow
        {
            get
            {
                if (videoWindow == null)
                {
                    videoWindow = new VideoWindow
                    {
                        DataContext = this,
                    };
                }
                return videoWindow;
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
                if (!object.ReferenceEquals(this.blurEffect, value))
                {
                    this.blurEffect = value;
                    this.OnPropertyChanged(nameof(BlurEffect));
                }
            }
        }


        private string remoteAddr = "";
        public string RemoteAddr
        {
            get { return remoteAddr; }
            set
            {
                remoteAddr = value;
                OnPropertyChanged(nameof(RemoteAddr));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int ParentId { get; set; }

        public string SyncEventId { get; set; }

        public string VlcOptions { get; set; }

        public int WindowHandle { get; set; }


        public void SetBlurRadius(int blurRadius)
        {
            this.dispatcher.Invoke(() =>
            {
                if (blurRadius < 0)
                {
                    blurRadius = 0;
                }
                else if (blurRadius > 100)
                {
                    blurRadius = 100;
                }

                if (BlurEffect.Radius != blurRadius)
                {
                    BlurEffect.Radius = blurRadius;
                }
            });
        }

        internal void IncrBlurRadius(int delta)
        {
            var val = BlurEffect.Radius;
            if (delta > 0)
            {
                val++;
            }
            else if (delta < 0)
            {
                val--;
            }
            SetBlurRadius((int)val);

        }


        public void SetVideoWindowVisible(bool visible)
        {
            dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (VideoWindow.Visibility != Visibility.Visible)
                    {
                        VideoWindow.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (VideoWindow.Visibility == Visibility.Visible)
                    {
                        VideoWindow.Visibility = Visibility.Hidden;
                    }
                }
            });

        }

        private void SetupCommunications()
        {
            logger.Debug("SetupCommunications");

            if (!string.IsNullOrEmpty(RemoteAddr))
            {
                communicationClient = new CommunicationClient(this);

                communicationClient.Setup(this.RemoteAddr);

                var options = communicationClient.Connect(new[] { "eventId", "memoId" });
                if (options != null)
                {
                    //playbackHost.Volume = options.Volume;
                    //playbackHost.IsMute = options.IsMute;
                    //vlcPlayback.LoopPlayback = options.LoopPlayback;

                    this.SetBlurRadius(options.BlurRadius);


                }
            }
        }

        internal void ProcessIncomingCommand(string command, object[] args)
        {
            string arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            if (command == "Play")
            {
                if (string.IsNullOrEmpty(arg0))
                {
                    arg0 = vlcPlayback.MediaAddr;
                }
                else
                {
                    vlcPlayback.MediaAddr = arg0;
                }

                PlayCommand.Execute(arg0);
            }
            else if (command == "Pause")
            {
                PauseCommand.Execute(null);
            }
            else if (command == "Stop")
            {
                StopCommand.Execute(null);
            }
            else if (command == "Mute")
            {
                vlcPlayback.Mute(args);
            }
            else if (command == "Position")
            {
                //EnqueueCommand("Position", new[] { arg0 });
            }
            else if (command == "Volume")
            {
                int volume = 0;
                if (int.TryParse(arg0, out volume))
                {
                    vlcPlayback.SetVolume(volume);
                }

                //EnqueueCommand("Volume", new[] { arg0 });
            }
            else if (command == "SetAdjustments")
            {
                //EnqueueCommand("SetAdjustments", args);
            }
            else if (command == "SetRate")
            {
                //EnqueueCommand("SetRate", args);
            }
            else if (command == "Blur")
            {
                int blurRadius = 0;
                if (int.TryParse(arg0, out blurRadius))
                {
                    this.SetBlurRadius(blurRadius);
                }
            }
            else if (command == "SwitchVisibilityState")
            {
                bool visible = false;
                if (bool.TryParse(arg0, out visible))
                {
                    this.SetVideoWindowVisible(visible);

                }
            }
            else if (command == "SetLoopPlayback")
            {
                bool _loopPlayback = false;
                if (bool.TryParse(arg0, out _loopPlayback))
                {
                    vlcPlayback.LoopPlayback = _loopPlayback;

                }
                else
                {
                    vlcPlayback.LoopPlayback = !vlcPlayback.LoopPlayback;
                }
            }
        }



        public void Start()
        {
            VideoSourceProvider = new VideoSourceProvider();


            if (Program.ParentProcess == null)
            {
                VideoWindow.Show();

                var handle = new WindowInteropHelper(this.VideoWindow).Handle;//EnsureHandle();

                VideoSourceProvider.WindowHandle = handle;//(IntPtr)this.WindowHandle;

            }

            vlcPlayback.Start(VideoSourceProvider);

            //playbackHost.PropertyChanged += PlaybackHost_PropertyChanged;
            vlcPlayback.PlaybackChanged += vlcPlayback_PlaybackChanged;
        }

        private void vlcPlayback_PlaybackChanged(string command, object[] args)
        {
            communicationClient?.OnPostMessage(command, args);
        }

        private void PlaybackHost_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "VideoSource")
            {
                
            }
        }
    }




    public class VideoSourceProvider : INotifyPropertyChanged
    {
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        public VideoSourceProvider() { }
        public VideoSourceProvider(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        private IntPtr windowHandle;

        public IntPtr WindowHandle
        {
            get
            {

                return this.windowHandle;
            }

            set
            {
                if (this.windowHandle != value)
                {
                    this.windowHandle = value;
                    this.OnPropertyChanged(nameof(WindowHandle));
                }
            }
        }

        private InteropBitmap interopBitmap = null;
        private ImageSource videoSource;

        public ImageSource VideoSource
        {
            get
            {

                return this.videoSource;
            }

            set
            {
                if (!Object.ReferenceEquals(this.videoSource, value))
                {
                    this.videoSource = value;
                    this.OnPropertyChanged(nameof(VideoSource));
                }
            }
        }


        public void SetupVideo(IntPtr handle, int width, int height, PixelFormat fmt, int pitches, int offset)
        {
            this.dispatcher.Invoke(() =>
            {
                VideoSource = Imaging.CreateBitmapSourceFromMemorySection(handle, width, height, fmt, pitches, offset);

            });

            interopBitmap = this.VideoSource as InteropBitmap;
        }


        public void DisplayVideo()
        {
            this.dispatcher.BeginInvoke(DispatcherPriority.Render,
                (Action)(() =>
                {
                    interopBitmap?.Invalidate();
                }));
        }

        public void CleanupVideo()
        {
            VideoSource = null;
            interopBitmap = null;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class MediaResource
    {

    }

    public class PlaybackStatistics : INotifyPropertyChanged
    {

        private int bytesRead = 0;
        public int ReadBytes
        {
            get { return bytesRead; }
            set
            {
                bytesRead = value;
                OnPropertyChanged(nameof(ReadBytes));
            }
        }

        private int demuxBytesRead = 0;
        public int DemuxReadBytes
        {
            get { return demuxBytesRead; }
            set
            {
                demuxBytesRead = value;
                OnPropertyChanged(nameof(DemuxReadBytes));
            }
        }

        private int displayedPictures = 0;
        public int DisplayedPictures
        {
            get { return displayedPictures; }
            set
            {
                displayedPictures = value;
                OnPropertyChanged(nameof(DisplayedPictures));
            }
        }

        private int playedAudioBuffers = 0;
        public int PlayedAudioBuffers
        {
            get { return playedAudioBuffers; }
            set
            {
                playedAudioBuffers = value;
                OnPropertyChanged(nameof(PlayedAudioBuffers));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}
