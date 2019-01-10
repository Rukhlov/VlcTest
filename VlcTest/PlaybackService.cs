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
    enum PlaybackState
    {
        Created,
        Opening,
        Playing,
        Paused,
        Stopped,
        Faulted,
    }

    class PlaybackSession
    {
        public string Mri { get; set; }

        private PlaybackState _state = PlaybackState.Created;
        public PlaybackState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state != value)
                {
                    var old = _state;
                    _state = value;

                    OnStateChanged(_state, old);
                }
            }

        }

        public double Position { get; set; }
        public int Volume { get; set; }
        public bool IsMute { get; set; }

        public TimeSpan TotalTime { get; set; }
        public TimeSpan CurrentTime { get; set; }


        public string SourceMedia { get; set; }


        public string EventSyncId { get; set; }
        public string MemoryBufferId { get; set; }

        public event Action<PlaybackState, PlaybackState> StateChanged;
        private void OnStateChanged(PlaybackState newState, PlaybackState oldState)
        {
            //StateChanged?.BeginInvoke(state, args, null, null);

            StateChanged?.Invoke(newState, oldState);
        }

    }


    enum ServiceState
    {
        Created,
        Opening,
        Opened,
        ReadyToPlay,
        Closed,
        Faulted,
    }

    //[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    class PlaybackService : ICommunicationService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public PlaybackService()
        { }

        public PlaybackSession Session { get; private set; }

        private ServiceHost service = null;

        private ICommunicationCallback playbackChannel = null;


        private Thread playbackThread = null;
        private AutoResetEvent syncEvent = new AutoResetEvent(false);
        private volatile bool closing = false;

        private CommandQueue commandQueue = new CommandQueue();



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
        public event Action<string, object[]> _StateChanged;
        private void _OnStateChanged(string state, object[] args = null)
        {
            //StateChanged?.BeginInvoke(state, args, null, null);

            _StateChanged?.Invoke(state, args);
        }

        private ServiceState _state = ServiceState.Created;
        public ServiceState State
        {
            get
            {
                return _state;
            }
            set
            {
                if( _state!= value)
                {
                    var old = _state;
                    _state = value;

                    OnStateChanged(_state, old);
                }
            }
        }

        public event Action<object, object[]> Opened;
        private void OnOpened(object[] args = null)
        {
            Opened?.Invoke(this, args);
        }

        public event Action<object, object[]> ReadyToPlay;
        private void OnReadyToPlay(object[] args = null)
        {
            ReadyToPlay?.Invoke(this, args);
        }

        public event Action<object, object[]> Closed;
        private void OnClosed(object[] args = null)
        {
            Closed?.Invoke(this, args);
        }

        public event Action<object, object[]> PlaybackPlaying;
        private void OnPlaybackPlaying(object[] args = null)
        {
            PlaybackPlaying?.Invoke(this, args);
        }

        public event Action<object, object[]> PlaybackStopped;
        private void OnPlaybackStopped(object[] args = null)
        {
            PlaybackStopped?.Invoke(this, args);
        }

        public event Action PlaybackStopDisplay;
        private void OnPlaybackStopDisplay()
        {
            PlaybackStopDisplay?.Invoke();
        }

        public event Action PlaybackStartDisplay;
        private void OnPlaybackStartDisplay()
        {
            PlaybackStartDisplay?.Invoke();
        }

        public event Action<object, object[]> PlaybackPaused;
        private void OnPlaybackPaused(object[] args = null)
        {
            PlaybackPaused?.Invoke(this, args);
        }

        public event Action<float> PlaybackPositionChanged;
        private void OnPlaybackPositionChanged(float position)
        {
            PlaybackPositionChanged?.Invoke(position);
        }

        public event Action<long> PlaybackLengthChanged;
        private void OnPlaybackLengthChanged(long len)
        {
            PlaybackLengthChanged?.Invoke(len);
        }

        public event Action<ServiceState, ServiceState> StateChanged;
        private void OnStateChanged(ServiceState newState, ServiceState oldState)
        {
            StateChanged?.Invoke(newState, oldState);
        }


        private static string currentDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        private Process playbackProcess = null;

        private volatile bool playbackStarting = false;


        public void Setup(object[] args)
        {

            if (playbackThread != null && playbackThread.IsAlive)
            {
                logger.Debug("(playbackThread != null && playbackThread.IsAlive)");
                return;
            }

            State = ServiceState.Opening;

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

                Session = new PlaybackSession
                {
                    State = PlaybackState.Created,
                    SourceMedia = fileName,
                };

                StartUp(fileName);
                started = true;

                State = ServiceState.Opened;
                OnOpened();

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

                State = ServiceState.Faulted;

            }
            finally
            {
                CleanUp();
                State = ServiceState.Closed;
                started = false;

                OnClosed(null);
            }

            logger.Trace("PlaybackProc() END");
        }

        int count = 0;
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
                case "Ipc_Opened":
                    {
                        IsOpened = true;

                        var obj = command.args[0];

                        var serv = (ServiceHost)obj;
                        Name = "Server: " + string.Join(";", serv.BaseAddresses.Select(a => a.ToString()));


                        _OnStateChanged("Service openned...");
                    }
                    break;
                case "Ipc_Faulted":
                    {
                        //...

                        playbackChannel = null;
                        _OnStateChanged("Service fauled...");
                    }
                    break;
                case "Ipc_Closed":
                    {
                        //...


                        Name = "";

                        IsOpened = false;
                        playbackChannel = null;
                        _OnStateChanged("Service closed...");
                    }
                    break;
                case "Ipc_ClientConnected":
                    {
                        //...
                        var args = command.args;
                        if (args != null)
                        {
                            var id = args[0].ToString();
                            var channel = args[1] as ICommunicationCallback;
                            var _args = args[2] as object[];

                            if (playbackChannel != null)
                            {
                                // TODO:
                                // ...
                            }
                            else
                            {
                                playbackChannel = channel;

                                ((IClientChannel)playbackChannel).Closed += (o, a) =>
                                {
                                    logger.Debug("Client " + id + " channel closed...");

                                    Close();
                                };

                                Session.EventSyncId = _args?[0]?.ToString();
                                Session.MemoryBufferId = _args?[1]?.ToString();

                                logger.Debug("Client " + id + " connected...");

                                
                                State = ServiceState.ReadyToPlay;
                                OnReadyToPlay();

                            }
                        }
                    }
                    break;
                case "Ipc_ClientDisconnected":
                    {
                        //...
                        playbackChannel = null;
                    }
                    break;
                case "Playback_RunCommand":
                    {
                        var cmd = command.args[0].ToString();
                        object[] _args = null;
                        if (command.args.Length > 1)
                        {
                            _args = command.args[1] as object[];
                        }

                        RunCommand(cmd, _args);
                    }
                    break;
                case "Playback_Playing":
                    {
                        
                       // IsStopped = false;

                        playbackStarting = false;
                        count++;
                        IsPaused = false;
                        IsStopped = false;

                        Session.State = PlaybackState.Playing;
                        OnPlaybackPlaying();

                        //_OnStateChanged("UpdateUi");
                        //owner.UpdateUi();

                        logger.Debug("Play count " + count);
                    }
                    break;

                case "Playback_Paused":
                    {
                       

                        IsPaused = true;

                        Session.State = PlaybackState.Paused;
                        OnPlaybackPaused();
                    }
                    break;
                case "Playback_StartDisplay":
                    {
                        OnPlaybackStartDisplay();
                    }
                    break;
                case "Playback_StopDisplay":
                    {
                        OnPlaybackStopDisplay();
                    }
                    break;
                case "Playback_Stopped":
                    {
                       

                        IsStopped = true;

                        Session.State = PlaybackState.Stopped;
                        OnPlaybackStopped();

                    }
                    break;
                case "Playback_LengthChanged":
                    {
                        //_OnStateChanged("Playback_LengthChanged", command.args);
                        
                        var val0 = command.args[0].ToString();
                        long len = 0;
                        if (long.TryParse(val0, out len))
                        {
                            TimeSpan totalTime = TimeSpan.FromMilliseconds(len);
                            //if (totalTime != Session.TotalTime)
                            {
                                Session.TotalTime = totalTime;
                                OnPlaybackLengthChanged(len);
                            }

                        }
                    }
                    break;
                case "Playback_Position":
                    {
                        //_OnStateChanged("Playback_Position", command.args);

                        var val0 = command.args[0].ToString();
                        float position = 0;
                        if (float.TryParse(val0, out position))
                        {
                            if (Session.Position != position)
                            {
                                Session.Position = position;
                                OnPlaybackPositionChanged(position);
                            }

                        }

                        //owner.SetPosition(val0);
                    }
                    break;
                default:
                    {

                    }
                    break;
            }
        }


        public void Play(string uri, bool forse = false)
        {
            logger.Debug("Play(...) " + uri);

            if (IsConnected)
            {
                if (/*IsPaused ||*/ IsStopped || forse)
                {
                    EnqueueCommand("Playback_RunCommand", new object[] { "Play", new[] { uri } });
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

        public void RunPlaybackCommand(string msg, object[] args)
        {
            EnqueueCommand("Playback_RunCommand", new object[] { msg, args });
        }

        public void SetPosition(double position)
        {
            EnqueueCommand("Playback_RunCommand", new object[] { "Position", new object[] { position } });
        }

        public void SetMute(bool mute)
        {
            EnqueueCommand("Playback_RunCommand", new object[] { "Mute", new object[] { mute } });
        }

        public void SetVideoAdjustments(object [] adjustments)
        {
            EnqueueCommand("Playback_RunCommand", new object[] { "SetAdjustments", adjustments });
        }
        
        public void SetVolume(int volume)
        {
            EnqueueCommand("Playback_RunCommand", new object[] { "Volume", new object[] { volume } });
        }


        public void Pause()
        {
            logger.Debug("Pause()");

            if (IsConnected && IsOpened)
            {
                EnqueueCommand("Playback_RunCommand", new[] { "Pause" });
            }
        }

        public void Stop()
        {
            logger.Debug("Stop()");

            if (IsConnected && started)
            {
                EnqueueCommand("Playback_RunCommand", new[] { "Stop" });
            }
            else
            {
                Close();
            }
        }


        private bool started = false;
        private void StartUp(string mri,  string [] vlcopts = null)
        {
            logger.Debug("StartUp(...) " + mri);
            try
            {

                CleanUp();

                int tryCount = 5;
                while (tryCount-- > 0)
                {
                    try
                    {
                        //Thread.Sleep(5000);
                        logger.Debug("Try to open " + address);
                        OpenCommunicationHost(address);
                        break;
                    }
                    catch (AddressAlreadyInUseException ex)
                    {
                        logger.Error(ex);

                        CloseCommuntcationHost();

                        address += "_" + Guid.NewGuid();

                        Thread.Sleep(100);
                        continue;
                    }
                }

                IsOpened = true;
                IsStopped = true;

                if (vlcopts == null)
                {
                    vlcopts = new string[] 
                    {
                        "--extraintf=logger",
                        "--verbose=0",
                        "--network-caching=1000",
                     };
                }

                var args = new string[] 
                {
                    "--channel=\"" + address + "\"",
                    "--media=\"" + mri + "\"",
                    "--parentid=\"" + Process.GetCurrentProcess().Id + "\"",
                    "--vlcopts=\"" + string.Join(" ", vlcopts) + "\"",
                    //"--eventid=\"" + syncEventId + "\"",
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
                //Thread.Sleep(50000);
                started = true;
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

        private void PlaybackProcess_Exited(object sender, EventArgs e)
        {
            logger.Debug("PlaybackProcess_Exited(...)");

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

            logger.Debug("OpenCommunicationHost(...) " + address);

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

            EnqueueCommand("Ipc_Opened", new object[] { sender });
        }

        private void service_Closed(object sender, EventArgs e)
        {
            logger.Debug("service_Closed(...)");
            EnqueueCommand("Ipc_Closed");
        }

        private void service_Faulted(object sender, EventArgs e)
        {
            logger.Debug("service_Faulted(...)");

            EnqueueCommand("Ipc_Faulted");
        }


        bool ICommunicationService.Connect(string id, object[] args)
        {
            logger.Debug("ICommunicationService.Connect(...) " + id);

            bool Result = false;
            try
            {
                var channel = OperationContext.Current.GetCallbackChannel<ICommunicationCallback>();
                EnqueueCommand("Ipc_ClientConnected", new object[] { id, channel, args });
                Result = true;

            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Result = false;
            }

            return Result;

        }


        void ICommunicationService.Disconnect()
        {
            logger.Debug("ICommunicationService.Disconnect()");

            EnqueueCommand("Ipc_ClientDisconnected");

        }

        void ICommunicationService.PostMessage(string command, object[] args)
        {

            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            EnqueueCommand("Playback_" + command, args);

        }

        private void RunCommand(string cmd, object[] args)
        {
            if (closing)
            {
                return;
            }

            if (playbackChannel == null)
            {
                return;
            }

            try
            {
                playbackChannel.PostMessage(cmd, args);
                //Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
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



        public void Close()
        {
            logger.Debug("Close()");

            closing = true;
            commandQueue.Clear();
            syncEvent.Set();

            if (!started)
            {
                CleanUp();
            }

        }


        private void CleanUp()
        {
            logger.Debug("CleanUp()");
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
            logger.Debug("ClosePlaybackProccess()");

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

        private void CloseCommuntcationHost()
        {
            logger.Debug("CloseCommuntcationHost()");

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
                    logger.Error(ex);
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
