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
using VlcContracts;

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

        public SharedBuffer VideoBuffer { get; private set; }

        public void Start()
        {
            logger.Debug("Start()");

            var options = Session.Config.Options;

            vlcPlayback = new VlcPlayback();
            vlcPlayback.PlaybackChanged += vlcPlayback_PlaybackChanged;

            vlcPlayback.PropertyChanged+= (s, e) => 
            {
                if (e.PropertyName == "State")
                {
                    OnPropertyChanged(nameof(PlaybackState));
                    OnPropertyChanged(nameof(IsPlaying));
                }
            };


            Session.Config.ExchangeId = Guid.NewGuid();

            if (Session.ParentProcess == null)
            {
                MainWindow.Show();

                var handle = new WindowInteropHelper(this.MainWindow).Handle; //EnsureHandle();

                if (handle != IntPtr.Zero)
                {
                    vlcPlayback.SetVideoHostHandle(handle);
                }
            }

            var videoBufferId = Session.Config.ExchangeId;
            if (videoBufferId != Guid.Empty)
            {
                string bufferName = Session.Config.ExchangeId.ToString("N");

                int headerSize = 1024;
                VideoBufferInfo videoInfo = new VideoBufferInfo
                {
                    Width = 1920,
                    Height = 1080,
                    PixelFormat = System.Windows.Media.PixelFormats.Bgr32,
                    DataOffset = headerSize,
                    
                };

                var videoSize = VideoUtils.EstimateVideoSize(videoInfo.Width, videoInfo.Height, videoInfo.PixelFormat);

                int buffrerCapacity = videoSize + headerSize;

                this.VideoBuffer = new SharedBuffer(bufferName, buffrerCapacity);
                this.VideoBuffer.WriteData(videoInfo);

                vlcPlayback.SetOutputVideoToBuffer(VideoBuffer);
            }


            vlcPlayback.Start(options?.FileName);

        }

        private void vlcPlayback_PlaybackChanged(string command, object[] args)
        {
            //...
            playbackHost.OnSendCommand(command, args);
            if(command == "Position")
            {
                OnPropertyChanged(nameof(Position));
            }

        }

        public void Close()
        {
            logger.Debug("Close()");

            if (vlcPlayback != null)
            {
                vlcPlayback.PlaybackChanged -= vlcPlayback_PlaybackChanged;
                vlcPlayback.Close();
            }

            VideoBuffer?.Dispose();

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
                        //var fileName = "";
                        //if (obj != null)
                        //{
                        //    object[] args = obj as object[];
                        //    if (args != null && args.Length > 0)
                        //    {
                        //        fileName = args[0]?.ToString();

                        //    }
                        //}
                        //vlcPlayback.Play(fileName);
                        //EnqueueCommand("Play", new[] { this.MediaAddr });

                        if (obj != null)
                        {
                            object[] args = obj as object[];
                            vlcPlayback.Play(args);
                        }
                        else
                        {
                            vlcPlayback.Play();
                        }

     
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
                            bool force = true;
                            PlayCommand.Execute(new object[] { ofd.FileName , force });
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

        private ICommand posCommand = null;
        public ICommand PositionCommand
        {
            get
            {
                if (posCommand == null)
                {
                    posCommand = new PlaybackCommand(p =>
                    {
                        double pos = (double)p; 
                        vlcPlayback.SetPosition(pos);

                    });
                }
                return posCommand;
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


        public PlaybackState PlaybackState
        {
            get { return vlcPlayback?.State?? PlaybackState.Created; }

        }

        public bool IsPlaying
        {
            get
            {
                bool isPlaying = false;
                if(vlcPlayback?.State == PlaybackState.Playing)
                {
                    isPlaying = true;
                }

                return isPlaying;
            }
        }

        public double Position
        {
            get
            {
                return vlcPlayback?.Position ?? 0;
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
                double pos = 0;
                if (double.TryParse(arg0, out pos))
                {
                    vlcPlayback.SetPosition(pos);
                }

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
