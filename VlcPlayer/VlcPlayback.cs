﻿using Microsoft.Win32;
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

    public class VlcPlayback : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public VlcPlayback()
        {
           // this.communicationClient = new CommunicationClient(this);
           // this.videoGrabber = new VideoFrameGrabber(this);

            this.mrlProvider = new MrlProvider(this);
            this.state = PlaybackState.Created;
        }

        private VlcMediaPlayer mediaPlayer = null;
        private VideoFrameGrabber videoGrabber = null;

        private MrlProvider mrlProvider = null;

        private VideoSourceProvider videoSourceProvider = null;

        private PlaybackState state;
        public PlaybackState State
        {
            get { return state; }
            private set
            {
                state = value;
                OnPropertyChanged(nameof(State));
            }
        }

        private string mrl = "";
        public string Mrl
        {
            get { return mrl; }
            private set
            {
                mrl = value;
                OnPropertyChanged(nameof(Mrl));
            }
        }

        private string mediaAddr = "";
        public string MediaAddr
        {
            get { return mediaAddr; }
            set
            {
                mediaAddr = value;
                OnPropertyChanged(nameof(MediaAddr));
            }
        }

        private float position = 0;
        public float Position
        {
            get { return position; }
            private set
            {
                position = value;
                OnPropertyChanged(nameof(Position));
            }
        }


        private TimeSpan currentTime = TimeSpan.MinValue;
        public TimeSpan CurrentTime
        {
            get { return currentTime; }
            private set
            {
                currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        private TimeSpan totalTime = TimeSpan.MinValue;
        public TimeSpan TotalTime
        {
            get { return totalTime; }
            private set
            {
                totalTime = value;
                OnPropertyChanged(nameof(TotalTime));
            }
        }


        private int volume = 0;
        public int Volume
        {
            //get
            //{
            //    return mediaPlayer?.Audio?.Volume ?? 0;
            //}

            get { return volume; }
            private set
            {
                volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }

        private bool isMute = false;
        public bool IsMute
        {
            //get
            //{
            //    return mediaPlayer?.Audio?.IsMute ?? false;
            //}

            get { return isMute; }
            private set
            {
                if (isMute != value)
                {
                    isMute = value;
                    OnPropertyChanged(nameof(IsMute));
                }
            }
        }

        private bool loopPlayback = false;
        public bool LoopPlayback
        {
            get { return loopPlayback; }
            set
            {
                if (loopPlayback != value)
                {
                    loopPlayback = value;
                    OnPropertyChanged(nameof(LoopPlayback));
                }
            }
        }

        private PlaybackStatistics playbackStats = new PlaybackStatistics();
        public PlaybackStatistics PlaybackStats
        {
            get { return playbackStats; }
            private set
            {
                playbackStats = value;
                OnPropertyChanged(nameof(PlaybackStats));
            }
        }


        public void Play(string mediaAddr = "")
        {
            if (string.IsNullOrEmpty(mediaAddr))
            {
                mediaAddr = this.MediaAddr;
            }
            EnqueueCommand("Play", new[] { mediaAddr });
        }

        public void Pause()
        {
            EnqueueCommand("Pause");
        }

        public void Stop()
        {
            EnqueueCommand("Stop");
        }

        public void SetMute(bool mute)
        {
            EnqueueCommand("Mute", new object[] { mute });
        }

        public void SetVolume(int vol)
        {
            EnqueueCommand("Volume", new object[] { vol });
        }

        public void Start(VideoSourceProvider provider)
        {
            try
            {
                this.State = PlaybackState.Initializing;

                this.videoSourceProvider = provider;

                // throw new Exception("Setup()");

                playbackThread = new Thread(PlaybackProc);
                playbackThread.IsBackground = true;
                playbackThread.Start();

                //throw new Exception(" playbackThread.Start();");


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

            // throw new Exception("CreatePlayback");

            var opts = new string[] { "--extraintf=logger", "--verbose=0", "--network-caching=5000" };
            this.mediaPlayer = CreatePlayer(Program.VlcLibDirectory, opts);

            var handle = videoSourceProvider?.WindowHandle ?? IntPtr.Zero;

            if (handle != IntPtr.Zero)
            {
                mediaPlayer.VideoHostControlHandle = handle;

                logger.Info("mediaPlayer.VideoHostControlHandle " + mediaPlayer.VideoHostControlHandle);
            }
            else
            {
                //videoGrabber = new VideoFrameGrabber(this);
                //videoGrabber.Setup();
            }
                

            { // Setup audio
                var outputs = mediaPlayer?.Audio?.Outputs;
                if (outputs != null)
                {
                    var directsound = outputs.All.FirstOrDefault(o => o.Name == "directsound");
                    if (directsound != null)
                    {
                        outputs.Current = directsound;
                    }
                }
            }

            /*
            if (!string.IsNullOrEmpty(Session.RemoteAddr))
            {

                communicationClient.Setup(Session.RemoteAddr);

                var eventId = videoGrabber?.eventId;
                var memoId = videoGrabber?.memoryId;

                var options = communicationClient.Connect(new[] { eventId, memoId });
                if (options != null)
                {
                    //logger.Debug(options.Volume);

                    Session.Volume = options.Volume;
                    Session.IsMute = options.IsMute;
                    Session.SetBlurRadius(options.BlurRadius);
                    loopPlayback = options.LoopPlayback;

                }
            }
            */
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

            player.SetUserAgent("Mitsar Player", "Mitsar Player");

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
                // System.Windows.Threading.Dispatcher.Run();

                //throw new Exception(" playbackThread.Start();");

                CreatePlayback();

                State = PlaybackState.Initialized;

                if (!string.IsNullOrEmpty(this.MediaAddr))
                {
                    Play(this.MediaAddr);
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

                        if (State == PlaybackState.Opening)
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
                //Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                logger.Error(ex);

                // Environment.Exit(-100500);

                //throw;
            }
            finally
            {
                CleanUp();

                State = PlaybackState.Closed;

                //dispatcher.InvokeShutdown();
            }

            logger.Trace("PlaybackProc() END");
        }



        private void ProcessPlaybackState()
        {
            if (closing)
            {
                return;
            }

            if (State == PlaybackState.Playing ||
                State == PlaybackState.Paused)
            {

                var media = mediaPlayer?.GetMedia();
                if (media != null)
                {

                    var stats = media.Statistics;

                    var playbackStats = this.PlaybackStats;
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

                    //var audio = mediaPlayer.Audio;
                    //if (audio != null)
                    //{
                    //    if(audio.Volume != Session.Volume)
                    //    {
                    //        audio.Volume = Session.Volume;
                    //    }
                    //    if (audio.IsMute != Session.IsMute)
                    //    {
                    //        audio.IsMute = Session.IsMute;
                    //    }
                    //}

                }

                if (!float.IsNaN(currentPosition))
                {
                    if (this.Position != currentPosition)
                    {
                        this.Position = currentPosition;

                        // Session.TotalTime = media.Duration;
                        var msec = this.TotalTime.TotalMilliseconds * currentPosition;

                        this.CurrentTime = TimeSpan.FromMilliseconds(msec);

                        OnPlaybackChanged("Position", new object[] { this.Position });
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

            //logger.Debug("command " + command.command);

            switch (command.command)
            {
                case "Play":
                    {
                        if (State == PlaybackState.Opening)
                        {
                            return;
                        }

                        DoPlay(command.args);

                    }
                    break;
                case "PlayMrl":
                    {
                        if (State == PlaybackState.Opening)
                        {
                            DoPlayMrl(command.args);
                        }
                    }
                    break;
                case "Opening":
                    {
                        //ResetSession();
                        openingTime = 0;

                        State = PlaybackState.Opening;
                        this.PlaybackStats = new PlaybackStatistics();

                        OnPlaybackChanged("Opening");
                    }
                    break;
                case "LengthChanged":
                    {
                        var args = command.args;
                        if (args?.Length > 0)
                        {
                            var msecTotal = (long)args[0];
                            this.TotalTime = TimeSpan.FromMilliseconds(msecTotal);
                            OnPlaybackChanged("LengthChanged", new object[] { msecTotal });
                        }

                    }
                    break;
                case "Playing":
                    {
                        openingTime = 0;
                        State = PlaybackState.Playing;

                        var media = mediaPlayer?.GetMedia();
                        var mediaStr = media?.GetInfoString();
                        logger.Info(mediaStr);

                        this.Mrl = media.Mrl;

                        //mediaPlayer.Audio.Volume = Session.Volume;
                        //mediaPlayer.Audio.IsMute = Session.IsMute;


                        //Session.PlaybackStats = new PlaybackStatistics();

                        OnPlaybackChanged("Playing", new object[] { });
                    }
                    break;
                case "Pause":
                    {
                        if (State == PlaybackState.Paused ||
                            State == PlaybackState.Playing)
                        {
                            DoPause();
                        }
                    }
                    break;
                case "Paused":
                    {
                        State = PlaybackState.Paused;
                        OnPlaybackChanged("Paused");
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
                            this.Mrl = media.Mrl;

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
                                        OnPlaybackChanged("Restarting");
                                        break;
                                    }
                                }
                            }
                        }
                        stopping = false;
                        State = PlaybackState.Stopped;
                        ResetSession();

                        OnPlaybackChanged("Stopped");

                    }
                    break;
                case "Position":
                    {
                        if (State == PlaybackState.Playing ||
                            State == PlaybackState.Paused)
                        {
                            SetPlayerPosition(command.args);


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
                        SetPlayerMute(command.args);
                    }
                    break;
                case "Muted":
                    {
                        //this.IsMute = true;
                        OnPlaybackChanged("Muted");
                    }
                    break;
                case "Unmuted":
                    {
                        //this.IsMute = false;
                        OnPlaybackChanged("Unmuted");
                    }
                    break;
                case "Volume":
                    {

                        SetVolume(command.args);

                    }
                    break;
                case "NextFrame":
                    {
                        mediaPlayer.NextFrame();
                    }
                    break;
                case "SetRate":
                    {
                        SetPlayerRate(command.args);
                    }
                    break;
                case "AudioVolume":
                    {

                        //Session.Volume = (int)mediaPlayer?.Audio?.Volume;

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
                State = PlaybackState.Opening;

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

        private void SetPlayerVolume(int volume)
        {
            if (mediaPlayer != null)
            {
                int incr = 0;
                if (volume == -101)
                {
                    incr = -1;
                }
                else if (volume == 101)
                {
                    incr = 1;
                }
                else if (volume < 0)
                {
                    volume = 0;
                }
                else if (volume > 100)
                {
                    volume = 100;
                }

                if (State == PlaybackState.Playing ||
                        State == PlaybackState.Paused)
                {
                    var audio = mediaPlayer.Audio;
                    if (audio != null)
                    {
                        if (incr == 0)
                        {
                            if (audio.Volume != volume)
                            {
                                logger.Debug("SetPlayerVolume(...) " + volume + " " + audio.Volume);
                                audio.Volume = volume;

                            }
                        }
                        else
                        {
                            logger.Debug("SetPlayerVolume(...) " + volume + " " + incr);
                            audio.Volume += incr;

                        }
                        volume = audio.Volume;
                    }
                }

                this.Volume = volume;
            }
        }

        private void SetPlayerMute(object[] args)
        {
            var arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }
            bool playing = (State == PlaybackState.Playing || State == PlaybackState.Paused);

            if (!string.IsNullOrEmpty(arg0))
            {
                var mute = bool.Parse(arg0);
                if (playing)
                {
                    var audio = mediaPlayer?.Audio;
                    if (audio != null)
                    {
                        audio.IsMute = mute;
                    }
                }
                else
                {
                    this.IsMute = mute;
                }
            }
            else
            {
                if (playing)
                {
                    mediaPlayer?.Audio?.ToggleMute();
                }
                else
                {
                    this.IsMute = !this.IsMute;
                }
            }

        }

        private void SetPlayerRate(object[] args)
        {
            var arg0 = "";
            if (args?.Length > 0)
            {
                arg0 = args[0]?.ToString();
            }

            float rate = 0;
            if (float.TryParse(arg0, out rate))
            {
                logger.Debug("SetRate() " + rate);

                if (rate > 0 && rate <= 8)
                {
                    mediaPlayer.Rate = rate;
                }
            }
        }

        private void SetPlayerPosition(object[] args)
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
            logger.Debug(">>MediaPlayer_AudioVolume(...) " + mediaPlayer?.Audio?.Volume);

            EnqueueCommand("AudioVolume");

        }


        public void Close()
        {
            logger.Debug("PlaybackHost::Close(...)");

            closing = true;
            syncEvent.Set();

            if (playbackThread != null)
            {
                if (playbackThread.IsAlive)
                {

                }

                if (!playbackThread.Join(3000))
                {
                    //Debug.Fail("!playbackThread.Join(3000)");
                    throw new TimeoutException("!playbackThread.Join(3000)");
                }
            }
        }

        //public void Close()
        //{
        //    logger.Debug("PlaybackHost::Close(...)");

        //    try
        //    {
        //        if (playbackThread != null)
        //        {
        //            if (playbackThread.IsAlive)
        //            {
        //                closing = true;
        //                syncEvent.Set();
        //            }

        //            if (!playbackThread.Join(3000))
        //            {
        //                //Debug.Fail("!playbackThread.Join(3000)");
        //                throw new TimeoutException("!playbackThread.Join(3000)");
        //            }
        //        }

        //        dispatcher.InvokeShutdown();
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Fatal(ex);

        //        //Environment.Exit(-100501);
        //        Process.GetCurrentProcess().Kill();
        //    }
        //    finally
        //    {
        //        //Environment.Exit(0);

        //    }


        //    //Process.GetCurrentProcess().Kill();

        //}


        private void ResetSession()
        {
            openingTime = 0;

            PlaybackStats = null;

            Position = -1;
            // Volume = -1;
            CurrentTime = TimeSpan.Zero;
            TotalTime = TimeSpan.Zero;

            commandQueue.Clear();
        }

        private void CleanUp()
        {
            //Thread.Sleep(1000000);
            //throw new Exception("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! CleanUp()");
            logger.Debug("CleanUp(...)");
           // communicationClient?.Close();

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

            videoGrabber?.Dispose();
            mrlProvider?.Dispose();

            // cancellationTokenSource?.Dispose();
        }

        /*
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

                Play(arg0);

                //PlayCommand.Execute(arg0);
            }
            else if (command == "Pause")
            {
                Pause();
            }
            else if (command == "Stop")
            {
                Stop();
            }
            else if (command == "Mute")
            {
                Mute(args);
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
            else if (command == "SetRate")
            {
                EnqueueCommand("SetRate", args);
            }
            else if (command == "Blur")
            {
                int blurRadius = 0;
                if (int.TryParse(arg0, out blurRadius))
                {
                    Session.SetBlurRadius(blurRadius);
                }
            }
            else if (command == "SwitchVisibilityState")
            {
                bool visible = false;
                if (bool.TryParse(arg0, out visible))
                {
                    Session.SetVideoWindowVisible(visible);

                }
            }
            else if (command == "SetLoopPlayback")
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
        */

        public event Action<string, object[]> PlaybackChanged;
        private void OnPlaybackChanged(string command, object[] args = null)
        {
            PlaybackChanged?.Invoke(command, args);
        }

        //private void InvokeEventAsync(string command, object[] args = null)
        //{
        //    InvokeEvent(command, args);
        //    //return Task.Run(() => InvokeEvent(command, args));
        //}

        //private void InvokeEvent(string command, object[] args = null)
        //{
        //    //logger.Debug("PostToClient(...) " + command);
        //    communicationClient?.OnPostMessage(command, args);
        //}

        private void SetupVideo(IntPtr handle, int width, int height, PixelFormat fmt, int pitches, int offset)
        {

            videoSourceProvider?.SetupVideo(handle, width, height, fmt, pitches, offset);

            //var _fmt = (fmt == PixelFormats.Bgra32) ?
            //    System.Drawing.Imaging.PixelFormat.Format32bppArgb :
            //    System.Drawing.Imaging.PixelFormat.Format32bppRgb;

            //InvokeEventAsync("VideoFormat", new object[] { AppId, width, height, (int)_fmt, pitches });
        }


        private void DisplayVideo()
        {
            videoSourceProvider?.DisplayVideo();

            //this.dispatcher.BeginInvoke(DispatcherPriority.Render,
            //    (Action)(() =>
            //    {
            //        interopBitmap?.Invalidate();
            //    }));
        }

        private void CleanupVideo()
        {
            OnPlaybackChanged("StopDisplay");

            videoSourceProvider?.CleanupVideo();

        }



        class VideoFrameGrabber : IDisposable
        {

            private readonly VlcPlayback playback = null;

            private MemoryMappedFile memoryMappedFile;
            private MemoryMappedViewAccessor memoryMappedView;

            private EventWaitHandle eventWaitHandle = null;

            private bool isAlphaChannelEnabled = true;

            public VideoFrameGrabber(VlcPlayback playback)
            {
                this.playback = playback;
            }

            internal string eventId = "";
            internal string memoryId = "";

            public void Setup()
            {
                logger.Debug("Setup(...)");


                var player = playback.mediaPlayer;
                if (player != null)
                {                
                    memoryId = Guid.NewGuid().ToString("N");
                    this.memoryMappedFile = MemoryMappedFile.CreateNew(memoryId, 30 * 1024 * 1024, MemoryMappedFileAccess.ReadWrite);

                    eventId = Guid.NewGuid().ToString("N");
                    this.eventWaitHandle = CreateEventWaitHandle(eventId);

                    player.SetVideoFormatCallbacks(this.VideoFormat, this.Cleanup);
                    player.SetVideoCallbacks(LockVideo, null, Display, IntPtr.Zero);
                    
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
                logger.Debug("VideoFormat(...) " + eventId + " chroma " + chroma + " width " + width + " height " + height + " pitches " + pitches + " lines " + lines);

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
                logger.Debug("CleanupVideo(...)");

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
                    playback.OnPlaybackChanged("StartDisplay");
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
                logger.Debug("RemoveVideo(...)");

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
                logger.Debug("Dispose(...)");
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
            ~VideoFrameGrabber()
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

        }// VideoFrameGrabber


        class MrlProvider
        {
            private readonly VlcPlayback playback = null;
            internal MrlProvider(VlcPlayback host)
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

                if (YoutubeExplode.YoutubeClient.TryParseVideoId(mediaLink, out videoId))
                {
                    var youtube = new YoutubeExplode.YoutubeClient();

                    YoutubeExplode.Models.MediaStreams.MediaStreamInfo streamInfo = null;
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

                    //if (YoutubeApi.YoutubeClient.TryParseVideoId(mediaLink, out videoId))
                    //{ 
                    //using (var youtube = new YoutubeApi.YoutubeClient())
                    //{
                    //    YoutubeApi.MediaStreamInfo streamInfo = null;
                    //    var streamInfoSet = await youtube.GetVideoMediaStreamInfosAsync(videoId);
                    //    if (streamInfoSet != null)
                    //    {
                    //        if (streamInfoSet.Muxed?.Count > 0)
                    //        {
                    //            streamInfo = streamInfoSet.Muxed.OrderByDescending(s => s.VideoQuality).FirstOrDefault();
                    //        }
                    //        else if (streamInfoSet.Video?.Count > 0)
                    //        {
                    //            streamInfo = streamInfoSet.Video.OrderByDescending(s => s.VideoQuality).FirstOrDefault();
                    //        }
                    //        else if (streamInfoSet.Audio?.Count > 0)
                    //        {
                    //            streamInfo = streamInfoSet.Audio.OrderByDescending(s => s.Bitrate).FirstOrDefault();
                    //        }
                    //    }

                    //    if (streamInfo != null)
                    //    {
                    //        mri = streamInfo.Url;
                    //    }
                    //    else
                    //    {
                    //        throw new Exception("Unsupported youtube link!");
                    //    }
                    //}
                    //}
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

    public class VideoSourceFormat
    {
        public IntPtr handle = IntPtr.Zero;
        public int width = 0;
        public int height = 0;
        public int offset = 0;
        public PixelFormat fmt = PixelFormats.Default;
        int pitches = 0;
    }
}