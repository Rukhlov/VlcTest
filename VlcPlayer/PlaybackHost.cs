using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace VlcPlayer
{
    public class PlaybackHost
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private CommunicationClient ipcChannel = null;

        private PlaybackController playbackController = null;
        public Dispatcher Dispatcher { get; private set; }

        public void Run()
        {
            Dispatcher = Dispatcher.CurrentDispatcher;

            var cmdOpts = Session.Config.Options;

            string remoteAddr = cmdOpts?.ServerAddr;

            if (!string.IsNullOrEmpty(remoteAddr))
            {
                ipcChannel = new CommunicationClient(this);

                ipcChannel.Setup(remoteAddr);

                var options = ipcChannel.Connect(new[] { "eventId", "memoId" });
                if (options != null)
                {
                    //playbackHost.Volume = options.Volume;
                    //playbackHost.IsMute = options.IsMute;
                    //vlcPlayback.LoopPlayback = options.LoopPlayback;

                    //this.SetBlurRadius(options.BlurRadius);


                }
            }

            playbackController = new PlaybackController(this);

            playbackController.Start();

            System.Windows.Threading.Dispatcher.Run();
        }

        public void OnSendCommand(string command, object[] args)
        {
            //...

            ipcChannel?.OnPostMessage(command, args);
        }

        public void OnReceiveCommand(string command, object[] args)
        {
            //...

            playbackController?.ProcessCommand(command, args);
        }

        public void Quit()
        {
            try
            {
                ipcChannel?.Close();
                playbackController?.Close();

                Dispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                Process.GetCurrentProcess().Kill();
            }

        }
    }

    public class PlaybackController : INotifyPropertyChanged
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly Dispatcher dispatcher = null;
        private readonly PlaybackHost playbackHost = null;

        public PlaybackController(PlaybackHost host)
        {
            this.dispatcher = host.Dispatcher;
            this.playbackHost = host;
        }

        private VlcPlayback vlcPlayback = null;
       

        public void Start()
        {
            logger.Debug("Start()");

            var options = Session.Config.Options;

            vlcPlayback = new VlcPlayback();
            vlcPlayback.PlaybackChanged += vlcPlayback_PlaybackChanged;

            if (options != null)
            {
                vlcPlayback.MediaAddr = options.FileName;
                this.RemoteAddr = options.ServerAddr;
                this.ParentId = options.ParentId;
                this.WindowHandle = options.WindowHandle;
            }

            Session.Config.ExchangeId = Guid.NewGuid();

            if (Session.ParentProcess == null)
            {
                MainWindow.Show();

                var handle = new WindowInteropHelper(this.MainWindow).Handle;//EnsureHandle();

                //videoSourceProvider.WindowHandle = handle;
               // Session.Config.VideoOutHandle = handle;


            }
           
            vlcPlayback.Start(Session.Config.VideoOutHandle, Session.Config.ExchangeId);


        }


        private void vlcPlayback_PlaybackChanged(string command, object[] args)
        {
            //...
            playbackHost.OnSendCommand(command, args);

        }

        public void Close()
        {
            logger.Debug("Close()");

            if (vlcPlayback != null)
            {
                vlcPlayback.PlaybackChanged -= vlcPlayback_PlaybackChanged;
                vlcPlayback.Close();
            }

            //try
            //{
            //}
            //catch (Exception ex)
            //{
            //    logger.Fatal(ex);
            //    Process.GetCurrentProcess().Kill();
            //}
        }


        private VideoWindow mainWindow = null;
        public VideoWindow MainWindow
        {
            get
            {
                if (mainWindow == null)
                {
                    mainWindow = new VideoWindow
                    {
                        DataContext = this,
                    };
                }
                return mainWindow;
            }
        }

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
                            if (args != null && args.Length > 0)
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
                        Task.Run(() =>
                        {
                            playbackHost.Quit();

                        });

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
                        vlcPlayback.SetMute(true);
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


        private Visibility visibility = Visibility.Visible;
        public Visibility Visibility
        {
            get => visibility;
            set
            {
                if (visibility != value)
                {
                    visibility = value;
                    OnPropertyChanged(nameof(Visibility));
                }
            }
        }

        public void SetVideoWindowVisible(bool visible)
        {
            dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (MainWindow.Visibility != Visibility.Visible)
                    {
                        MainWindow.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (MainWindow.Visibility == Visibility.Visible)
                    {
                        MainWindow.Visibility = Visibility.Hidden;
                    }
                }
            });

        }


        public void ProcessCommand(string command, object[] args)
        {
            string arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            if (command == "Play")
            {
                //if (string.IsNullOrEmpty(arg0))
                //{
                //    arg0 = vlcPlayback.MediaAddr;
                //}
                //else
                //{
                //    vlcPlayback.MediaAddr = arg0;
                //}
                //vlcPlayback.Play(arg0);

                PlayCommand.Execute(args);
            }
            else if (command == "Pause")
            {
                PauseCommand.Execute(null);

                //vlcPlayback.Pause();
            }
            else if (command == "Stop")
            {
                StopCommand.Execute(null);

                // vlcPlayback.Stop();
            }
            else if (command == "Mute")
            {
                bool mute = false;
                if (bool.TryParse(arg0, out mute))
                {
                    vlcPlayback.SetMute(mute);
                }
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
                    SetBlurRadius(blurRadius);
                }
            }
            else if (command == "SwitchVisibilityState")
            {

                bool visible = false;
                if (bool.TryParse(arg0, out visible))
                {
                    SetVideoWindowVisible(visible);
                    // playbackController.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

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


    public class PlaybackCommand : System.Windows.Input.ICommand
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
