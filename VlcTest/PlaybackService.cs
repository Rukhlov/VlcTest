using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VlcContracts;

namespace VlcTest
{

    //[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    class PlaybackService : ICommunicationService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public PlaybackService(MainForm o)
        {
            this.owner = o;

        }

        private MainForm owner = null;

        private ServiceHost service = null;

        private ICommunicationCallback playbackChannel = null;


        private Thread playbackThread = null;
        private AutoResetEvent syncEvent = new AutoResetEvent(false);
        private volatile bool closing = false;

        private CommandQueue commandQueue = new CommandQueue();

        public void Setup()
        {

            playbackThread = new Thread(PlaybackProc);
            playbackThread.IsBackground = true;
            playbackThread.Start();
        }

        private void PlaybackProc()
        {
            logger.Trace("PlaybackProc() BEGIN");
            while (true)
            {

                InternalCommand command = null;
                do
                {
                    command = DequeueCommand();
                    if (command != null)
                    {
                        //...
                        ProcessPlaybackCommand(command);
                    }

                } while (command != null);

                syncEvent.WaitOne(1000);

                if (closing)
                {
                    break;
                }
            }

            logger.Trace("PlaybackProc() END");
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
                case "StartPlayer":
                    {
                        var fileName = command.args[0].ToString();

                        DoStartPlayer(fileName);
                    }
                    break;
                case "ServiceOpened":
                    {
                        IsOpened = true;

                        OnStateChanged("Service openned...");
                    }
                    break;
                case "ServiceFaulted":
                    {
                        playbackChannel = null;
                        OnStateChanged("Service fauled...");
                    }
                    break;
                case "ServiceClosed":
                    {
                        Name = "";

                        IsOpened = false;
                        playbackChannel = null;
                        OnStateChanged("Service closed...");
                    }
                    break;
                case "StopPlayer":
                    {
                        DoStopPlayer();
                    }
                    break;

                case "PostMessage":
                    {
                        var cmd = command.args[0].ToString();
                        var _args = command.args[1] as object[];

                        DoPostMessage(cmd, _args);
                    }
                    break;
                default:
                    break;
            }
        }

        // public Dictionary<ICommunicationCallback, string> clients = new Dictionary<ICommunicationCallback, string>();
        internal string address = CommunicationConst.PipeAddress;


        public bool IsOpened = false;

        public bool IsConnected
        {
            get
            {
                return (playbackChannel != null && 
                    ((IClientChannel)playbackChannel).State == CommunicationState.Opened);
            }
        }


        internal bool IsStopped = true;
        internal bool IsPaused = false;


        public string Name = "";
        public event Action<string> StateChanged;

        private int seq = 0;
        private void OnStateChanged(string state)
        {
            StateChanged?.Invoke(state);

        }


        enum TransportType
        {
            Pipe,
            Tcp,
        }

        private static string currentDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        private Process clientProcess = null;

        private volatile bool playbackStarting = false;

        internal void StartPlayer(string fileName)
        {
            logger.Debug("StartPlayerProc(...) " + fileName);

            EnqueueCommand("StartPlayer", new[] { fileName });


        }

        internal void StopPlayer()
        {
            EnqueueCommand("StopPlayer");
        }

        private void DoStopPlayer()
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                CloseClientProccess();
            }
        }

        private void DoStartPlayer(string fileName)
        {
            try
            {

                TryToOpen(address);

                //if (!this.IsOpened)
                //{

                //    return;
                //}

                IsStopped = true;

                CloseClientProccess();


                var _vlcopts = new string[] { "--extraintf=logger",
                                                "--verbose=0",
                                                "--network-caching=1000"
                                            };

                string vlcopts = string.Join(" ", _vlcopts);

                var args = new string[] { "--channel=\"" + address + "\"",
                                           "--media=\"" + fileName + "\"",
                                           "--parentid=\"" + Process.GetCurrentProcess().Id + "\"",
                                           //"--eventid=\"" + syncEventId + "\"",
                                           "--vlcopts=\"" + vlcopts + "\"",
                                          // "--hwnd=\"" + videoForm.Handle + "\""
                                         };

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "VlcPlayer.exe",
                    Arguments = string.Join(" ", args),
                };


                clientProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                clientProcess.Exited += ClientProcess_Exited;

                playbackStarting = true;

                clientProcess.Start();


                logger.Debug("Client proccess started...");
            }
            catch (Exception ex)
            {
                //buttonStartClientInstance.Enabled = true;
                playbackStarting = false;
                logger.Error(ex);
            }
            finally
            {
                //processStarting = false;
            }
        }

        private void TryToOpen(string address)
        {
            logger.Debug("TryToOpen(...)");

            try
            {

                Close();

                //playbackService = new PlaybackService(this);
                //playbackService.StateChanged += new Action<string>(communicationService_StateChanged);

                int tryCount = 5;
                while (tryCount-- > 0)
                {
                    try
                    {
                        logger.Debug("Try to open " + address);
                        OpenServiceHost(address);
                        break;
                    }
                    catch (AddressAlreadyInUseException ex)
                    {
                        logger.Error(ex);

                        Close();

                        address += "_" + Guid.NewGuid();
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {

                logger.Error(ex);
                CleanUp();
            }

        }


        private void CleanUp()
        {

            Close();

            owner.UpdateUi();
        }

        internal void CloseClientProccess()
        {
            try
            {
                playbackStarting = false;

                if (clientProcess != null && !clientProcess.HasExited)
                {
                    clientProcess.Kill();
                }


            }
            catch (Exception ex)
            {
                logger.Error(ex);
                //Debug.Fail(ex.Message);
            }
            finally
            {
                //if (clientProcess != null)
                //{
                //    clientProcess.Dispose();
                //    clientProcess = null;
                //}

                owner.UpdateUi();
            }
        }

        private void ClientProcess_Exited(object sender, EventArgs e)
        {
            Process p = sender as Process;
            if (p != null)
            {
                int code = p.ExitCode;
                logger.Debug("Client process exited with code: " + code);
                p.Dispose();

                playbackStarting = false;
            }

            // communicationService?.Close();

            //videoControl?.Close();

        }



        private void OpenServiceHost(string address)
        {

            logger.Debug("OpenServiceHost(...) " + address);

            seq = 0;

            System.ServiceModel.Channels.Binding binding = null;
            var uri = new Uri(address);
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

            if (binding == null)
            {
                return;
            }

            service = new ServiceHost(this, uri);

            service.AddServiceEndpoint(typeof(ICommunicationService), binding, uri);

            service.Opened += new EventHandler(service_Opened);
            service.Faulted += new EventHandler(service_Faulted);
            service.Closed += new EventHandler(service_Closed);

            service.Open();


        }

        private void service_Opened(object sender, EventArgs e)
        {
            logger.Debug("service_Opened(...)");

            var serv = (ServiceHost)sender;
            Name = "Server: " + string.Join(";", serv.BaseAddresses.Select(a => a.ToString()));

            EnqueueCommand("ServiceOpened");
        }

        private void service_Closed(object sender, EventArgs e)
        {
            logger.Debug("service_Closed(...)");
            EnqueueCommand("ServiceClosed");


        }

        private void service_Faulted(object sender, EventArgs e)
        {
            logger.Debug("service_Faulted(...)");
            EnqueueCommand("ServiceFaulted");


        }

        public void Close()
        {
            logger.Debug("Close()");

            if (service != null)
            {
                try
                {
                    if (service.State != CommunicationState.Closed)
                    {
                        //service.Close();
                        service.Abort();
                    }

                }
                catch (Exception ex)
                {
                    service.Abort();
                }
                finally
                {
                    service.Opened -= new EventHandler(service_Opened);
                    service.Faulted -= new EventHandler(service_Faulted);
                    service.Closed -= new EventHandler(service_Closed);
                    service = null;
                    playbackChannel = null;
                }
            }
            IsOpened = false;

        }

        bool ICommunicationService.Connect(string id, object[] args)
        {
            logger.Debug("ICommunicationService.Connect(...) " + id);

            bool Result = true;
            try
            {

                if (playbackChannel != null)
                {
                    Result = false;
                }
                else
                {
                    var eventId = args?[0]?.ToString();
                    var memoryId = args?[1]?.ToString();

                    owner.CreateVideoControl(eventId, memoryId);

                    //throw new Exception("CONNECTION_ERROR!");
                    //Thread.Sleep(15000);

                    playbackChannel = OperationContext.Current.GetCallbackChannel<ICommunicationCallback>();
                    ((IClientChannel)playbackChannel).Closed += (o, a) =>
                    {
                        playbackChannel = null;
                        OnStateChanged("Client " + id + " channel closed...");
                    };

                    OnStateChanged("Client " + id + " connected...");

                    Result = true;
                }
            }
            catch (Exception ex)
            {
                Result = false;
                OnStateChanged("Client error: " + ex.Message);
            }

            return Result;

        }

        void ICommunicationService.Disconnect()
        {
            logger.Debug("ICommunicationService.Disconnect()");

            try
            {
                if (playbackChannel != null)
                {
                    OnStateChanged("Client disconnected...");
                    playbackChannel = null;
                }
            }
            catch (Exception ex)
            {
                OnStateChanged("Client error: " + ex.Message);
            }
        }

        object ICommunicationService.SendMessage(string message, object[] args)
        {
            //MessageBox.Show(message);

            logger.Debug(message);

            Thread.Sleep(2000);

            return "OK Client (" + message + ")";

        }


        private Func<string, string> messageProc = null;
        IAsyncResult ICommunicationService.BeginSendMessage1(string message, object[] args, AsyncCallback cb, object state)
        {
            if (messageProc == null)
            {
                messageProc = new Func<string, string>((msg) =>
                {

                    logger.Debug("Begin process message " + "\"" + msg + "\"");

                    Thread.Sleep(5000);
                    logger.Debug("End process message: " + "\"" + msg + "\"");

                    return "OK Client (" + msg + ")";

                });
            }

            return messageProc.BeginInvoke(message, cb, state);
        }

        object ICommunicationService.EndSendMessage1(IAsyncResult result)
        {
            return messageProc.EndInvoke(result);
        }

        //internal long totalMediaLength = 0;
        //float mediaPosition = 0;
        void ICommunicationService.PostMessage(string command, object[] args)
        {
            // owner.Log("PostMessage BEGIN");

            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            Task.Run(() => ProcessCommand(command, args));

            //owner.Log("PostMessage END");

        }

        private string applicationId = "";
        int count = 0;
        private void ProcessCommand(string command, object[] args)
        {
            try
            {
                // var client = OperationContext.Current.GetCallbackChannel<ICommunicationCallback>();

                //owner.Log("Client post msg: " + msg);
                //Thread.Sleep(1000);

                var val0 = "";
                if (args != null && args.Length > 0)
                {
                    val0 = args[0]?.ToString();
                }

                if (command == "Playing")
                {
                    playbackStarting = false;
                    count++;
                    IsPaused = false;
                    IsStopped = false;

                    //owner.SetupVideoForm();
                    owner.UpdateUi();

                    //owner.Invoke(new Action(() =>
                    //{
                    //    owner.trackBarPosition.Enabled = !owner.IsStopped;
                    //    owner.buttonPlay.Text = "Pause";

                    //}));

                    logger.Debug("Play count " + count);
                }
                else if (command == "VideoFormat")
                {

                    //applicationId = args?[0]?.ToString();

                    //owner.videoForm.videoControl1.AppEventId = args?[0]?.ToString();
                    Debug.WriteLine(">>>>>>>>>>>>> applicationId " + applicationId);

                    //owner.ShowVideoForm(applicationId);
                    //owner.ShowVideoForm(args);//videoControl?.Open(args);


                }
                else if (command == "CleanupVideo")
                {
                    owner.videoControl.Stop();

                    //owner.videoForm.StopRender();

                    //owner.(args);//videoControl?.Open(args);

                }
                else if (command == "Paused")
                {
                    IsPaused = true;
                    owner.UpdateUi();

                    //TimeSpan ts = TimeSpan.Zero;
                    //owner.Invoke(new Action(() =>
                    //{

                    //    owner.buttonPlay.Text = "Play";

                    //}));
                }
                else if (command == "Restarting")
                {
                    //owner.videoForm.videoControl1.Reset();

                    //owner.videoForm.StopRender();
                }//
                else if (command == "Initiated")
                {
                    owner.videoControl.Play();

                }
                else if (command == "Stopped")
                {
                    IsStopped = true;

                    // owner.videoForm.StopRender();

                    owner.videoControl.Clear();

                    //owner.videoControl?.Close();
                    owner.UpdateUi();


                    //TimeSpan ts = TimeSpan.Zero;
                    //owner.Invoke(new Action(() =>
                    //{
                    //    owner.buttonPlay.Text = "Play";
                    //    owner.trackBarPosition.Enabled = false;

                    //    owner.labelCurrentTime.Text = "--:--";
                    //    owner.labelTotalTime.Text = "--:--";
                    //    owner.trackBarPosition.Value = 0;

                    //}));

                    //PostMessage("Play" + ";" + owner.currentMediaFile);
                }
                else if (command == "LengthChanged")
                {
                    owner.SetMediaLength(val0);
                }
                else if (command == "Position")
                {
                    owner.SetPosition(val0);
                }



            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void PostMessage(string msg, object[] args)
        {
            EnqueueCommand("PostMessage", new object[] { msg, args });

        }

        private void DoPostMessage(string msg, object[] args)
        {

            //owner.Log("PostMessage BEGIN");
            try
            {
                //Thread.Sleep(3000);
                if (playbackChannel != null)
                {

                    seq++;
                    string text = msg; // + " seq " + seq;
                    logger.Debug("Server post: " + text);
                    playbackChannel.PostMessage(text, args);
                    //Thread.Sleep(2000);


                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
            //owner.Log("PostMessage END");
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

        class InternalCommand
        {
            public string command = "";
            public object[] args = null;
        }


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

    }


}
