using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using System.Windows.Media.Effects;
using System.Windows.Threading;

using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops;
using Vlc.DotNet.Core.Interops.Signatures;



namespace VlcPlayer
{

    public class PlaybackHost : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly App app = null;
        private readonly Dispatcher dispatcher = null;
        public PlaybackHost(App _app)
        {
            this.app = _app;
            this.dispatcher = this.app.Dispatcher;

            Session = new PlaybackSession(App.CommandLineOptions);


            this.communicationClient = new CommunicationClient(this);
            this.videoProvider = new VideoProvider(this);
            this.audioProvider = new AudioProvider(this);

            VideoVisibility = (App.parentProcess == null) ? Visibility.Visible : Visibility.Collapsed;
            Session.PlaybackState = PlaybackState.Created;

        }

        private CommunicationClient communicationClient = null;

        private VideoProvider videoProvider = null;
        private AudioProvider audioProvider = null;

        private VlcMediaPlayer mediaPlayer = null;

        private InteropBitmap interopBitmap = null;

        private bool loopPlayback = false;

        private PlaybackSession session;
        public PlaybackSession Session
        {
            get { return session; }
            private set
            {
                session = value;
                OnPropertyChanged(nameof(Session));
            }
        }

        private Visibility videoVisibility = Visibility.Collapsed;
        public Visibility VideoVisibility
        {
            get { return videoVisibility; }
            set
            {
                videoVisibility = value;
                OnPropertyChanged(nameof(VideoVisibility));
            }
        }


        private string playbackTiming;
        public string PlaybackTiming
        {
            get { return playbackTiming; }
            set
            {
                if (playbackTiming != value)
                {
                    playbackTiming = value;
                    OnPropertyChanged(nameof(PlaybackTiming));
                }
            }
        }

        private ICommand playCommand = null;
        public ICommand PlayCommand
        {
            get
            {
                if (playCommand == null)
                {
                    //playCommand = new PlaybackCommand(p => this.Play());

                    playCommand = new PlaybackCommand(m =>
                    {
                        EnqueueCommand("Play", new[] { Session.MediaAddr });

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
                    stopCommand = new PlaybackCommand(p => EnqueueCommand("Stop"));
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
                    pauseCommand = new PlaybackCommand(p => EnqueueCommand("Pause"));
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
                        this.Close();
                        Environment.Exit(0);
                    });
                }
                return quitCommand;
            }
        }


        public void Setup()
        {
            try
            {
                Session.PlaybackState = PlaybackState.Initializing;

                //var options = new string[] { ":audio-visual=visual", ":effect-list=spectrum" };
                //var options = new string[] { "--video-filter=transform:sepia", "--sepia-intensity=100", "--transform-type=180" };
                // var vlcopts = new string[] { "input-repeat=65535" };
                //var opts = new string[] { "--aout=\"waveout\"" };

                var opts = new string[] { "--extraintf=logger", "--verbose=0" };
                this.mediaPlayer = CreatePlayer(App.VlcLibDirectory, opts);
                //this.mediaPlayer = CreatePlayer(App.VlcLibDirectory);

                videoProvider.Setup();
                //audioProvider.Setup();

                if (!string.IsNullOrEmpty(Session.RemoteAddr))
                {
                    communicationClient.Setup(Session.RemoteAddr);
                    //await SetupRemoteConnection();
                    //TryToStart();
                }

                playbackThread = new Thread(PlaybackProc);
                playbackThread.Start();
            }
            catch(Exception ex)
            {
                logger.Fatal(ex);
                this.Dispose();

                throw;
            }

        }


        private VlcMediaPlayer CreatePlayer(DirectoryInfo directory, string[] options = null)
        {
            var _options = "";
            if (options != null)
            {
                _options = string.Join(" ", options);
            }

            logger.Debug("CreataPlayer(...) " + directory.ToString(), " " + _options);

            var player = new VlcMediaPlayer(directory, options);

            // player.Log += MediaPlayer_Log;
            player.EncounteredError += MediaPlayer_EncounteredError;

            player.Opening += MediaPlayer_Opening;
            player.Playing += MediaPlayer_Playing;
            player.Paused += MediaPlayer_Paused;
            player.EndReached += Player_EndReached;
            player.Stopped += MediaPlayer_Stopped;

            player.PositionChanged += MediaPlayer_PositionChanged;
            player.LengthChanged += MediaPlayer_LengthChanged;

            player.AudioDevice += Player_AudioDevice;
            player.AudioVolume += MediaPlayer_AudioVolume;
            player.Muted += MediaPlayer_Muted;
            player.Unmuted += Player_Unmuted; ;
            player.VideoOutChanged += Player_VideoOutChanged;
            player.Video.IsKeyInputEnabled = false;
            player.Video.IsMouseInputEnabled = false;

            player.SetUserAgent("Mitsar Vlc Player", "Mitsar Vlc Player");

            return player;

        }




