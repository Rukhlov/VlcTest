using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using VlcContracts;

namespace VlcTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();


            //TryToOpen();
            CreateVideoForm();

            videoForm.Visible = true;

            UpdateUi();

            timer.Interval = 1000;

            timer.Tick += (o, a) =>
            {
                int currPosition = trackBarPosition.Value;
                //tick += 500;
                // UpdatePosition();

                if (currPosition != prevPosition)
                {
                    UpdatePosition();
                }
            };
        }

        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();


        private CommunicationService communicationService = null;
        private string address = CommunicationConst.PipeAddress;

        private void TryToOpen()
        {
            try
            {
                if (communicationService != null)
                {
                    communicationService.Close();
                }

                communicationService = new CommunicationService(this);
                communicationService.StateChanged += new Action<string>(communicationService_StateChanged);

                int tryCount = 5;
                while (tryCount-- > 0)
                {
                    try
                    {
                        Log("Try to open " + address);
                        communicationService.Open(address);
                        break;
                    }
                    catch (AddressAlreadyInUseException)
                    {
                        communicationService.Close();

                        address += "_" + Guid.NewGuid();
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {

                Log(ex.Message);
                CleanUp();
            }

        }
        public bool IsOpened
        {
            get
            {
                return (communicationService != null && communicationService.IsOpened);
            }
        }

        public bool IsConnected
        {
            get
            {
                return (communicationService != null && communicationService.IsConnected);
            }
        }

        private void communicationService_StateChanged(string state)
        {
            Log(state);

            UpdateUi();
        }

        private bool IsStopped = true;
        private bool IsPaused = false;

        private void UpdateUi()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    UpdateUi();

                }));
            }
            else
            {
                trackBarPosition.Enabled = !IsStopped;
                if (IsStopped || !IsConnected)
                {
                    labelCurrentTime.Text = "--:--";
                    labelTotalTime.Text = "--:--";
                    trackBarPosition.Value = 0;
                }

                buttonPlay.Text = (IsPaused || IsStopped || !IsConnected) ?  "Play" : "Pause";


            }





        }

        private Form2 videoForm = null;
        public void ShowVideoForm(object []args)
        {
            this.Invoke((Action)(() =>
            {
                //CreateVideoForm();

                videoForm.Visible = true;

                videoForm.StartRender(args);

            }));


        }

        private void CreateVideoForm()
        {
            if (videoForm == null || videoForm.IsDisposed || videoForm.Disposing)
            {

                videoForm = new Form2();

                videoForm.FormClosing += (o, a) =>
                {
                    a.Cancel = true;
                    videoForm.Visible = false;
                    PostMessage("Stop");
                };

                //videoForm.FormClosed += (o, a) =>
                //{
                //    PostMessage("Stop");
                //};
            }
        }

        internal void Log(string msg)
        {

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() =>
                {
                    Log(msg);
                }));
            }
            else
            {
                Debug.WriteLine(msg);

                string text = DateTime.Now.ToString("HH:mm:ss.fff") + " >> " + msg + Environment.NewLine;
                textBox1.AppendText(text);
            }

        }

        private void CleanUp()
        {
            if (communicationService != null)
            {
                communicationService.Close();
            }

            UpdateUi();
        }
        private static string currentDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        private Process clientProcess = null;

        async private Task<string> GetYouTubeFileLink(string url)
        {
            var profiles = await Task.Factory.StartNew<List<List<string>>>(() =>
            {
                return YoutubeUrlResolver.Extractor(url);
            });

            foreach (var p in profiles)
            {
                Log("---------------------------------");
                foreach (var r in p)
                {
                    Log(r);
                }
                Log("---------------------------------");
            }

            string link = "";
            if (profiles != null)
            {
                var profile = profiles.FirstOrDefault();
                if (profile != null)
                {
                    link = profile.FirstOrDefault();
                }
            }

            return link;
        }

        private static Process CurrentProccess = Process.GetCurrentProcess();

        private void buttonPlay_Click(object sender, EventArgs e)
        {                
            ////string fileName = currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";
            //string fileName = "";//currentDirectory + @"\Test\AV_60Sec_30Fps.mkv";
            var fileName = this.textBox2.Text;

            CreateVideoForm();

            StartPlayerProc(fileName);
        }

        private EventWaitHandle globalSyncEvent = null;

        private EventWaitHandle CreateWaitHandle(string Name)
        {

            EventWaitHandle handle = null;

            // create a rule that allows anybody in the "Users" group to synchronise with us
            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var rule = new EventWaitHandleAccessRule(users,
                EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
                                        AccessControlType.Allow);

            var security = new EventWaitHandleSecurity();
            security.AddAccessRule(rule);
            bool created;
            handle = new EventWaitHandle(false, EventResetMode.AutoReset, Name, out created, security);


            return handle;
        }

        private void StartPlayerProc(string fileName)
        {
            try
            {
                string syncEventId = Guid.NewGuid().ToString("N");

                //if (globalSyncEvent != null)
                //{
                //    globalSyncEvent.Dispose();
                //    globalSyncEvent = null;
                //}

                //globalSyncEvent = CreateWaitHandle(syncEventId);

                TryToOpen();

                if (!this.IsOpened)
                {

                    return;
                }

                IsStopped = true;

                CloseClientProccess();
                var _vlcopts = new string[] {"--extraintf=logger", "--verbose=0" , "--network-caching=1000" };
                string vlcopts = string.Join(" ", _vlcopts);
                var args = new string [] { "--channel=\"" + address + "\"",
                                           "--media=\"" + fileName + "\"",
                                           "--parentid=\"" + CurrentProccess.Id + "\"",
                                           "--evendid=\"" + syncEventId + "\"",
                                           "--vlcopts=\"" + vlcopts + "\"",
                                         };

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "VlcPlayer.exe",
                    Arguments = string.Join(" ", args), //address + " \"" + fileName + "\" " + CurrentProccess.Id + " " + syncEventId,
                    //RedirectStandardError = true,
                    //RedirectStandardOutput = true,
                    //UseShellExecute = false,
                };


                clientProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                clientProcess.Exited += ClientProcess_Exited;
                //clientProcess.OutputDataReceived += ClientProcess_OutputDataReceived;
                //clientProcess.ErrorDataReceived += ClientProcess_ErrorDataReceived;
                playbackStarting = true;

                clientProcess.Start();
              

                Log("Client proccess started...");
            }
            catch (Exception ex)
            {
                //buttonStartClientInstance.Enabled = true;
                playbackStarting = false;
                Log(ex.Message);
            }
            finally
            {
                //processStarting = false;
            }
        }

        private void ClientProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Debug.WriteLine(e.Data);
        }

        private void ClientProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Debug.WriteLine(e.Data);
        }

        private void CloseClientProccess()
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
                Log(ex.Message);
                //Debug.Fail(ex.Message);
            }
            finally
            {
                //if (clientProcess != null)
                //{
                //    clientProcess.Dispose();
                //    clientProcess = null;
                //}

                UpdateUi();
            }
        }

        private void ClientProcess_Exited(object sender, EventArgs e)
        {
            Process p = sender as Process;
            if (p != null)
            {
                int code = p.ExitCode;
                Log("Client process exited with code: " + code);
                p.Dispose();

                playbackStarting = false;
            }

            communicationService?.Close();

            videoForm?.StopRender();
            videoForm.videoControl1.Clear();
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (communicationService != null)
                {
                    communicationService.Close();
                    communicationService = null;
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            finally
            {
                CloseClientProccess();
            }
        }




        private void PostMessage(string command, object[] args = null)
        {
            if (communicationService == null) return;
            //string text = textBox3.Text;
            //var client = comboBox1.SelectedValue as ICommunicationCallback;

            //try
            //{
            //Task.Factory.StartNew(() => service.OneWaySend(client, DateTime.Now.ToString("HH:mm:ss.fff")));
            Task task = new Task(() =>
            {
                communicationService.PostMessage(command, args);
            });

            task.ContinueWith(t =>
            {

                if (!t.IsCanceled && !t.IsFaulted)
                {

                }

                if (t.IsFaulted)
                {
                    string error = "Unknown error!";
                    var ex = t.Exception;
                    if (ex != null)
                    {
                        var iex = ex.InnerException;
                        if (iex != null)
                        {
                            error = iex.Message;
                        }
                    }
                    Log(error);
                    //CleanUp();
                }


            }, TaskScheduler.FromCurrentSynchronizationContext());



            task.Start();
        }


        //[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
        class CommunicationService : ICommunicationService
        {
            private Form1 owner = null;

            private ServiceHost service = null;

            private ICommunicationCallback communicationClient = null;

            // public Dictionary<ICommunicationCallback, string> clients = new Dictionary<ICommunicationCallback, string>();

            public bool IsOpened = false;

            public bool IsConnected
            {
                get { return (communicationClient != null); }
            }

            public string Name = "";
            public event Action<string> StateChanged;

            private int seq = 0;
            private void OnStateChanged(string state)
            {
                StateChanged?.Invoke(state);

            }

            public CommunicationService(Form1 o)
            {
                this.owner = o;

            }

            enum TransportType
            {
                Pipe,
                Tcp,
            }

            public void Open(string address)
            {
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
                IsOpened = true;

                var serv = (ServiceHost)sender;
                Name = "Server: " + string.Join(";", serv.BaseAddresses.Select(a => a.ToString()));

                OnStateChanged("Service openned...");
            }

            private void service_Closed(object sender, EventArgs e)
            {
                Name = "";

                IsOpened = false;
                communicationClient = null;
                OnStateChanged("Service closed...");
            }

            private void service_Faulted(object sender, EventArgs e)
            {
                communicationClient = null;
                OnStateChanged("Service fauled...");
            }

            public void Close()
            {
                if (service != null)
                {
                    try
                    {
                        if (service.State != CommunicationState.Closed)
                        {
                            //service.Close();
                            service.Abort();
                            communicationClient = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        service.Abort();
                    }

                    service.Opened -= new EventHandler(service_Opened);
                    service.Faulted -= new EventHandler(service_Faulted);
                    service.Closed -= new EventHandler(service_Closed);

                }
                IsOpened = false;

            }

            bool ICommunicationService.Connect(string id, object[] args)
            {
                bool Result = true;
                try
                {

                    if (communicationClient != null)
                    {
                        Result = false;
                    }
                    else
                    {
                        //throw new Exception("CONNECTION_ERROR!");
                        //Thread.Sleep(15000);

                        communicationClient = OperationContext.Current.GetCallbackChannel<ICommunicationCallback>();
                        ((IClientChannel)communicationClient).Closed += (o, a) =>
                        {
                            communicationClient = null;
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
                try
                {
                    if (communicationClient != null)
                    {
                        OnStateChanged("Client disconnected...");
                        communicationClient = null;
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

                owner.Log(message);

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

                        owner.Log("Begin process message " + "\"" + msg + "\"");

                        Thread.Sleep(5000);
                        owner.Log("End process message: " + "\"" + msg + "\"");

                        return "OK Client (" + msg + ")";

                    });
                }

                return messageProc.BeginInvoke(message, cb, state);
            }

            object ICommunicationService.EndSendMessage1(IAsyncResult result)
            {
                return messageProc.EndInvoke(result);
            }

            internal long totalMediaLength = 0;
            float mediaPosition = 0;
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
                        owner.playbackStarting = false;
                        count++;
                        owner.IsPaused = false;
                        owner.IsStopped = false;
                        owner.UpdateUi();

                        //owner.Invoke(new Action(() =>
                        //{
                        //    owner.trackBarPosition.Enabled = !owner.IsStopped;
                        //    owner.buttonPlay.Text = "Pause";

                        //}));

                        owner.Log("Play count " + count);
                    }
                    else if(command == "VideoFormat")
                    {

                        owner.ShowVideoForm(args);//videoControl?.Open(args);

                    }
                    else if (command == "CleanupVideo")
                    {

                        owner.videoForm.StopRender();

                        //owner.(args);//videoControl?.Open(args);

                    }
                    else if (command == "Paused")
                    {
                        owner.IsPaused = true;
                        owner.UpdateUi();

                        //TimeSpan ts = TimeSpan.Zero;
                        //owner.Invoke(new Action(() =>
                        //{

                        //    owner.buttonPlay.Text = "Play";

                        //}));
                    }
                    else if (command == "Stopped")
                    {
                        owner.IsStopped = true;

                        owner.videoForm.StopRender();
                        owner.videoForm.videoControl1.Clear();

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
                        if (long.TryParse(val0, out totalMediaLength))
                        {
                            TimeSpan ts = TimeSpan.FromMilliseconds(totalMediaLength);

                            owner.Invoke(new Action(() =>
                            {
                                owner.labelTotalTime.Text = ts.ToString("hh\\:mm\\:ss");

                            }));
                        }
                    }
                    else if (command == "Position")
                    {
                        //var val1 = "";
                        //if (args?.Length > 1)
                        //{
                        //    val1 = args[1]?.ToString();
                        //}
                        //owner.Log("Position " + val0 + " " + val1);

                        if (float.TryParse(val0, out mediaPosition))
                        {
                            long currTime = (long)(totalMediaLength * mediaPosition);
                            TimeSpan ts = TimeSpan.FromMilliseconds(currTime);

                            owner.Invoke(new Action(() =>
                            {
                                
                                if (!owner.mouseDown)
                                {
                                    owner.labelCurrentTime.Text = ts.ToString("hh\\:mm\\:ss");

                                    var pos = (int)(mediaPosition * owner.trackBarPosition.Maximum);
                                    if (owner.trackBarPosition.Value != pos)
                                    {
                                        owner.trackBarPosition.Value = pos;
                                    }
                                }
                            }));
                        }
                    }



                }
                catch (Exception ex)
                {
                    owner.Log("Error: " + ex.ToString());
                }
            }


            public void PostMessage(string msg, object[] args)
            {

                //owner.Log("PostMessage BEGIN");
                try
                {
                    //Thread.Sleep(3000);
                    if (communicationClient != null)
                    {

                        seq++;
                        string text = msg; // + " seq " + seq;
                        owner.Log("Server post: " + text);
                        communicationClient.PostMessage(text, args);
                        //Thread.Sleep(2000);


                    }
                }
                catch (Exception ex)
                {
                    owner.Log("Server end post error: " + ex.Message);
                    throw;
                }
                //owner.Log("PostMessage END");
            }



        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        async private void button1_Click(object sender, EventArgs e)
        {
            var url = this.textBox2.Text;
            if (!string.IsNullOrEmpty(url))
            {
                var link = await GetYouTubeFileLink(url);
            }
        }

        private void elementHost2_ChildChanged(object sender, System.Windows.Forms.Integration.ChildChangedEventArgs e)
        {

        }

        private void Test()
        {
            //this.userControl11.SetBinding();
        }

        private void button2_Click(object sender, EventArgs e)
        {

            //waitHandle = EventWaitHandle.OpenExisting("Vlc.DotNet.Wfp");


            //mmf = MemoryMappedFile.OpenExisting("Vlc.DotNet.Wfp_Mmf");

            //userControl11.Setup(mmf.SafeMemoryMappedFileHandle.DangerousGetHandle());

            //ImageSource videoSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(mmf.SafeMemoryMappedFileHandle.DangerousGetHandle(), 
            //    1280, 960, PixelFormats.Bgr32, 1280 * 24, 0);

            //userControl11.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VlcVideoProvider.VideoSource)) { Source = videoSource });

            //userControl11.SetBinding(mmf.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1280, 960, 0);
        }

        //private void button3_Click(object sender, EventArgs e)
        //{
        //    waitHandle = EventWaitHandle.OpenExisting("Vlc.DotNet.Wfp");

        //    userControl11.Invalidate();
        //    Task.Run(() => 
        //    {

        //        while (true)
        //        {

        //            userControl11.Invalidate();


        //            waitHandle?.WaitOne(1000);

        //        }


        //    });
        //}

        private volatile bool playbackStarting = false;
        private void buttonPlay_Click_1(object sender, EventArgs e)
        {
            //if (playbackStarting)
            //{
            //    Debug.WriteLine("processStarting " + playbackStarting);
            //    return;
            //}

            currentMediaFile = textBox2.Text;
            if (string.IsNullOrEmpty(currentMediaFile))
            {
                OpenFileDialog dlg = new OpenFileDialog();

                var result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    currentMediaFile = dlg.FileName;
                    textBox2.Text = currentMediaFile;
                }
            }


            PlayFile();
        }

        private void PlayFile(bool forse = false)
        {
            CreateVideoForm();
            if (IsConnected)
            {
                if (/*IsPaused ||*/ IsStopped || forse)
                {
                    PostMessage("Play", new[] { currentMediaFile });
                }
                else
                {
                    PostMessage("Pause");
                }
            }
            else
            {
                var fileName = this.textBox2.Text;

                StartPlayerProc(fileName);
            }
        }

        private void buttonPause_Click(object sender, EventArgs e)
        {
            PostMessage("Pause");
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                PostMessage("Stop");
            }
            else
            {
                CloseClientProccess();
            }


        }

        private string currentMediaFile = "";
        private void buttonOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                currentMediaFile = dlg.FileName;

                textBox2.Text = currentMediaFile;

                if (File.Exists(currentMediaFile))
                {
                    PlayFile(true);

                    //if (IsPaused || IsStopped)
                    //{
                    //    CreateVideoForm();

                    //    PostMessage("Play", new[] { currentMediaFile });
                    //}
                }
            }
        }

        private void checkBoxMute_CheckedChanged(object sender, EventArgs e)
        {
            PostMessage("Mute", new object[] { checkBoxMute.Checked });

        }

        private void trackBarVolume_Scroll(object sender, EventArgs e)
        {

        }


        private int prevPosition = 0;
        private volatile bool mouseDown = false;
        private void trackBarPosition_MouseUp(object sender, MouseEventArgs e)
        {
            int currPosition = trackBarPosition.Value;

            timer.Enabled = false;
            mouseDown = false;

            if (currPosition != prevPosition)
            {
                UpdatePosition();
            }

            prevPosition = currPosition;
        }

        private void UpdatePosition()
        {
            var position = trackBarPosition.Value / (double)(trackBarPosition.Maximum - trackBarPosition.Minimum);

            long currTime = (long)(communicationService.totalMediaLength * position);
            TimeSpan ts = TimeSpan.FromMilliseconds(currTime);

            labelCurrentTime.Text = ts.ToString("hh\\:mm\\:ss");

            PostMessage("Position", new object[] { position });
        }

        private void trackBarPosition_MouseDown(object sender, MouseEventArgs e)
        {
            timer.Enabled = true;
            mouseDown = true;
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            if (mouseDown)
            {
                //var position = trackBarPosition.Value / (double)(trackBarPosition.Maximum - trackBarPosition.Minimum);
                //PostMessage("Position;" + position + ";");
            }
        }

        private void trackBarVolume_ValueChanged(object sender, EventArgs e)
        {
            var val = (int)trackBarVolume.Value;

            PostMessage("Volume", new object[] { val });

        }

        private void trackBarBlur_ValueChanged(object sender, EventArgs e)
        {
            var val = (int)trackBarBlur.Value;
            label1.Text = val.ToString();
            PostMessage("Blur", new object[] { val });
        }

        private void labelCurrentTime_Click(object sender, EventArgs e)
        {

        }

        private void trackBarBrightness_ValueChanged(object sender, EventArgs e)
        {
            var val = trackBarBrightness.Value / 100.0; //(float)(trackBarBrightness.Maximum - trackBarBrightness.Minimum));

            PostMessage("Brightness", new object[] { val });
        }

        private void trackBarHue_ValueChanged(object sender, EventArgs e)
        {
            var val = (float)trackBarHue.Value;//(trackBarHue.Value / (float)(trackBarHue.Maximum - trackBarHue.Minimum));
            PostMessage("Hue", new object[] { val });

        }

        private void trackBarContrast_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarContrast.Value / 100.0); // (float)(trackBarContrast.Maximum - trackBarContrast.Minimum));

            PostMessage("Contrast", new object[] { val });
        }

        private void trackBarGamma_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarGamma.Value / 100.0); //(float)(trackBarGamma.Maximum - trackBarGamma.Minimum));

            //var val = (int)trackBarGamma.Value;

            PostMessage("Gamma", new object[] { val });
        }

        private void trackBarSaturation_ValueChanged(object sender, EventArgs e)
        {
            var val = (trackBarSaturation.Value / 100.0); // (float)(trackBarSaturation.Maximum - trackBarSaturation.Minimum));
                                                          //var val = (int)trackBarSaturation.Value;

            PostMessage("Saturation", new object[] { val });
        }

        private void checkBoxVideoAdjustments_CheckedChanged(object sender, EventArgs e)
        {
            var enable = checkBoxVideoAdjustments.Checked ? 1:0;
            PostMessage("SetVideoAdjustments", new object[] { enable });
        }

        private void buttonResetVideoAdjustments_Click(object sender, EventArgs e)
        {
            PostMessage("ResetVideoAdjustments");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //videoForm.Visible = true;
            PostMessage("SwitchVisibilityState", new object[] { checkBox1.Checked });
        }

        private void checkBoxLoopPlayback_CheckedChanged(object sender, EventArgs e)
        {
            PostMessage("SwitchLoopPlayback");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (communicationService != null)
            {
                communicationService.Close();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            PostMessage("GetStats");
        }

        private void button6_Click(object sender, EventArgs e)
        {
           // MemoryMappedFile mmf = MemoryMappedFile.OpenExisting("Vlc.DotNet.Wfp_Mmf");

           // userControl11.Video.
            //UserControl1.SetBinding(mmf.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1280, 960, 0);
        }
    }

}