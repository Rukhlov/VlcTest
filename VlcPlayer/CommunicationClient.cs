using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VlcContracts;

namespace VlcPlayer
{

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    class CommunicationClient : IPlaybackClient// ICommunicationCallback
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();
        public CommunicationClient(PlaybackHost host)
        {
            this.host = host;
        }


        private readonly PlaybackHost host = null;

        //private ICommunicationService playbackService = null;
        private IPlaybackService playbackService = null;

        private Guid Id = Guid.NewGuid();

        private bool IsConnected
        {

            get
            {
                bool isConnected = false;

                if (playbackService != null)
                {
                    var channel = ((IClientChannel)playbackService);
                    isConnected = (channel.State == CommunicationState.Opened);
                }

                return isConnected;
            }

        }

        public void Setup(string addr, object[] args = null)
        {
            try
            {
                System.ServiceModel.Channels.Binding binding = null;
                var uri = new Uri(addr);
                if (uri.Scheme == "net.pipe")
                {
                    var _binding = new NetNamedPipeBinding
                    {
                        ReceiveTimeout = TimeSpan.MaxValue,//TimeSpan.FromSeconds(10),
                        SendTimeout = TimeSpan.FromSeconds(10),
                    };

                    binding = _binding;
                }
                else
                {
                    throw new Exception("Unsupported scheme: " + uri.Scheme);

                }


                // InstanceContext context = new InstanceContext(serverCallback);
                playbackService = DuplexChannelFactory<IPlaybackService>.CreateChannel(this, binding, new EndpointAddress(uri));

                IClientChannel channel = (IClientChannel)playbackService;

                channel.Opened += new EventHandler(channel_Opened);
                channel.Closed += new EventHandler(channel_Closed);

                channel.Faulted += new EventHandler(channel_Faulted);

                channel.Open();

                //Connect(args);
                //throw new Exception("sdfsdfsdfsf");

            }
            catch (Exception ex)
            {
                Close();

                throw;
            }
        }

        private void channel_Opened(object sender, EventArgs e)
        {
            logger.Debug("Channel openned...");

            // Connect();

        }

        public PlaybackOptions Connect(object[] args = null)
        {
            PlaybackOptions options = null;
            try
            {
                string name = Id.ToString("N");

                //PlaybackOptions options = null;
                var obj = playbackService.Connect(name, args);
                if (obj == null)
                {
                    //TODO:
                    throw new Exception("Connection error!");
                }

                options = playbackService.GetPlaybackOptions();

                //if (obj != null)
                //{
                //    options = obj as PlaybackOptions;
                //}

                //if (options == null)
                //{
                //    //TODO:
                //    throw new Exception("Connection error!");
                //}

                logger.Debug("Client id:" + name + " connected " + (options != null));
  
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                host.Quit();
            }

            return options;
        }


        public void Disconnect()
        {
            try
            {
                playbackService.Disconnect();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                host.Quit();
            }
        }

        private void channel_Faulted(object sender, EventArgs e)
        {
            logger.Debug("Channel faulted...");
            try
            {
                ((IClientChannel)sender).Abort();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            //FATAL
            //throw new Exception("Communication channel faulted");
        }


        private void channel_Closed(object sender, EventArgs e)
        {
            logger.Debug("Channel closed...");
            try
            {
                //view.Close();
                //view.CleanUp();
                Close();
            }
            catch (Exception ex)
            {
                logger.Trace(ex);

            }
            finally
            {
                host.Quit();
            }
        }


        object ICommunicationCallback.SendMessage(string message, object[] args)
        {

            logger.Debug("RECEIVED_FROM_SERV: " + "\"" + message + "\"");
            Thread.Sleep(3000);
            return "Client send response: OK " + "\"" + message + "\"";
            //Thread.Sleep(1000);

        }

        private Func<string, string> messageProc = null;
        private AutoResetEvent waitEvent = new AutoResetEvent(false);
        IAsyncResult ICommunicationCallback.BeginSendMessage1(string message, object[] args, AsyncCallback cb, object state)
        {

            if (messageProc == null)
            {
                messageProc = new Func<string, string>((msg) =>
                {
                    logger.Debug("Begin process message " + "\"" + msg + "\"");

                    waitEvent.WaitOne(5000);

                    logger.Debug("End process message " + "\"" + msg + "\"");

                    return "OK Server (" + msg + ")";

                });
            }

            return messageProc.BeginInvoke(message, cb, state);
        }

        object ICommunicationCallback.EndSendMessage1(IAsyncResult ar)
        {
            return messageProc.EndInvoke(ar);
        }


        void ICommunicationCallback.PostMessage(string command, object[] args)
        {
            logger.Debug("ICommunicationCallback.PostMessage(...) " + command);

            if (!string.IsNullOrEmpty(command))
            {
                //Task.Run(() => playback.ProcessIncomingCommand(command, args));
                Task.Run(() => host.OnReceiveCommand(command, args));
            }

        }

        internal void OnPostMessage(string command, object[] args = null)
        {
            if (this.IsConnected)
            {
                playbackService.PostMessage(command, args);
            }
        }

        internal void Close()
        {
            try
            {
                if (playbackService != null)
                {
                    IClientChannel channel = (IClientChannel)playbackService;
                    if (channel.State != CommunicationState.Closed)
                    {
                        try
                        {
                            // channel.Close();
                            channel.Abort();
                        }
                        catch (Exception ex)
                        {
                            channel.Abort();
                        }
                    }
                    channel.Opened -= new EventHandler(channel_Opened);
                    channel.Closed -= new EventHandler(channel_Closed);
                    channel.Faulted -= new EventHandler(channel_Faulted);
                }
            }
            catch (Exception ex)
            {
                logger.Error<Exception>(ex);
            }
        }

    }
}
