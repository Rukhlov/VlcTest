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
using VlcContracts;

namespace VlcPlayer
{

    public class PlaybackHost : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Dispatcher dispatcher = null;
        public PlaybackHost()
        {

            this.dispatcher = Dispatcher.CurrentDispatcher;// this.app.Dispatcher;

            Session = new PlaybackSession(Program.CommandLineOptions);


            this.communicationClient = new CommunicationClient(this);
            this.videoProvider = new VideoProvider(this);
            this.audioProvider = new AudioProvider(this);

            this.mrlProvider = new MrlProvider(this);

            Session.PlaybackState = PlaybackState.Created;

        }

        private CommunicationClient communicationClient = null;

        private VideoProvider videoProvider = null;
        private AudioProvider audioProvider = null;

        private VlcMediaPlayer mediaPlayer = null;

        private InteropBitmap interopBitmap = null;

        private bool loopPlayback = false;

        private MrlProvider mrlProvider = null;
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
                        //Environment.Exit(0);
                    });
                }
                return quitCommand;
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
                        EnqueueCommand("Mute", args);
                    });
                }
                return muteCommand;
            }
        }

        public void Setup()
        {
            try
            {
                if (Program.ParentProcess == null)
                {
                    VideoWindow.Show();
                }

                Session.PlaybackState = PlaybackState.Initializing;

                //SetupPlayback();

                playbackThread = new Thread(PlaybackProc);
                playbackThread.IsBackground = true;
                playbackThread.Start();


            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                this.CleanUp();

                throw;
            }

        }

        private void CreatePlayback()
        {
            //var options = new string[] { ":audio-visual=visual", ":effect-list=spectrum" };
            //var options = new string[] { "--video-filter=transform:sepia", "--sepia-intensity=100", "--transform-type=180" };
            // var vlcopts = new string[] { "input-repeat=65535" };
            //var opts = new string[] { "--aout=\"waveout\"" };

            var opts = new string[] { "--extraintf=logger", "--verbose=0", "--network-caching=1000" };
            this.mediaPlayer = CreatePlayer(Program.VlcLibDirectory, opts);

            videoProvider.Setup();
            //audioProvider.Setup();

            if (!string.IsNullOrEmpty(Session.RemoteAddr))
            {

                communicationClient.Setup(Session.RemoteAddr);

                var eventId = videoProvider.eventId;
                var memoId = videoProvider.memoryId;

                communicationClient.Connect(new[] { eventId, memoId });
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

            //player.Log += MediaPlayer_Log;
            player.EncounteredError += MediaPlayer_EncounteredError;

            player.Opening += MediaPlayer_Opening;
            player.Playing += MediaPlayer_Playing;
            player.Paused += MediaPlayer_Paused;
            player.EndReached += MediaPlayer_EndReached;
            player.Stopped += MediaPlayer_Stopped;
            player.MediaChanged += Player_MediaChanged;
            player.PositionChanged += MediaPlayer_PositionChanged;
            //player.TimeChanged += Player_TimeChanged;
            player.LengthChanged += MediaPlayer_LengthChanged;

            player.AudioDevice += MediaPlayer_AudioDevice;
            player.AudioVolume += MediaPlayer_AudioVolume;
            player.Muted += MediaPlayer_Muted;
            player.Unmuted += MediaPlayer_Unmuted; ;
            player.VideoOutChanged += MediaPlayer_VideoOutChanged;

            // player.Buffering += Player_Buffering;
            player.Video.IsKeyInputEnabled = false;
            player.Video.IsMouseInputEnabled = false;

            player.SetUserAgent("Mitsar Vlc Player", "Mitsar Vlc Player");

            return player;

        }

        private void Player_Buffering(object sender, VlcMediaPlayerBufferingEventArgs e)
        {
            logger.Debug("Player_Buffering(...) " + e.NewCache);
        }

        class InternalCommand
        {
            public string command = "";
            public object[] args = null;
        }

        private Thread playbackThread = null;
        private AutoResetEvent syncEvent = new AutoResetEvent(false);
        private volatile bool closing = false;

        // private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private CommandQueue commandQueue = new CommandQueue();
        class CommandQueue
        {
            private readonly LinkedList<InternalCommand> list = new LinkedList<InternalCommand>();

            private readonly Dictionary<string, LinkedListNode<InternalCommand>> dict = new Dictionary<string, LinkedListNode<InternalCommand>>();

            private readonly object locker = new object();

            public InternalCommand Dequeue()
            {
                lock (locker)
                {
                    InternalCommand command = null;
                    if (list.Count > 0)
                    {
                        command = list.First();
                        list.RemoveFirst();

                        var key = command.command;
                        if (dict.ContainsKey(key))
                        {
                            dict.Remove(key);
                        }
                    }
                    return command;
                }
            }

            public void Enqueue(InternalCommand command)
            {
                lock (locker)
                {
                    //if(list.Count> maxCount)
                    //{
                    //    //...
                    //}
                    var key = command.command;
                    if (dict.ContainsKey(key))
                    {
                        var node = dict[key];
                        node.Value = command;
                    }
                    else
                    {
                        LinkedListNode<InternalCommand> node = list.AddLast(command);
                        dict.Add(key, node);
                    }

                }
            }

            public void Clear()
            {
                lock (locker)
                {
                    list.Clear();
                    dict.Clear();
                }
            }
        }

        private object locker = new object();
        private InternalCommand DequeueCommand()
        {
            if (closing)
            {
                return null;
            }

            return commandQueue.Dequeue();
        }

        private void EnqueueCommand(string command, object[] args = null)
        {
            if (closing)
            {
                return;
            }

            commandQueue.Enqueue(new InternalCommand { command = command, args = args });
            syncEvent.Set();
        }


        public event Action<object> Closed;
        private void OnClosed(object obj)
        {
            logger.Debug("OnClosed(...)");
            Closed?.Invoke(obj);
        }

        private int openingTimeout = 10000;
        private long openingTime = 0;
        private void PlaybackProc()
        {
            logger.Trace("PlaybackProc() BEGIN");

            try
            {
                CreatePlayback();

                Session.PlaybackState = PlaybackState.Initialized;

                if (!string.IsNullOrEmpty(Session.MediaAddr))
                {
                    PlayCommand.Execute(Session.MediaAddr);
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (true)
                {
                    try
                    {
                        InternalCommand command = null;
                        do
                        {
                            command = DequeueCommand();
                            if (command != null)
                            {
                                ProcessPlaybackCommand(command);
                            }

                        } while (command != null);

                        ProcessPlaybackState();

                        if (Session.PlaybackState == PlaybackState.Opening)
                        {
                            openingTime += stopwatch.ElapsedMilliseconds;
                        }

                        if (openingTime > openingTimeout)
                        {
                            logger.Warn("!!!!!!!!!!!! openingTime=" + openingTime);

                            EnqueueCommand("Stop");

                            openingTime = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }

                    stopwatch.Restart();
                    syncEvent.WaitOne(300);
                    if (closing)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                CleanUp();
            }

            logger.Trace("PlaybackProc() END");
        }

        private void ProcessPlaybackState()
        {
            if (closing)
            {
                return;
            }

            if (Session.PlaybackState == PlaybackState.Playing ||
                Session.PlaybackState == PlaybackState.Paused)
            {

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
                }
                else
                {
                    //...
                }

                float currentPosition = float.NaN;
                if (mediaPlayer != null)
                {
                    currentPosition = mediaPlayer.Position;
                }

                if (!float.IsNaN(currentPosition))
                {
                    if (Session.Position != currentPosition)
                    {
                        Session.Position = currentPosition;

                        // Session.TotalTime = media.Duration;
                        var msec = Session.TotalTime.TotalMilliseconds * currentPosition;

                        Session.CurrentTime = TimeSpan.FromMilliseconds(msec);

                        InvokeEventAsync("Position", new object[] { Session.Position });
                    }
                }



            }
        }

        private void ProcessPlaybackCommand(InternalCommand command)
        {
           // logger.Debug("ProcessInternalCommand(...)");

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
                        if (Session.PlaybackState == PlaybackState.Opening)
                        {
                            return;
                        }

                        DoPlay(command.args);

                    }
                    break;
                case "PlayMrl":
                    {
                        if (Session.PlaybackState == PlaybackState.Opening)
                        {
                            DoPlayMrl(command.args);
                        }
                    }
                    break;
                case "Opening":
                    {
                        //ResetSession();
                        openingTime = 0;

                        Session.PlaybackState = PlaybackState.Opening;
                        Session.PlaybackStats = new PlaybackStatistics();

                        InvokeEventAsync("Opening");
                    }
                    break;
                case "LengthChanged":
                    {
                        var args = command.args;
                        if (args?.Length > 0)
                        {
                            var msecTotal = (long)args[0];
                            Session.TotalTime = TimeSpan.FromMilliseconds(msecTotal);
                            InvokeEventAsync("LengthChanged", new object[] { msecTotal });
                        }

                    }
                    break;
                case "Playing":
                    {
                        openingTime = 0;
                        Session.PlaybackState = PlaybackState.Playing;

                        var media = mediaPlayer?.GetMedia();
                        var mediaStr = media?.GetInfoString();
                        logger.Info(mediaStr);

                        Session.Mrl = media.Mrl;

                        //Session.PlaybackStats = new PlaybackStatistics();

                        InvokeEventAsync("Playing", new object[] { });
                    }
                    break;
                case "Pause":
                    {
                        if (Session.PlaybackState == PlaybackState.Paused ||
                            Session.PlaybackState == PlaybackState.Playing)
                        {
                            DoPause();
                        }
                    }
                    break;
                case "Paused":
                    {
                        Session.PlaybackState = PlaybackState.Paused;
                        InvokeEventAsync("Paused");
                    }
                    break;
                case "Stop":
                    {
                        commandQueue.Clear();
                        DoStop();
                    }
                    break;
                case "MediaChanged":
                    {
                        var media = mediaPlayer?.GetMedia();
                        if (media != null)
                        {
                            Session.Mrl = media.Mrl;

                            //Session.Position = 0;
                            //Session.CurrentTime = TimeSpan.Zero;
                            //Session.TotalTime = TimeSpan.Zero;//media.Duration;

                            //logger.Info(media.GetInfoString());
                        }
                    }
                    break;
                case "EncounteredError":
                    {
                        //Session.PlaybackState = PlaybackState.Failed;
                        logger.Error("EncounteredError");
                    }
                    break;
                case "Stopped":
                    {
                        // if (!cancellationTokenSource.IsCancellationRequested)
                        if (!stopping)
                        {
                            if (loopPlayback)
                            {
                                var media = mediaPlayer?.GetMedia();
                                if (media != null && media.State == MediaStates.Ended)
                                {
                                    if (ValidateMedia(media))
                                    {
                                        EnqueueCommand("Play");
                                        InvokeEventAsync("Restarting");
                                        break;
                                    }
                                }
                            }
                        }
                        stopping = false;
                        Session.PlaybackState = PlaybackState.Stopped;
                        ResetSession();

                        InvokeEventAsync("Stopped");

                    }
                    break;
                case "Position":
                    {
                        if (Session.PlaybackState == PlaybackState.Playing || Session.PlaybackState == PlaybackState.Paused)
                        {
                            SetPosition(command.args);
                        }
                    }
                    break;
                case "SetAdjustments":
                    {
                        SetVideoAdjustment(command.args);
                    }
                    break;

                case "Mute":
                    {
                        SetMute(command.args);
                    }
                    break;
                case "Muted":
                    {
                        Session.IsMute = true;
                        InvokeEventAsync("Muted");
                    }
                    break;
                case "Unmuted":
                    {
                        Session.IsMute = false;
                        InvokeEventAsync("Unmuted");
                    }
                    break;
                case "Volume":
                    {
                        SetVolume(command.args);
                    }
                    break;
                case "AudioVolume":
                    {
                        Session.Volume = (int)mediaPlayer?.Audio?.Volume;

                        //PostToClientAsync("AudioVolume", new object[] { Session.Volume });
                    }
                    break;
                default:
                    {

                    }
                    break;

            }

        }

        private void DoPlay(object[] args)
        {
            logger.Debug("DoPlay(...)");
            string mediaLink = "";
            if (args?.Length > 0)
            {
                mediaLink = (string)args[0];
            }

            if (!string.IsNullOrEmpty(mediaLink))
            {
                Session.PlaybackState = PlaybackState.Opening;

                Task.Run(() =>
                {
                    mrlProvider.FetchMrl(mediaLink);

                });
            }
            else
            {
                var media = mediaPlayer?.GetMedia();
                if (media != null)
                {
                    DoPlayMrl(new[] { media.Mrl });
                }
            }
        }

        private void DoPlayMrl(object[] args)
        {
            logger.Debug("DoPlayMrl(...)");
            string mrl = "";
            if (args?.Length > 0)
            {
                mrl = args[0]?.ToString();
            }

            if (!string.IsNullOrEmpty(mrl))
            {
                //logger.Info(mrl);
                mediaPlayer?.Play(mrl);
            }
        }

        private void DoPause()
        {
            logger.Debug("DoPause(...)");

            if (mediaPlayer != null)
            {
                if (mediaPlayer.IsPausable())
                {
                    mediaPlayer.Pause();
                }
            }
        }
        private bool stopping = false;
        private void DoStop()
        {
            logger.Debug("DoStop(...)");

            // cancellationTokenSource?.Cancel();
            stopping = true;
            mrlProvider?.Reset();
            mediaPlayer?.Stop();
        }

        private bool ValidateMedia(VlcMedia media)
        {
            bool isValidMedia = false;
            if (media != null)
            {
                var duration = media.Duration;

                isValidMedia = (duration != TimeSpan.Zero && duration < TimeSpan.MaxValue && duration > TimeSpan.MinValue);
                isValidMedia &= media.Tracks.Any(t => t.Type == MediaTrackTypes.Audio || t.Type == MediaTrackTypes.Video);
            }
            return isValidMedia;
        }


        private void SetVolume(object[] args)
        {
            var arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            int volume = 0;
            if (int.TryParse(arg0, out volume))
            {
                SetPlayerVolume(volume);
            }
        }

        private void SetPosition(object[] args)
        {
            var arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            double pos = 0;
            if (double.TryParse(arg0, out pos))
            {
                SetPlayerPosition(pos);
            }
        }

        private void SetMute(object[] args)
        {
            var arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            if (!string.IsNullOrEmpty(arg0))
            {
                var mute = bool.Parse(arg0);
                var audio = mediaPlayer?.Audio;
                if (audio != null)
                {
                    audio.IsMute = mute;
                }
            }
            else
            {
                mediaPlayer?.Audio?.ToggleMute();
            }
        }

        private void SetVideoAdjustment(object[] args)
        {
            var arg0 = "";
            var arg1 = "";
            if (args?.Length > 1)
            {
                arg0 = args[0]?.ToString();
                arg1 = args[1]?.ToString();
            }

            var video = mediaPlayer?.Video;
            var videoAdjustments = video?.Adjustments;

            if (videoAdjustments != null)
            {
                /*
                 *  contrast <float> : Contrast (0.0 - 2.0). default value: 1.0
                    brightness <float> : Brightness (0.0 - 2.0). default value: 1.0
                    hue <integer> : Hue (0 - 360). default value: 0
                    saturation <float> : Saturation (0.0 - 3.0). default value: 1.0
                    gamma <float> : Gamma (0.01 - 10.0). default value: 1.0 
                 */

                if (arg0 == "Enable")
                {
                    float _enabled = 0;
                    if (float.TryParse(arg1, out _enabled))
                    {
                        bool enabled = (_enabled != 0);

                        //if (videoAdjustments.Enabled != enabled)// libvlc_video_get_adjust_int() always return 0
                        {
                            videoAdjustments.Enabled = enabled;
                        }
                    }

                }
                else if (arg0 == "Contrast")
                {
                    float contrast = 0;
                    if (float.TryParse(arg1, out contrast))
                    {
                        if (videoAdjustments.Contrast != contrast)
                        {
                            videoAdjustments.Contrast = contrast;
                        }
                    }
                }
                else if (arg0 == "Brightness")
                {
                    float brightness = 0;
                    if (float.TryParse(arg1, out brightness))
                    {
                        if (videoAdjustments.Brightness != brightness)
                        {
                            videoAdjustments.Brightness = brightness;
                        }
                    }
                }
                else if (arg0 == "Gamma")
                {
                    float gamma = 0;
                    if (float.TryParse(arg1, out gamma))
                    {
                        if (videoAdjustments.Gamma != gamma)
                        {
                            videoAdjustments.Gamma = gamma;
                        }
                    }
                }
                else if (arg0 == "Hue")
                {
                    float hue = 0;
                    if (float.TryParse(arg1, out hue))
                    {
                        if (videoAdjustments.Hue != hue)
                        {
                            videoAdjustments.Hue = hue;
                        }
                    }
                }
                else if (arg0 == "Saturation")
                {
                    float saturation = 0;
                    if (float.TryParse(arg1, out saturation))
                    {
                        if (videoAdjustments.Saturation != saturation)
                        {
                            videoAdjustments.Saturation = saturation;
                        }
                    }
                }
            }
        }


        private void MediaPlayer_Log(object sender, VlcMediaPlayerLogEventArgs e)
        {
            string message = $"libVlc : {e.Level} {e.Message} @ {e.Module}";
            logger.Debug(e.Message);
        }

        private void MediaPlayer_EncounteredError(object sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            logger.Debug(">>MediaPlayer_EncounteredError(...) ");

            EnqueueCommand("EncounteredError");
        }

        private void MediaPlayer_Opening(object sender, VlcMediaPlayerOpeningEventArgs e)
        {
            logger.Debug(">>MediaPlayer_Opening(...) ");

            EnqueueCommand("Opening");

            //Session.PlaybackState = PlaybackState.Opening;
            //PostMessage("Opening");

        }

        private void MediaPlayer_VideoOutChanged(object sender, VlcMediaPlayerVideoOutChangedEventArgs e)
        {
            logger.Debug(">>MediaPlayer_VideoOutChanged(...) " + e.NewCount);
        }

        private void MediaPlayer_AudioDevice(object sender, VlcMediaPlayerAudioDeviceEventArgs e)
        {
            logger.Debug(">>MediaPlayer_AudioDevice(...) " + e.Device);

        }

        private void MediaPlayer_Playing(object sender, VlcMediaPlayerPlayingEventArgs e)
        {
            logger.Debug(">>MediaPlayer_Playing(...)");

            EnqueueCommand("Playing");

        }

        private void MediaPlayer_Paused(object sender, VlcMediaPlayerPausedEventArgs e)
        {
            logger.Debug(">>MediaPlayer_Paused(...)");

            EnqueueCommand("Paused");
        }

        private void MediaPlayer_EndReached(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            logger.Debug(">>Player_EndReached(...)");

            //EnqueueCommand("EndReached");
        }

        private void MediaPlayer_Stopped(object sender, VlcMediaPlayerStoppedEventArgs e)
        {
            logger.Debug(">>MediaPlayer_Stopped(...) " + mediaPlayer.State);

            EnqueueCommand("Stopped");
        }

        private void Player_MediaChanged(object sender, VlcMediaPlayerMediaChangedEventArgs e)
        {
            logger.Debug(">>Player_MediaChanged(...)");

            //EnqueueCommand("MediaChanged", new object[] { });
        }



        private void MediaPlayer_PositionChanged(object sender, VlcMediaPlayerPositionChangedEventArgs e)
        {
            logger.Debug(">>MediaPlayer_PositionChanged(...) " + e.NewPosition);
            //EnqueueCommand("PositionChanged", new object[] { e.NewPosition });
        }

        //private void Player_TimeChanged(object sender, VlcMediaPlayerTimeChangedEventArgs e)
        //{
        //    logger.Debug("Player_TimeChanged(...) " + e.NewTime);
        //}

        private void MediaPlayer_LengthChanged(object sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            logger.Debug(">>MediaPlayer_LengthChanged(...)");
            EnqueueCommand("LengthChanged", new object[] { e.NewLength });

        }

        private void MediaPlayer_Muted(object sender, EventArgs e)
        {
            logger.Debug(">>MediaPlayer_Muted(...)");

            EnqueueCommand("Muted");
        }

        private void MediaPlayer_Unmuted(object sender, EventArgs e)
        {
            logger.Debug(">>MediaPlayer_Unmuted(...)");

            EnqueueCommand("Unmuted");

        }

        private void MediaPlayer_AudioVolume(object sender, VlcMediaPlayerAudioVolumeEventArgs e)
        {
            logger.Debug(">>MediaPlayer_AudioVolume(...) ");

            EnqueueCommand("AudioVolume");

        }

        public void Close()
        {
            logger.Debug("PlaybackHost::Close(...)");

            Task.Run(() =>
            {
                try
                {
                    closing = true;
                    syncEvent.Set();

                    if (!playbackThread.Join(3000))
                    {
                        Debug.Fail("!playbackThread.Join(3000)");
                    }

                    //if (!playbackThread.Join(3000))
                    //{
                    //    //playbackThread.Interrupt();
                    //    //playbackThread.Abort();
                    //    //playbackThread = null;
                    //}
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
                finally
                {
                    OnClosed(dispatcher);
                }
            });


        }
        private void ResetSession()
        {
            openingTime = 0;

            Session.Reset();
            commandQueue.Clear();
        }

        public void CleanUp()
        {
            //Thread.Sleep(1000000);
            //throw new Exception("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! CleanUp()");
            logger.Debug("CleanUp(...)");
            communicationClient?.Close();

            if (mediaPlayer != null)
            {
                mediaPlayer.EncounteredError -= MediaPlayer_EncounteredError;
                mediaPlayer.Opening -= MediaPlayer_Opening;
                mediaPlayer.Playing -= MediaPlayer_Playing;
                mediaPlayer.Paused -= MediaPlayer_Paused;
                mediaPlayer.EndReached -= MediaPlayer_EndReached;
                mediaPlayer.Stopped -= MediaPlayer_Stopped;
                mediaPlayer.PositionChanged -= MediaPlayer_PositionChanged;
                mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                mediaPlayer.AudioDevice -= MediaPlayer_AudioDevice;
                mediaPlayer.AudioVolume -= MediaPlayer_AudioVolume;
                mediaPlayer.Muted -= MediaPlayer_Muted;
                mediaPlayer.Unmuted -= MediaPlayer_Unmuted; ;
                mediaPlayer.VideoOutChanged -= MediaPlayer_VideoOutChanged;

                mediaPlayer.Dispose();
                mediaPlayer = null;
            }

            videoProvider?.Dispose();
            mrlProvider?.Dispose();

            // cancellationTokenSource?.Dispose();
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
                    arg0 = Session.MediaAddr;
                }
                else
                {
                    Session.MediaAddr = arg0;
                }

                PlayCommand.Execute(arg0);
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
                MuteCommand.Execute(arg0);
                // EnqueueCommand("Mute" , new[] { val0 });
            }
            else if (command == "Position")
            {
                EnqueueCommand("Position", new[] { arg0 });
            }
            else if (command == "Volume")
            {
                EnqueueCommand("Volume", new[] { arg0 });
            }
            else if (command == "SetAdjustments")
            {
                EnqueueCommand("SetAdjustments", args);
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
                    SetVisibleState(visible);

                }
            }
            else if (command == "SwitchLoopPlayback")
            {
                bool _loopPlayback = false;
                if (bool.TryParse(arg0, out _loopPlayback))
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

        private void SetVisibleState(bool visible)
        {
            this.dispatcher.Invoke(() =>
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

        private void InvokeEventAsync(string command, object[] args = null)
        {
            InvokeEvent(command, args);
            //return Task.Run(() => InvokeEvent(command, args));
        }

        private void InvokeEvent(string command, object[] args = null)
        {
            //logger.Debug("PostToClient(...) " + command);
            communicationClient?.OnPostMessage(command, args);
        }

        private void SetupVideo( IntPtr handle, int width, int height, PixelFormat fmt, int pitches, int offset)
        {
            this.dispatcher.Invoke(() =>
            {
                Session.VideoSource = Imaging.CreateBitmapSourceFromMemorySection(handle, width, height, fmt, pitches, offset);

            });

            interopBitmap = Session.VideoSource as InteropBitmap;

            //var _fmt = (fmt == PixelFormats.Bgra32) ?
            //    System.Drawing.Imaging.PixelFormat.Format32bppArgb :
            //    System.Drawing.Imaging.PixelFormat.Format32bppRgb;

            //InvokeEventAsync("VideoFormat", new object[] { AppId, width, height, (int)_fmt, pitches });
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
            InvokeEventAsync("CleanupVideo");
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

            private EventWaitHandle eventWaitHandle = null;

            private bool isAlphaChannelEnabled = true;

            public VideoProvider(PlaybackHost playback)
            {
                this.playback = playback;
            }

            internal string eventId = "";
            internal string memoryId = "";

            public void Setup()
            {
                logger.Debug("VlcVideoProvider::Setup(...)");

  
                var player = playback.mediaPlayer;
                if (player != null)
                {
                    var handle =(IntPtr)Program.CommandLineOptions.WindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        player.VideoHostControlHandle = handle;


                        logger.Info("player.VideoHostControlHandle " + player.VideoHostControlHandle);
                    }
                    else
                    {
                        memoryId = Guid.NewGuid().ToString("N");
                        this.memoryMappedFile = MemoryMappedFile.CreateNew(memoryId, 30 *1024* 1024, MemoryMappedFileAccess.ReadWrite);
                        
                        eventId = Guid.NewGuid().ToString("N");
                        this.eventWaitHandle = CreateEventWaitHandle(eventId);

                        player.SetVideoFormatCallbacks(this.VideoFormat, this.Cleanup);
                        player.SetVideoCallbacks(LockVideo, null, Display, IntPtr.Zero);
                    }
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
            private unsafe uint VideoFormat(out IntPtr userdata, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
            {  
                logger.Debug("VlcVideoProvider::VideoFormat(...) " + eventId + " chroma " + chroma + " width " + width + " height " + height + " pitches " + pitches + " lines " + lines);

                PixelFormat pixelFormat = isAlphaChannelEnabled ? PixelFormats.Bgra32 : PixelFormats.Bgr32;

                // FourCCConverter.ToFourCC("BGRA", chroma);
                FourCCConverter.ToFourCC("RV32", chroma);

                pitches = this.GetAlignedDimension((uint)(width * pixelFormat.BitsPerPixel) / 8, 32);
                lines = this.GetAlignedDimension(height, 32);

                //var size = pitches * lines;

              
                int headerSize = 1024;//args.Length * sizeof(int);
                long dataSize = pitches * lines;
                var size = headerSize + dataSize;
                int offset = 0;

                this.memoryMappedView = this.memoryMappedFile.CreateViewAccessor();
                if (memoryMappedView.Capacity < size)
                {
                    width = 1280;
                    height = 720;

                    pitches = this.GetAlignedDimension((uint)(width * pixelFormat.BitsPerPixel) / 8, 32);
                    lines = this.GetAlignedDimension(height, 32);
                    dataSize = pitches * lines;
                    size = headerSize + dataSize;

                    //this.memoryMappedView?.Dispose();
                    //this.memoryMappedFile?.Dispose();

                    //this.memoryMappedFile = MemoryMappedFile.CreateNew(playback.AppId, size);
                    //this.memoryMappedView = this.memoryMappedFile.CreateViewAccessor();
                }

                var args = new int[] { (int)width, (int)height, isAlphaChannelEnabled ? 1 : 0, (int)pitches };
                memoryMappedView.WriteArray(offset, args, 0, args.Length);
                offset += headerSize;
                IntPtr ptr = memoryMappedView.SafeMemoryMappedViewHandle.DangerousGetHandle();
                userdata = IntPtr.Add(ptr, offset);

                //byte* ptr = (byte*)0;
                //this.memoryMappedView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                //userdata = IntPtr.Add(new IntPtr(ptr), (int)offset);

                var handle = this.memoryMappedFile.SafeMemoryMappedFileHandle.DangerousGetHandle();
                playback.SetupVideo(handle, (int)width, (int)height, pixelFormat, (int)pitches, (int)offset);
                initiated = true;
                return 1;
            }

            /// <summary>
            /// Called by Vlc when it requires a cleanup
            /// </summary>
            /// <param name="userdata">The parameter is not used</param>
            private void Cleanup(ref IntPtr userdata)
            {
                logger.Debug("VlcVideoProvider::CleanupVideo(...)");

                var args = new int[] { 0, 0, 0, 0 };
                memoryMappedView?.WriteArray(0, args, 0, args.Length);

                //using (var handle = memoryMappedView.SafeMemoryMappedViewHandle)
                //{
                //    NativeMethods.ZeroMemory(handle.DangerousGetHandle(), handle.ByteLength);
                //}

                if (!disposedValue)
                {
                    this.RemoveVideo();
                }

                playback.CleanupVideo();
                initiated = false;

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

            private bool initiated = false;
            /// <summary>
            /// Called by libvlc when the picture has to be displayed.
            /// </summary>
            /// <param name="userdata">The pointer to the buffer (the out parameter of the <see cref="VideoFormat"/> callback)</param>
            /// <param name="picture">The pointer returned by the <see cref="LockVideo"/> callback. This is not used.</param>
            private void Display(IntPtr userdata, IntPtr picture)
            {
                if (initiated)
                {
                    initiated = false;
                    playback.InvokeEventAsync("Initiated");
                }

                eventWaitHandle?.Set();

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

                //this.memoryMappedFile?.Dispose();
                //this.memoryMappedFile = null;

                //this.globalSyncEvent?.Dispose();
                //this.globalSyncEvent = null;

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

                    this.memoryMappedFile?.Dispose();
                    this.memoryMappedFile = null;

                    eventWaitHandle?.Dispose();

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


        class MrlProvider
        {
            private readonly PlaybackHost playback = null;
            internal MrlProvider(PlaybackHost host)
            {
                this.playback = host;
            }

            private CancellationTokenSource cancellationTokenSource = null;

            internal async void FetchMrl(string mediaLink)
            {
                logger.Debug("FetchMrl(...) " + mediaLink);

                if (playback.closing)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(mediaLink))
                {
                    string mrl = "";
                    try
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                        mrl = await GetMrlAsync(mediaLink, cancellationTokenSource.Token);

                        playback.EnqueueCommand("PlayMrl", new[] { mrl, mediaLink });
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException)
                        {
                            logger.Warn(ex);
                        }
                        else
                        {
                            logger.Error(ex);
                        }

                        playback.EnqueueCommand("Stopped");
                    }
                    finally
                    {
                        cancellationTokenSource?.Dispose();
                        cancellationTokenSource = null;
                    }
                }
            }

            private async Task<string> GetMrlAsync(string mediaLink, CancellationToken cancellationToken)
            {
                var tcs = new TaskCompletionSource<string>();
                cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled();
                });

                var getMrlTask = GetMrlAsync(mediaLink);
                var completedTask = await Task.WhenAny(getMrlTask, tcs.Task);
                if (completedTask == getMrlTask)
                {
                    var result = await getMrlTask;
                    tcs.TrySetResult(result);
                }
                return await tcs.Task;
            }

            private async Task<string> GetMrlAsync(string mediaLink)
            {
                logger.Debug("GetMrlAsync(...) " + mediaLink);

                string mri = "";
                string videoId = "";
                if (YoutubeApi.YoutubeClient.TryParseVideoId(mediaLink, out videoId))
                {
                    using (var youtube = new YoutubeApi.YoutubeClient())
                    {
                        YoutubeApi.MediaStreamInfo streamInfo = null;
                        var streamInfoSet = await youtube.GetVideoMediaStreamInfosAsync(videoId);
                        if (streamInfoSet != null)
                        {
                            if (streamInfoSet.Muxed?.Count > 0)
                            {
                                streamInfo = streamInfoSet.Muxed.OrderByDescending(s => s.VideoQuality).FirstOrDefault();
                            }
                            else if (streamInfoSet.Video?.Count > 0)
                            {
                                streamInfo = streamInfoSet.Video.OrderByDescending(s => s.VideoQuality).FirstOrDefault();
                            }
                            else if (streamInfoSet.Audio?.Count > 0)
                            {
                                streamInfo = streamInfoSet.Audio.OrderByDescending(s => s.Bitrate).FirstOrDefault();
                            }
                        }

                        if (streamInfo != null)
                        {
                            mri = streamInfo.Url;
                        }
                        else
                        {
                            throw new Exception("Unsupported youtube link!");
                        }
                    }
                }
                else
                {
                    Uri u = new Uri(mediaLink);

                    mri = u.AbsoluteUri;
                }
                return mri;
            }

            internal void Reset()
            {
                cancellationTokenSource?.Cancel();
            }

            internal void Dispose()
            {
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }
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
