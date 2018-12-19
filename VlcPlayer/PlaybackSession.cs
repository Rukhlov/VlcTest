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

        }

        public PlaybackSession(CommandLineOptions options)
        {
            if (options != null)
            {
                this.MediaAddr = options.FileName;
                this.RemoteAddr = options.ServerAddr;
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
                if (!Object.ReferenceEquals(this.blurEffect, value))
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
