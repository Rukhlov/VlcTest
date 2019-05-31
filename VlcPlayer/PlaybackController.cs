using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VlcContracts;

namespace VlcPlayer
{


    public class PlaybackController : INotifyPropertyChanged
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly Dispatcher dispatcher = null;
        private readonly PlaybackHost host = null;

        private readonly VlcPlayback playback = null;

        public PlaybackController(PlaybackHost host)
        {
            this.host = host;
            this.dispatcher = host.Dispatcher;
            this.playback = host.Playback;

            this.playback.PropertyChanged += Playback_PropertyChanged;
        }

        private void Playback_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "MediaAddr")
            {
                OnPropertyChanged(nameof(MediaAddr));
            }
            else if (e.PropertyName == "State")
            {
                OnPropertyChanged(nameof(PlaybackState));
            }
            else if (e.PropertyName == "PlaybackStats")
            {
                OnPropertyChanged(nameof(StatInfo));
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
                        if (obj != null)
                        {
                            object[] args = obj as object[];
                            playback.Play(args);
                        }
                        else
                        {
                            playback.Play();
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
                    stopCommand = new PlaybackCommand(p => playback.Stop());
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
                    pauseCommand = new PlaybackCommand(p => playback.Pause());
                }
                return pauseCommand;
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
                            PlayCommand.Execute(new object[] { ofd.FileName, force });
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
                        playback.SetMute(true);
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
                        playback.SetVolume(101);
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
                        playback.SetVolume(-101);

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
                        playback.SetPosition(pos);

                    });
                }
                return posCommand;
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
                            host.Quit();

                        });

                    });
                }
                return quitCommand;
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
            get { return playback?.State ?? PlaybackState.Created; }

        }

        public bool IsPlaying
        {
            get
            {
                bool isPlaying = false;
                if (playback?.State == PlaybackState.Playing)
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
                var pos = playback?.Position ?? 0;
               // host?.MainWindow?.UpdatePosition(pos);
                return pos;
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
                var window = host?.MainWindow;
                if (visible)
                {
                    if (window.Visibility != Visibility.Visible)
                    {
                        window.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (window.Visibility == Visibility.Visible)
                    {
                        window.Visibility = Visibility.Hidden;
                    }
                }
            });

        }


        private MemoryRenderer renderer = null;
        public MemoryRenderer Renderer
        {
            get
            {
                if (renderer == null)
                {
                    if (playback?.VideoBuffer != null)
                    {
                        renderer = new MemoryRenderer(playback.VideoBuffer);
                    }
                }

                return renderer;
            }
        }

        public string StatInfo
        {
            get
            {

                string statInfo = "";

                var stats = playback?.PlaybackStats;
                if (stats != null)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("ReadBytes: " + stats.ReadBytes);
                    sb.AppendLine("DemuxReadBytes: " + stats.DemuxReadBytes);
                    sb.AppendLine("DisplayedPictures: " + stats.DisplayedPictures);
                    sb.AppendLine("PlayedAudioBuffers: " + stats.PlayedAudioBuffers);

                    statInfo = sb.ToString();
                }

                return statInfo;
            }
        }


        public string MediaAddr
        {
            get
            {
                return playback?.MediaAddr??"";
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



    public class MemoryRenderer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Dispatcher dispatcher = null;
        private Image image = null;

        public MemoryRenderer(SharedBuffer buffer)
        {
            this.videoBuffer = buffer;

        }

        private InteropBitmap interopBitmap = null;

        private SharedBuffer videoBuffer = null;

        public void Run(Image image)
        {
            this.image = image;
            this.dispatcher = image.Dispatcher;

            logger.Debug("Run(...) " + videoBuffer.Name);

            closing = false;

            Task task = Task.Run(() =>
            {
                logger.Debug("Render task started...");

                //sharedBuf = new SharedBuffer(ExchangeId.ToString("N"));

                try
                {

                    videoBuffer.WaitForSignal();

                    bool started = false;

                    VideoBufferInfo vi = new VideoBufferInfo();

                    while (!closing)
                    {

                        vi = videoBuffer.ReadData<VideoBufferInfo>();

                        if (vi.State == VideoBufferState.Display)
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

                                            interopBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(videoBuffer.Section, width, height, fmt, pitches, offset);

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

                        videoBuffer.WaitForSignal(1000);
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

        private bool closing = false;
        public void Close()
        {

            logger.Debug("Close()");

            closing = true;

            videoBuffer?.Pulse();

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