        class InternalCommand
        {
            public string command = "";
            public object[] args = null;
        }

        private Thread playbackThread = null;
        private AutoResetEvent syncEvent = new AutoResetEvent(false);
        private volatile bool closing = false;
        private Queue<InternalCommand> commandQueue = new Queue<InternalCommand>();

        private object locker = new object();
        private InternalCommand DequeueCommand()
        {
            if (closing)
            {
                return null;
            }


            InternalCommand command = null;
            lock (locker)
            {
                if (commandQueue.Count > 0)
                {
                    command = commandQueue.Dequeue();
                }
            }

            return command;
        }

        private void EnqueueCommand(string command, object[] args = null)
        {
            if (closing)
            {
                return;
            }

            lock (locker)
            {
                commandQueue.Enqueue(new InternalCommand { command = command, args = args });
            }
            syncEvent.Set();
        }

        public event Action<object> Closed;     
        private void OnClosed(object obj)
        {
            logger.Debug("OnClosed(...)");
            Closed?.Invoke(obj);
        }

        private void PlaybackProc()
        {
            logger.Trace("PlaybackProc() BEGIN");

            try
            {
                Session.PlaybackState = PlaybackState.Initialized;

                if (!string.IsNullOrEmpty(Session.MediaAddr))
                {
                    PlayCommand.Execute(Session.MediaAddr);
                }

                while (true)
                {
                    try
                    {
                        InternalCommand command = DequeueCommand();

                        if (command != null)
                        {
                            ProcessInternalCommand(command);
                        }
                        else
                        {
                            ProcessStatistic();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }


                    syncEvent.WaitOne(300);
                    if (closing)
                    {
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                logger.Error(ex);

            }
            finally
            {

                OnClosed(null);

                //Dispose();
                //Environment.Exit(0);
            }

            logger.Trace("PlaybackProc() END");
        }


        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private void ProcessInternalCommand(InternalCommand command)
        {
            logger.Debug("ProcessInternalCommand(...)");

            if (closing)
            {
                return;
            }

            if (command == null)
            {
                return;
            }

            logger.Debug("command " + command.command);

            switch (command.command)
            {
                case "Play":
                    {
                        string mediaLink = "";
                        var args = command.args;
                        if (args != null && args.Length > 0)
                        {
                            mediaLink = (string)args[0];
                        }

                        if (!string.IsNullOrEmpty(mediaLink))
                        {
                            Session.PlaybackState = PlaybackState.Opening;
                            cancellationTokenSource = new CancellationTokenSource();
                            Task.Run(() =>
                            {
                                TryGetMrl(mediaLink, cancellationTokenSource.Token);

                            });
                        }
                        else
                        {
                            Task.Run(() =>
                            {
                                var media = mediaPlayer?.GetMedia();
                                var mrl = media?.Mrl;
                                if (!string.IsNullOrEmpty(mrl))
                                {
                                    mediaPlayer.Play(mrl);
                                }
                            });
                        }

                    }
                    break;
                case "PlayMrl":
                    {
                        //if (Session.PlaybackState == PlaybackState.Opening)
                        {
                            string mrl = "";
                            var args = command.args;
                            if (args != null && args.Length > 0)
                            {
                                mrl = (string)args[0];
                            }

                            Task.Run(() =>
                            {
                                if (!string.IsNullOrEmpty(mrl))
                                {
                                    logger.Info(mrl);

                                    mediaPlayer.Play(mrl);
                                }
                            });
                        }
                    }
                    break;
                case "Opening":
                    {
                        Session.PlaybackState = PlaybackState.Opening;
                        Session.PlaybackStats = new PlaybackStatistics();

                        PostToClientAsync("Opening");
                    }
                    break;
                case "LengthChanged":
                    {
                        var args = command.args;
                        if (args != null && args.Length > 0)
                        {
                            PostToClientAsync("LengthChanged", new object[] { args[0] });
                        }

                    }
                    break;
                case "Playing":
                    {

                        Session.PlaybackState = PlaybackState.Playing;

                        var media = mediaPlayer?.GetMedia();
                        var mediaStr = media?.GetInfoString();
                        logger.Info(mediaStr);

                        //Session.PlaybackStats = new PlaybackStatistics();

                        PostToClientAsync("Playing", new object[] { mediaStr });
                    }
                    break;
                case "Pause":
                    {
                        if (Session.PlaybackState == PlaybackState.Paused ||
                            Session.PlaybackState == PlaybackState.Playing)
                        {
                            if (mediaPlayer != null)
                            {
                                if (mediaPlayer.IsPausable())
                                {
                                    mediaPlayer.Pause();
                                }
                            }
                        }
                    }
                    break;
                case "Paused":
                    {
                        Session.PlaybackState = PlaybackState.Paused;
                        PostToClientAsync("Paused");
                    }
                    break;
                case "Stop":
                    {
                        cancellationTokenSource?.Cancel();

                        mediaPlayer?.Stop();
                    }
                    break;
                case "EndReached":
                    {
                        if (loopPlayback)
                        {

                            var media = mediaPlayer?.GetMedia();
                            var mrl = media?.Mrl;
                            if (!string.IsNullOrEmpty(mrl))
                            {
                                mediaPlayer.Play(mrl);
                            }

                            PostToClientAsync("Restart");

                            //Task.Run(() => StartPlaying("", cancellationTokenSource.Token));
                        }
                    }
                    break;
                case "EncounteredError":
                    {
                        Session.PlaybackState = PlaybackState.Failed;
                        //...
                    }
                    break;
                case "Stopped":
                    {

                        Session.PlaybackState = PlaybackState.Stopped;
                        Session.PlaybackStats = null;
                        Session.Position = 0;
                        Session.Volume = -1;
                        PlaybackTiming = "";

                        lock (locker)
                        {
                            commandQueue.Clear();
                        }

                        PostToClientAsync("Stopped");
                        //PostMessage("Stopped");
                    }
                    break;
                case "Position":
                    {
                        var val0 = "";
                        var args = command.args;
                        if (args != null && args.Length > 0)
                        {
                            val0 = (string)args[0];
                        }
                        double pos = 0;
                        if (double.TryParse(val0, out pos))
                        {
                            SetPlayerPosition(pos);
                        }
                    }
                    break;
                case "PositionChanged":
                    {

                    }
                    break;
                case "Mute":
                    {
                        mediaPlayer?.Audio?.ToggleMute();
                    }
                    break;
                case "Muted":
                    {
                        Session.IsMute = true;
                        PostToClientAsync("Muted");
                    }
                    break;
                case "Unmuted":
                    {
                        Session.IsMute = false;
                        PostToClientAsync("Unmuted");
                    }
                    break;
                case "Volume":
                    {
                        var val0 = "";
                        var args = command.args;
                        if (args != null && args.Length > 0)
                        {
                            val0 = (string)args[0];
                        }

                        int volume = 0;
                        if (int.TryParse(val0, out volume))
                        {
                            SetPlayerVolume(volume);
                        }
                    }
                    break;
                case "AudioVolume":
                    {
                        Session.Volume = (int)mediaPlayer?.Audio?.Volume;

                        PostToClientAsync("AudioVolume", new object[] { Session.Volume });
                    }
                    break;
                default:
                    {

                    }
                    break;

            }

        }


        private void ProcessStatistic()
        {
            if (closing)
            {
                return;
            }

            var media = mediaPlayer?.GetMedia();
            if (media != null)
            {
                var stats = media.Statistics;

                var playbackStats = Session?.PlaybackStats;
                if (playbackStats != null)
                {
                    playbackStats.DemuxReadBytes = stats.DemuxReadBytes;
                    playbackStats.ReadBytes = stats.ReadBytes;
                    playbackStats.DisplayedPictures = stats.DisplayedPictures;
                    playbackStats.PlayedAudioBuffers = stats.PlayedAudioBuffers;
                }

                if (media.State == MediaStates.Playing)
                {
                    PostToClientAsync("Position", new object[] { Session?.Position });
                }

                //Session.Stats = "ReadBytes: " + stats.ReadBytes + "\r\n" +
                //        "DemuxReadBytes: " + stats.DemuxReadBytes + "\r\n" +
                //        "DisplayedPictures: " + stats.DisplayedPictures + "\r\n" +
                //        "PlayedAudioBuffers: " + stats.PlayedAudioBuffers;
            }
        }

        internal async void TryGetMrl(string mediaLink, CancellationToken cancellationToken)
        {
            logger.Debug("TryGetMrl(...) " + mediaLink);

            if (closing)
            {
                return;
            }

            if (!string.IsNullOrEmpty(mediaLink))
            {
                try
                {
                    var mrl = await GetMrlAsync(mediaLink, cancellationToken);

                    EnqueueCommand("PlayMrl", new[] { mrl, mediaLink });
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    //EnqueueCommand("MediaResourceLocator", new[] { mrl });
                    Debug.Fail(ex.Message);
                    Debug.WriteLine(ex);
                }
            }

        }

        private async Task<string> GetMrlAsync(string mediaLink, CancellationToken cancellationToken)
        {

            string mri = "";

            Uri u = new Uri(mediaLink);
            if (u.Scheme == "http" || u.Scheme == "https")
            {
                string videoId = "";
                if (YoutubeApi.YoutubeClient.TryParseVideoId(mediaLink, out videoId))
                {
                    using (var youtube = new YoutubeApi.YoutubeClient())
                    {
                        cancellationToken.Register(() =>
                        {
                            youtube?.Dispose();
                        });

                        var streamInfo = await youtube.GetVideoMediaStreamInfosAsync(videoId);
                        if (streamInfo != null)
                        {
                            var moreQuality = streamInfo.Muxed.OrderByDescending(s => s.VideoQuality).FirstOrDefault();

                            if (moreQuality != null)
                            {
                                mri = moreQuality.Url;

                            }
                        }

                    }
                }
                else
                {// если не youtube 
                    mri = u.AbsoluteUri;
                }
            }
            else
            {
                mri = u.AbsoluteUri;
            }
            return mri;
        }



        private void MediaPlayer_Log(object sender, VlcMediaPlayerLogEventArgs e)
        {
            // string message = $"libVlc : {e.Level} {e.Message} @ {e.Module}";
            //if (e.Level == VlcLogLevel.Error || e.Level == VlcLogLevel.Warning)
            //{
            //    Log(e.Message);
            //}

            //logger.Debug(e.Message);
        }

        private void MediaPlayer_EncounteredError(object sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            logger.Trace("MediaPlayer.EncounteredError(...) ");

           // Session.PlaybackState = PlaybackState.Failed;

            EnqueueCommand("EncounteredError");
        }

        private void MediaPlayer_Opening(object sender, VlcMediaPlayerOpeningEventArgs e)
        {
            logger.Trace("Opening(...) ");

            EnqueueCommand("Opening");

            //Session.PlaybackState = PlaybackState.Opening;
            //PostMessage("Opening");

        }

        private void Player_VideoOutChanged(object sender, VlcMediaPlayerVideoOutChangedEventArgs e)
        {
            logger.Trace("Player_VideoOutChanged(...) " + e.NewCount);
        }

        private void Player_AudioDevice(object sender, VlcMediaPlayerAudioDeviceEventArgs e)
        {
            logger.Trace("Player_AudioDevice(...) " + e.Device);

        }

        private void MediaPlayer_Playing(object sender, VlcMediaPlayerPlayingEventArgs e)
        {
            logger.Trace("Playing(...)");

            EnqueueCommand("Playing");

            //var media = mediaPlayer.GetMedia();
            //var mediaStr = media?.GetInfoString();

            //logger.Info(mediaStr);

            //Session.PlaybackState = PlaybackState.Playing;
            //Session.PlaybackStats = new PlaybackStatistics();
            //PostMessage("Playing", new object[] { mediaStr });


        }

        private void MediaPlayer_Paused(object sender, VlcMediaPlayerPausedEventArgs e)
        {
            logger.Debug("Paused(...)");

            EnqueueCommand("Paused");

            //Session.PlaybackState = PlaybackState.Paused;
            //PostMessage("Paused");
        }

        private void Player_EndReached(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            logger.Debug("Player_EndReached(...)");

            EnqueueCommand("EndReached");
        }

        private void MediaPlayer_Stopped(object sender, VlcMediaPlayerStoppedEventArgs e)
        {
            logger.Debug("Stopped(...) " + mediaPlayer.State);

            EnqueueCommand("Stopped");

            //Session.PlaybackState = PlaybackState.Stopped;
            //Session.PlaybackStats = null;
            //Session.Position = 0;
            //Session.Volume = -1;
            //PlaybackTiming = "";

            //PostMessage("Stopped");

        }


        private void MediaPlayer_PositionChanged(object sender, VlcMediaPlayerPositionChangedEventArgs e)
        {
            logger.Debug("PositionChanged(...) " + e.NewPosition);

            Session.Position = e.NewPosition;
            var media = mediaPlayer.GetMedia();
            if (media != null)
            {
                var total = media.Duration;
                var current = TimeSpan.FromMilliseconds(total.TotalMilliseconds * e.NewPosition);

                PlaybackTiming = current.ToString("hh\\:mm\\:ss") + " / " + total.ToString("hh\\:mm\\:ss");
            }

            // PostMessage("Position", new object[] { e.NewPosition });

        }

        private void MediaPlayer_LengthChanged(object sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            logger.Trace("LengthChanged(...)");
            EnqueueCommand("LengthChanged", new object[] { e.NewLength });

        }

        private void MediaPlayer_Muted(object sender, EventArgs e)
        {
            logger.Debug("MediaPlayer_Muted(...)");
            EnqueueCommand("Muted");
            //Session.IsMute = true;

        }

        private void Player_Unmuted(object sender, EventArgs e)
        {
            logger.Debug("Player_Unmuted(...)");

            EnqueueCommand("Unmuted");
            //Session.IsMute = false;
        }

        private void MediaPlayer_AudioVolume(object sender, VlcMediaPlayerAudioVolumeEventArgs e)
        {

            logger.Debug("AudioVolume(...) ");
            EnqueueCommand("AudioVolume");

            //Session.Volume = (int)mediaPlayer?.Audio?.Volume;
        }

        public void Close()
        {

            logger.Debug("Close(...)");

            closing = true;
            syncEvent.Set();

            if (!playbackThread.Join(3000))
            {
                playbackThread.Abort();
            }
 
        }

        public void Dispose()
        {
            communicationClient?.Close();


            if (mediaPlayer != null)
            {
                mediaPlayer.EncounteredError -= MediaPlayer_EncounteredError;
                mediaPlayer.Opening -= MediaPlayer_Opening;
                mediaPlayer.Playing -= MediaPlayer_Playing;
                mediaPlayer.Paused -= MediaPlayer_Paused;
                mediaPlayer.EndReached -= Player_EndReached;
                mediaPlayer.Stopped -= MediaPlayer_Stopped;
                mediaPlayer.PositionChanged -= MediaPlayer_PositionChanged;
                mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                mediaPlayer.AudioDevice -= Player_AudioDevice;
                mediaPlayer.AudioVolume -= MediaPlayer_AudioVolume;
                mediaPlayer.Muted -= MediaPlayer_Muted;
                mediaPlayer.Unmuted -= Player_Unmuted; ;
                mediaPlayer.VideoOutChanged -= Player_VideoOutChanged;

                mediaPlayer.Dispose();
                mediaPlayer = null;
            }

            videoProvider?.Dispose();

            //Environment.Exit(0);
        }



        public void Log(string msg)
        {
            logger.Info(msg);

            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            //new Action(() =>
            //{

            //    string text = DateTime.Now.ToString("HH:mm:ss.fff") + " >> " + msg + "\r";//+ Environment.NewLine;
            //    Logger.AppendText(text);


            //}));

        }


        internal void ProcessIncomingCommand(string command, object[] args)
        {

            string val0 = "";
            if (args != null && args.Length > 0)
            {
                val0 = args[0]?.ToString();
            }

            if (command == "Play")
            {
                if (string.IsNullOrEmpty(val0))
                {
                    val0 = Session.MediaAddr;
                }
                else
                {
                    Session.MediaAddr = val0;
                }

                PlayCommand.Execute(val0);
            }
            else if (command == "Pause")
            {
                PauseCommand.Execute(null);
                // Pause();
            }
            else if (command == "Stop")
            {
                StopCommand.Execute(null);

            }
            else if (command == "Mute")
            {
                EnqueueCommand("Mute");
            }
            else if (command == "Position")
            {
                EnqueueCommand("Position", new[] { val0 });
            }
            else if (command == "Volume")
            {
                EnqueueCommand("Volume", new[] { val0 });
            }
            else if (command == "SwitchVideoAdjustments")
            {
                if (mediaPlayer.Video != null)
                {
                    var videoAdjustments = mediaPlayer.Video.Adjustments;
                    if (videoAdjustments != null)
                    {
                        videoAdjustments.Enabled = !videoAdjustments.Enabled;
                    }
                }
            }
            else if (command == "Brightness")
            {
                float brightness = 0;
                if (float.TryParse(val0, out brightness))
                {
                    SetVideoAdjastment(VideoAdjustOptions.Brightness, brightness);
                }
            }
            else if (command == "Hue")
            {
                float hue = 0;
                if (float.TryParse(val0, out hue))
                {
                    SetVideoAdjastment(VideoAdjustOptions.Hue, hue);
                }
            }
            else if (command == "Contrast")
            {
                float contrast = 0;
                if (float.TryParse(val0, out contrast))
                {
                    SetVideoAdjastment(VideoAdjustOptions.Contrast, contrast);
                }
            }
            else if (command == "Gamma")
            {
                float gamma = 0;
                if (float.TryParse(val0, out gamma))
                {
                    SetVideoAdjastment(VideoAdjustOptions.Gamma, gamma);

                }
            }
            else if (command == "Saturation")
            {
                float saturation = 0;
                if (float.TryParse(val0, out saturation))
                {
                    SetVideoAdjastment(VideoAdjustOptions.Saturation, saturation);
                }
            }
            else if (command == "SwitchVisibilityState")
            {
                bool visible = false;
                if(bool.TryParse(val0, out visible ))
                {
                    this.dispatcher.Invoke(() =>
                    {
                        if (visible)
                        {
                            if (VideoVisibility != Visibility.Visible)
                            {
                                var videoWindow = app.Windows.OfType<VideoWindow>().FirstOrDefault();
                                if (videoWindow == null)
                                {
                                    videoWindow = new VideoWindow
                                    {
                                        DataContext = this,
                                    };
                                }

                                VideoVisibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            if (VideoVisibility == Visibility.Visible)
                            {
                                VideoVisibility = Visibility.Hidden;
                            }
                        }
                    });

                }
            }
            else if (command == "Blur")
            {
                int blurRadius = 0;
                if (int.TryParse(val0, out blurRadius))
                {
                    SetBlurRadius(blurRadius);
                }
            }
            else if (command == "SwitchLoopPlayback")
            {
                bool _loopPlayback = false;
                if (bool.TryParse(val0, out _loopPlayback))
                {
                    if (loopPlayback != _loopPlayback)
                    {
                        loopPlayback = _loopPlayback;
                    }
                }
                else
                {
                    loopPlayback = !loopPlayback;
                }
            }

        }

        private void SetVideoAdjastment(VideoAdjustOptions opts, float val)
        {
            /*
             *  contrast <float> : Contrast (0.0 - 2.0). default value: 1.0
                brightness <float> : Brightness (0.0 - 2.0). default value: 1.0
                hue <integer> : Hue (0 - 360). default value: 0
                saturation <float> : Saturation (0.0 - 3.0). default value: 1.0
                gamma <float> : Gamma (0.01 - 10.0). default value: 1.0 
             */

            var video = mediaPlayer?.Video;
            var videoAdjustments = video?.Adjustments;
            if (videoAdjustments != null)
            {
                if (opts == VideoAdjustOptions.Brightness)
                {
                    if (videoAdjustments.Brightness != val)
                    {
                        videoAdjustments.Brightness = val;
                    }
                }
                else if (opts == VideoAdjustOptions.Contrast)
                {
                    if (videoAdjustments.Contrast != val)
                    {
                        videoAdjustments.Contrast = val;
                    }
                }
                else if (opts == VideoAdjustOptions.Gamma)
                {
                    if (videoAdjustments.Gamma != val)
                    {
                        videoAdjustments.Gamma = val;
                    }
                }
                else if (opts == VideoAdjustOptions.Hue)
                {
                    if (videoAdjustments.Hue != val)
                    {
                        videoAdjustments.Hue = val;
                    }
                }
                else if (opts == VideoAdjustOptions.Saturation)
                {
                    if (videoAdjustments.Saturation != val)
                    {
                        videoAdjustments.Saturation = val;
                    }
                }
                else if (opts == VideoAdjustOptions.Enable)
                {
                    bool enabled = (val == 0);
                    if (videoAdjustments.Enabled != enabled)
                    {
                        videoAdjustments.Enabled = enabled;
                    }
                }
            }

        }

        internal void IncrBlurRadius(int delta)
        {
            var val = Session.BlurEffect.Radius;
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

        internal void SetBlurRadius(int blurRadius)
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

                if (Session.BlurEffect.Radius != blurRadius)
                {
                    Session.BlurEffect.Radius = blurRadius;
                }
            });
        }


        private void SetPlayerVolume(int volume)
        {
            if (mediaPlayer != null)
            {
                if (volume < 0)
                {
                    volume = 0;
                }
                if (volume > 100)
                {
                    volume = 100;
                }

                var audio = mediaPlayer.Audio;
                if (audio != null)
                {
                    if (audio.Volume != volume)
                    {
                        audio.Volume = volume;

                    }
                }
            }
        }

        private void SetPlayerPosition(double pos)
        {
            if (mediaPlayer != null)
            {
                if (pos < 0)
                {
                    pos = 0;
                }
                if (pos > 1)
                {
                    pos = 1;
                }

                if (mediaPlayer.IsSeekable)
                {
                    //mediaPlayer.Position = (float)pos;
                    mediaPlayer.Time = (long)(mediaPlayer.Length * pos);
                }
            }
        }

        private Task PostToClientAsync(string command, object[] args = null)
        {
            return Task.Run(() => PostToClient(command, args));
        }

        private void PostToClient(string command, object[] args = null)
        {
            //logger.Debug("PostToClient(...) " + command);
            communicationClient?.OnPostMessage(command, args);
        }

        private void SetupVideo(string appId, IntPtr handle, int width, int height, PixelFormat fmt, int pitches)
        {
            this.dispatcher.Invoke(() =>
            {
                Session.VideoSource = Imaging.CreateBitmapSourceFromMemorySection(handle, width, height, fmt, pitches, 0);

            });

            interopBitmap = Session.VideoSource as InteropBitmap;
            var _fmt = (fmt == PixelFormats.Bgra32) ?
                System.Drawing.Imaging.PixelFormat.Format32bppArgb :
                System.Drawing.Imaging.PixelFormat.Format32bppRgb;

            PostToClient("VideoFormat", new object[] { appId, width, height, (int)_fmt, pitches });
        }


        private void DisplayVideo()
        {
            this.dispatcher.BeginInvoke(DispatcherPriority.Render,
                (Action)(() =>
                {
                    interopBitmap?.Invalidate();

                }));
        }

        private void CleanupVideo()
        {
            PostToClient("CleanupVideo");
            Session.VideoSource = null;
        }



        public class AudioProvider
        {
            private readonly PlaybackHost playback = null;
            public AudioProvider(PlaybackHost playback)
            {
                this.playback = playback;
            }

            public void Setup()
            {
                VlcMediaPlayer player = playback.mediaPlayer;

                var outputs = player?.Audio?.Outputs;
                if (outputs != null)
                {
                    var directsound = outputs.All.FirstOrDefault(o => o.Name == "directsound");
                    if (directsound != null)
                    {
                        outputs.Current = directsound;
                    }
                }
            }
        }

        class VideoProvider : IDisposable
        {

            private readonly PlaybackHost playback = null;

            private MemoryMappedFile memoryMappedFile;
            private MemoryMappedViewAccessor memoryMappedView;

            private EventWaitHandle globalSyncEvent = null;

            private bool isAlphaChannelEnabled = true;

            public VideoProvider(PlaybackHost playback)
            {
                this.playback = playback;
            }

            public void Setup()
            {
                logger.Debug("VlcVideoProvider::Setup(...)");

                var player = playback.mediaPlayer;
                if (player != null)
                {
                    player.SetVideoFormatCallbacks(this.VideoFormat, this.Cleanup);
                    player.SetVideoCallbacks(LockVideo, null, Display, IntPtr.Zero);
                }
                else
                {
                    //TODO
                }
            }


            #region Vlc video callbacks
            /// <summary>
            /// Called by vlc when the video format is needed. This method allocats the picture buffers for vlc and tells it to set the chroma to RV32
            /// </summary>
            /// <param name="userdata">The user data that will be given to the <see cref="LockVideo"/> callback. It contains the pointer to the buffer</param>
            /// <param name="chroma">The chroma</param>
            /// <param name="width">The visible width</param>
            /// <param name="height">The visible height</param>
            /// <param name="pitches">The buffer width</param>
            /// <param name="lines">The buffer height</param>
            /// <returns>The number of buffers allocated</returns>
            private uint VideoFormat(out IntPtr userdata, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
            {
                var appId = Guid.NewGuid().ToString("N");

                logger.Debug("VlcVideoProvider::VideoFormat(...) " + appId);

                PixelFormat pixelFormat = isAlphaChannelEnabled ? PixelFormats.Bgra32 : PixelFormats.Bgr32;

                FourCCConverter.ToFourCC("RV32", chroma);

                pitches = this.GetAlignedDimension((uint)(width * pixelFormat.BitsPerPixel) / 8, 32);
                lines = this.GetAlignedDimension(height, 32);
                var size = pitches * lines;


                logger.Debug("MemoryMappedFile.CreateNew(...) " + appId + " " + size);
                this.memoryMappedFile = MemoryMappedFile.CreateNew(appId, size);
                var handle = this.memoryMappedFile.SafeMemoryMappedFileHandle.DangerousGetHandle();

                this.memoryMappedView = this.memoryMappedFile.CreateViewAccessor();
                var viewHandle = this.memoryMappedView.SafeMemoryMappedViewHandle.DangerousGetHandle();

                userdata = viewHandle;

                globalSyncEvent = CreateEventWaitHandle(appId + "_event");

                playback.SetupVideo(appId, handle, (int)width, (int)height, pixelFormat, (int)pitches);


                return 1;
            }


            /// <summary>
            /// Called by Vlc when it requires a cleanup
            /// </summary>
            /// <param name="userdata">The parameter is not used</param>
            private void Cleanup(ref IntPtr userdata)
            {
                logger.Debug("VlcVideoProvider::CleanupVideo(...)");

                playback.CleanupVideo();

                // This callback may be called by Dispose in the Dispatcher thread, in which case it deadlocks if we call RemoveVideo again in the same thread.
                if (!disposedValue)
                {
                    this.RemoveVideo();
                    //this.dispatcher.Invoke((Action)this.RemoveVideo);
                }
            }

            /// <summary>
            /// Called by libvlc when it wants to acquire a buffer where to write
            /// </summary>
            /// <param name="userdata">The pointer to the buffer (the out parameter of the <see cref="VideoFormat"/> callback)</param>
            /// <param name="planes">The pointer to the planes array. Since only one plane has been allocated, the array has only one value to be allocated.</param>
            /// <returns>The pointer that is passed to the other callbacks as a picture identifier, this is not used</returns>
            private IntPtr LockVideo(IntPtr userdata, IntPtr planes)
            {
                Marshal.WriteIntPtr(planes, userdata);
                return userdata;
            }

            /// <summary>
            /// Called by libvlc when the picture has to be displayed.
            /// </summary>
            /// <param name="userdata">The pointer to the buffer (the out parameter of the <see cref="VideoFormat"/> callback)</param>
            /// <param name="picture">The pointer returned by the <see cref="LockVideo"/> callback. This is not used.</param>
            private void Display(IntPtr userdata, IntPtr picture)
            {
                globalSyncEvent?.Set();

                playback.DisplayVideo();

            }
            #endregion

            /// <summary>
            /// Removes the video (must be called from the Dispatcher thread)
            /// </summary>
            private void RemoveVideo()
            {
                logger.Debug("VlcVideoProvider::RemoveVideo(...)");

                //this.VideoSource = null;

                this.memoryMappedView?.Dispose();
                this.memoryMappedView = null;
                this.memoryMappedFile?.Dispose();
                this.memoryMappedFile = null;

                this.globalSyncEvent?.Dispose();
                this.globalSyncEvent = null;

            }


            private EventWaitHandle CreateEventWaitHandle(string eventId)
            {
                logger.Debug("CreateEventWaitHandle(...) " + eventId);
                EventWaitHandle handle = null;

                var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                var rule = new EventWaitHandleAccessRule(users,
                    EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                                            AccessControlType.Allow);

                var security = new EventWaitHandleSecurity();
                security.AddAccessRule(rule);
                bool created;
                handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventId, out created, security);

                return handle;
            }

            private uint GetAlignedDimension(uint dimension, uint mod)
            {
                var modResult = dimension % mod;
                if (modResult == 0)
                {
                    return dimension;
                }

                return dimension + mod - (dimension % mod);
            }

            #region IDisposable Support
            private bool disposedValue = false;

            /// <summary>
            /// Disposes the control.
            /// </summary>
            /// <param name="disposing">The parameter is not used.</param>
            protected virtual void Dispose(bool disposing)
            {
                logger.Debug("VlcVideoProvider::Dispose(...)");
                if (!disposedValue)
                {
                    disposedValue = true;

                    RemoveVideo();

                    globalSyncEvent?.Dispose();

                    // this.dispatcher.BeginInvoke((Action)this.RemoveVideo);
                }
            }

            /// <summary>
            /// The destructor
            /// </summary>
            ~VideoProvider()
            {
                Dispose(false);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion

        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum PlaybackState
    {
        Created,
        Initializing,
        Initialized,
        Opening,
        Playing,
        Paused,
        Stopped,
        Failed,
        Closing,
        Closed
    }

    public class _BooleanToVisibilityConverter : IValueConverter
    {
        public bool IsInverse { get; set; }
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool visibility = (bool)value;
            if (IsInverse)
            {
                visibility = !visibility;
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            if (visibility == Visibility.Collapsed || visibility == Visibility.Hidden)
            {
                return false;
            }
            else
            {
                return true;
            }
            //throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(PlaybackState), typeof(Visibility))]
    public class MediaStateToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            PlaybackState state = (PlaybackState)value;
            Visibility visibility = (state == PlaybackState.Playing) ? Visibility.Hidden : Visibility.Visible;
            return visibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
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

    static class VlcExtensions
    {
        public static string GetInfoString(this VlcMedia media)
        {
            StringBuilder sb = new StringBuilder();

            if (media != null)
            {
                sb.AppendLine("============= Media Info =============");
                sb.AppendLine(media.Mrl);
                sb.AppendLine("-----------------------");
                sb.AppendLine("Title: " + media.Title);
                sb.AppendLine("Duration: " + media.Duration);
                // media.
                sb.AppendLine("Setting: " + media.Setting);
                sb.AppendLine("-----------------------");
                var tracks = media.Tracks;
                if (tracks != null)
                {
                    foreach (var track in tracks)
                    {
                        sb.AppendLine("Track: " + track.Type + " " + track.Id + " " + track.Bitrate);
                        var codec = FourCCConverter.FromFourCC(track.CodecFourcc);
                        sb.AppendLine("Codec: " + codec);
                        var ti = track.TrackInfo;
                        if (ti != null)
                        {
                            var at = ti as AudioTrack;
                            if (at != null)
                            {
                                sb.AppendLine("Channel: " + at.Channels);
                                sb.AppendLine("Rate: " + at.Rate);

                            }
                            var vt = ti as VideoTrack;
                            if (vt != null)
                            {
                                sb.AppendLine("Width: " + vt.Width);
                                sb.AppendLine("Height: " + vt.Height);
                                var fps = vt.FrameRateNum / (double)vt.FrameRateDen;
                                sb.AppendLine("Fps: " + fps);
                            }

                            var st = ti as SubtitleTrack;
                            if (st != null)
                            {
                                sb.AppendLine("Encoding: " + st.Encoding);
                            }
                        }

                        sb.AppendLine("-----------------------");
                    }
                }
            }

            return sb.ToString();
        }

    }
}
