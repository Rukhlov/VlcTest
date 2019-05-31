using Microsoft.Win32;
using NLog;
using System;
using System.ComponentModel;
using System.Diagnostics;
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
    public class PlaybackHost
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private CommunicationClient ipcChannel = null;
        private PlaybackController controller = null;

        public Dispatcher Dispatcher { get; private set; }

        public VlcPlayback Playback { get; private set; }

        private VideoWindow mainWindow = null;
        public VideoWindow MainWindow
        {
            get
            {
                if (mainWindow == null)
                {
                    mainWindow = new VideoWindow
                    {
                        DataContext = controller,
                    };
                }
                return mainWindow;
            }
        }

        public void Run()
        {

            logger.Debug("Run()");

            Dispatcher = Dispatcher.CurrentDispatcher;

            Playback = new VlcPlayback();
            Playback.PlaybackChanged += vlcPlayback_PlaybackChanged;


            controller = new PlaybackController(this);


            var cmdOpts = Session.Config.Options;

            Session.Config.ExchangeId = Guid.NewGuid();

            var videoBufferId = Session.Config.ExchangeId;
            if (videoBufferId != Guid.Empty)
            {
                string bufferName = videoBufferId.ToString("N");

                VideoBufferInfo videoInfo = new VideoBufferInfo
                {
                    Width = 1920,
                    Height = 1080,
                    PixelFormat = System.Windows.Media.PixelFormats.Bgr32,
                    DataOffset = 1024,
                };

                Playback.SetOutputVideoToBuffer(bufferName, videoInfo);
            }


            if (Session.ParentProcess == null)
            {
                var handle = new WindowInteropHelper(this.MainWindow).EnsureHandle();

                if (handle != IntPtr.Zero)
                {
                   Playback.SetVideoHostHandle(handle);
                }
            }


            string remoteAddr = cmdOpts?.ServerAddr;

            if (!string.IsNullOrEmpty(remoteAddr))
            {
                ipcChannel = new CommunicationClient(this);

                ipcChannel.Setup(remoteAddr);

                var o = ipcChannel.Connect(new object[] { videoBufferId, });
                if (o != null)
                {
                    //playbackHost.Volume = options.Volume;
                    //playbackHost.IsMute = options.IsMute;
                    //vlcPlayback.LoopPlayback = options.LoopPlayback;

                    //this.SetBlurRadius(options.BlurRadius);
                }
            }

            Playback.Start(cmdOpts?.FileName);

            //MainWindow.Show();

            //================= RUN ==================
            System.Windows.Threading.Dispatcher.Run();
        }

        private void vlcPlayback_PlaybackChanged(string command, object[] args)
        {
            //...
            OnSendCommand(command, args);
        }

        public void OnSendCommand(string command, object[] args)
        {
            //...

            ipcChannel?.OnPostMessage(command, args);
        }

        public void OnReceiveCommand(string command, object[] args)
        {

            //...
            ProcessCommand(command, args);
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

                controller.PlayCommand.Execute(args);
            }
            else if (command == "Pause")
            {
                controller.PauseCommand.Execute(null);

                //vlcPlayback.Pause();
            }
            else if (command == "Stop")
            {
                controller.StopCommand.Execute(null);

                // vlcPlayback.Stop();
            }
            else if (command == "Mute")
            {
                bool mute = false;
                if (bool.TryParse(arg0, out mute))
                {
                    Playback.SetMute(mute);
                }
            }
            else if (command == "Position")
            {
                double pos = 0;
                if (double.TryParse(arg0, out pos))
                {
                    Playback.SetPosition(pos);
                }

                //EnqueueCommand("Position", new[] { arg0 });
            }
            else if (command == "Volume")
            {
                int volume = 0;
                if (int.TryParse(arg0, out volume))
                {
                    Playback.SetVolume(volume);
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
                    controller.SetBlurRadius(blurRadius);
                }
            }
            else if (command == "SwitchVisibilityState")
            {

                bool visible = false;
                if (bool.TryParse(arg0, out visible))
                {
                    controller.SetVideoWindowVisible(visible);
                    // playbackController.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

                }
            }
            else if (command == "SetLoopPlayback")
            {
                Playback.SwitchLoopPlayback(arg0);
            }
        }



        public void Quit()
        {
            logger.Debug("Quit()");

            try
            {
                ipcChannel?.Close();

                if (Playback != null)
                {
                    Playback.PlaybackChanged -= vlcPlayback_PlaybackChanged;
                    Playback.Close();
                }


                Dispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                Process.GetCurrentProcess().Kill();
            }

        }
    }







}
