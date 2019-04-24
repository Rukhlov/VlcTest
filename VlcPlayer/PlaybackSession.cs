using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace VlcPlayer
{
    public class PlaybackSession : INotifyPropertyChanged
    {
        public PlaybackSession()
        {
            this.PlaybackState = PlaybackState.Created;
        }

        public PlaybackSession(CommandLineOptions options) : this()
        {
            if (options != null)
            {
                this.MediaAddr = options.FileName;
                this.RemoteAddr = options.ServerAddr;
                this.ParentId = options.ParentId;
                this.WindowHandle = options.WindowHandle;
            }
        }

        private PlaybackState playbackState;
        public PlaybackState PlaybackState
        {
            get { return playbackState; }
            set
            {
                playbackState = value;
                OnPropertyChanged(nameof(PlaybackState));
            }
        }


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

        [CommandLine.Option("channel")]
        public string RemoteAddr
        {
            get { return remoteAddr; }
            set
            {
                remoteAddr = value;
                OnPropertyChanged(nameof(RemoteAddr));
            }
        }

        private bool isMute = false;
        public bool IsMute
        {
            get { return isMute; }
            set
            {
                isMute = value;
                OnPropertyChanged(nameof(IsMute));
            }
        }

        private int volume = 0;
        public int Volume
        {
            get { return volume; }
            set
            {
                volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }

        private string mediaAddr = "";

        [CommandLine.Option("media")]
        public string MediaAddr
        {
            get { return mediaAddr; }
            set
            {
                mediaAddr = value;
                OnPropertyChanged(nameof(MediaAddr));
            }
        }

        private string mrl = "";
        public string Mrl
        {
            get { return mrl; }
            set
            {
                mrl = value;
                OnPropertyChanged(nameof(Mrl));
            }
        }

        private float position = 0;
        public float Position
        {
            get { return position; }
            set
            {
                position = value;
                OnPropertyChanged(nameof(Position));
            }
        }

        private TimeSpan currentTime = TimeSpan.MinValue;
        public TimeSpan CurrentTime
        {
            get { return currentTime; }
            set
            {
                currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        private TimeSpan totalTime = TimeSpan.MinValue;
        public TimeSpan TotalTime
        {
            get { return totalTime; }
            set
            {
                totalTime = value;
                OnPropertyChanged(nameof(TotalTime));
            }
        }


        private PlaybackStatistics playbackStats = new PlaybackStatistics();
        public PlaybackStatistics PlaybackStats
        {
            get { return playbackStats; }
            set
            {
                playbackStats = value;
                OnPropertyChanged(nameof(PlaybackStats));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Reset()
        {
            PlaybackStats = null;
            Position = -1;
            // Volume = -1;
            CurrentTime = TimeSpan.Zero;
            TotalTime = TimeSpan.Zero;
        }


        [CommandLine.Option("parentid")]
        public int ParentId { get; set; }

        [CommandLine.Option("eventid")]
        public string SyncEventId { get; set; }

        [CommandLine.Option("vlcopts")]
        public string VlcOptions { get; set; }

        [CommandLine.Option("hwnd")]
        public int WindowHandle { get; set; }


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
