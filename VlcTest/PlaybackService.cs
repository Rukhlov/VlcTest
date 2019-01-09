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

        public PlaybackService()
        { }


        private ServiceHost service = null;

        private ICommunicationCallback playbackChannel = null;


        private Thread playbackThread = null;
        private AutoResetEvent syncEvent = new AutoResetEvent(false);
        private volatile bool closing = false;

        private CommandQueue commandQueue = new CommandQueue();

        public void Setup(object[] args)
        {

            playbackThread = new Thread(PlaybackProc);
            playbackThread.IsBackground = true;
            playbackThread.Start(args);
        }

        private void PlaybackProc(object obj)
        {
            logger.Trace("PlaybackProc() BEGIN");

            try
            {
                closing = false;

                var args = obj as object[];
                var fileName = args[0].ToString();
                StartUp(fileName);

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
            }
            catch(Exception ex)
            {
                logger.Error(ex);

            }
            finally
            {
                CleanUp();

                OnStateChanged("Closed");
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
                case "ServiceOpened":
                    {
                        IsOpened = true;

                        var obj = command.args[0];

                        var serv = (ServiceHost)obj;
                        Name = "Server: " + string.Join(";", serv.BaseAddresses.Select(a => a.ToString()));


                        OnStateChanged("Service openned...");
                    }
                    break;
                case "ServiceFaulted":
                    {
                        //...

                        playbackChannel = null;
                        OnStateChanged("Service fauled...");
                    }
                    break;
                case "ServiceClosed":
                    {
                        //...


                        Name = "";

                        IsOpened = false;
                        playbackChannel = null;
                        OnStateChanged("Service closed...");
                    }
                    break;

                case "RunPlaybackCommand":
                    {
                        var cmd = command.args[0].ToString();
                        object[] _args = null;
                        if (command.args.Length > 1)
                        {
                            _args = command.args[1] as object[];
                        }

                        DoPostMessage(cmd, _args);
                    }
                    break;
                case "Playing":
                    {
                        playbackStarting = false;
                        count++;
                        IsPaused = false;
                        IsStopped = false;

                        OnStateChanged("UpdateUi");
                        //owner.UpdateUi();

                        logger.Debug("Play count " + count);
                    }
                    break;
                case "CleanupVideo":
                    {
                        OnStateChanged("StopDisplay");

                    }
                    break;
                case "Paused":
                    {
                        IsPaused = true;

                        OnStateChanged("UpdateUi");

                    }
                    break;
                case "Initiated":
                    {
                        OnStateChanged("StartDisplay");

                        //owner.videoControl.Play();
                    }
                    break;
                case "Stopped":
                    {
                        IsStopped = true;

                       // owner.videoControl.Clear();

                        OnStateChanged("Stopped");
                        //owner.UpdateUi();
                    }
                    break;
                case "LengthChanged":
                    {
                       // var val0 = command.args[0].ToString();
                        //owner.SetMediaLength(val0);


                        OnStateChanged("LengthChanged", command.args);
                    }
                    break;
                case "Position":
                    {
                        OnStateChanged("Position", command.args);

                        //var val0 = command.args[0].ToString();

                        //owner.SetPosition(val0);
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
        public event Action<string, object[]> StateChanged;

        private void OnStateChanged(string state, object[] args = null)
        {
            StateChanged?.Invoke(state, args);

        }


        enum TransportType
        {
            Pipe,
            Tcp,
        }

        private static string currentDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        private Process playbackProcess = null;

        private volatile bool playbackStarting = false;

        public void Play(string uri, bool forse = false)
        {
            if (IsConnected)
            {
                if (/*IsPaused ||*/ IsStopped || forse)
                {
                    EnqueueCommand("RunPlaybackCommand", new object[] { "Play", new[] { uri } });
                }
                else
                {
                    Pause();
                }
            }
            else
            {
                //StartPlayer(mediaUri);
                Setup(new object[] { uri });
            }
        }

        public void SetPosition(double position)
        {
            EnqueueCommand("RunPlaybackCommand", new object[] { "Position", new object[] { position } });

        }

        public void SetMute(bool mute)
        {
            EnqueueCommand("RunPlaybackCommand", new object[] { "Mute", new object[] { mute } });

        }

        public void SetVideoAdjustments(object [] adjustments)
        {
            EnqueueCommand("RunPlaybackCommand", new object[] { "SetAdjustments", adjustments });

        }
        

        public void SetVolume(int volume)
        {
            EnqueueCommand("RunPlaybackCommand", new object[] { "Volume", new object[] { volume } });
        }

        public void Pause()
        {
            if (IsConnected && IsOpened)
            {
                EnqueueCommand("RunPlaybackCommand", new[] { "Pause" });
            }
        }

        public void Stop()
        {
            if (IsConnected)
            {
                EnqueueCommand("RunPlaybackCommand", new[] { "Stop" });
            }
            else
            {
                ClosePlaybackProccess();
            }
        }

        public void Close()
        {
            closing = true;
            commandQueue.Clear();
            syncEvent.Set();
            
            //CleanUp();
        }



        private void StartUp(string fileName)
        {
            try
            {

                CleanUp();

                int tryCount = 5;
                while (tryCount-- > 0)
                {
                    try
                    {
                        logger.Debug("Try to open " + address);
                        OpenCommunicationHost(address);
                        break;
                    }
                    catch (AddressAlreadyInUseException ex)
                    {
                        logger.Error(ex);

                        CloseCommuntcationHost();

                        address += "_" + Guid.NewGuid();
                        continue;
                    }
                }

                IsOpened = true;
                IsStopped = true;

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


                playbackProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                playbackProcess.Exited += PlaybackProcess_Exited;

                playbackStarting = true;

                playbackProcess.Start();

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


        private void CleanUp()
        {
            try
            {
                CloseCommuntcationHost();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                ClosePlaybackProccess();
            }
        }


        private void ClosePlaybackProccess()
        {
            try
            {
                playbackStarting = false;

                if (playbackProcess != null && !playbackProcess.HasExited)
                {
                    playbackProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                //Debug.Fail(ex.Message);
            }
            finally
            {
                if (playbackProcess != null)
                {
                    playbackProcess.Dispose();
                    playbackProcess = null;
                }
            }
        }

        private void PlaybackProcess_Exited(object sender, EventArgs e)
        {
            Process p = sender as Process;
            if (p != null)
            {
                int code = p.ExitCode;
                logger.Debug("Client process exited with code: " + code);
                p.Dispose();

                playbackStarting = false;

                Close();
            }
        }



        private void OpenCommunicationHost(string address)
        {

            logger.Debug("OpenServiceHost(...) " + address);

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

            EnqueueCommand("ServiceOpened", new object[] { sender });
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

        private void CloseCommuntcationHost()
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
                    OnStateChanged("SetupDisplay", args);


                    //throw new Exception("CONNECTION_ERROR!");
                    //Thread.Sleep(15000);

                    playbackChannel = OperationContext.Current.GetCallbackChannel<ICommunicationCallback>();
                    ((IClientChannel)playbackChannel).Closed += (o, a) =>
                    {
                        logger.Debug("Client " + id + " channel closed...");

                        Close();

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

            ProcessCommand(command, args);

            //Task.Run(() => ProcessCommand(command, args));

            //owner.Log("PostMessage END");

        }

        private string applicationId = "";
        int count = 0;
        private void ProcessCommand(string command, object[] args)
        {
            try
            {

                //var val0 = "";
                //if (args != null && args.Length > 0)
                //{
                //    val0 = args[0]?.ToString();
                //}

                if (command == "Playing")
                {
                    EnqueueCommand("Playing");

                }
                else if (command == "VideoFormat")
                {

                 }
                else if (command == "CleanupVideo")
                {
                    EnqueueCommand("CleanupVideo");

                }
                else if (command == "Paused")
                {

                    EnqueueCommand("Paused");

                 }
                else if (command == "Restarting")
                {

                }//
                else if (command == "Initiated")
                {
                    EnqueueCommand("Initiated");

                }
                else if (command == "Stopped")
                {

                    EnqueueCommand("Stopped");

                }
                else if (command == "LengthChanged")
                {
                    EnqueueCommand("LengthChanged", args);
                }
                else if (command == "Position")
                {
                    EnqueueCommand("Position", args);

                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void ExecuteCommand(string msg, object[] args)
        {
            if (closing)
            {
                return;
            }

            EnqueueCommand("RunPlaybackCommand", new object[] { msg, args });

        }

        private void DoPostMessage(string msg, object[] args)
        {

            //owner.Log("PostMessage BEGIN");
            try
            {
                //Thread.Sleep(3000);
                if (playbackChannel != null)
                {
                    //logger.Debug("Server post: " + msg);
                    playbackChannel.PostMessage(msg, args);
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
