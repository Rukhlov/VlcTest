using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VlcTest
{


    class AudioMixerManager
    {
        //public readonly List<AudioVolumeController> Controllers;
        object syncRoot = new object();
        public readonly List<AudioMixerItem> Items = new List<AudioMixerItem>();


        public AudioMixerManager()
        {
            //using (var deviceEnumerator = new MMDeviceEnumerator())
            //{
            //    foreach (var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            //    {
            //        devices.Add(device);
            //    }
            //}

            using (var deviceEnumerator = new MMDeviceEnumerator())
            {
                device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                //this.device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
            }
        }

       // private List<MMDevice> devices = new List<MMDevice>();

        private MMDevice device = null;

        public void Update()
        {
            ClearItems();

            AudioVolumeSession deviceSession = new AudioVolumeSession(device);
            deviceSession.VolumeChanged += DeviceSession_VolumeChanged;


            AudioMixerItem deviceItem = new AudioMixerItem(this, deviceSession);

            Items.Add(deviceItem);

            var sessions = device?.AudioSessionManager?.Sessions;
            if (sessions == null)
            {
                return;
            }

            for (int i = 0; i < sessions.Count; i++)
            {
                AudioVolumeSession mixerSession = new AudioVolumeSession(this.device, sessions[i]);

                if (mixerSession.IsEnabled)
                {
                    var groupId = mixerSession.GroupingParams;
                    AudioMixerItem groupItem = null;
                    if (groupId != Guid.Empty)
                    {
                        groupItem = Items.FirstOrDefault(s => s.GroupingParams == groupId);
                        if (groupItem != null)
                        {
                            groupItem.AddSession(mixerSession);
                        }
                    }

                    if (groupItem != null)
                    {
                        groupItem.AddSession(mixerSession);
                    }
                    else
                    {
                        AudioMixerItem item = new AudioMixerItem(this, mixerSession);
                        Items.Add(item);
                    }
                }
                else
                {
                    Debug.WriteLine("Session.IsEnabled "+ false);
                }
            }
        }

        private void DeviceSession_VolumeChanged(int _volume, bool _mute )
        {
            Debug.WriteLine("DeviceSession_VolumeChanged(...) " + _volume + " " + _mute);

            if (_volume >= 0)
            {
                foreach (var item in Items)
                {
                    item.UpdateVolume();
                }
            }
        }


        private void ClearItems()
        {
            for(int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                item.Dispose();
                item = null;
            }

            this.Items.Clear();
        }

        public void Dispose()
        {
            ClearItems();


            if (device != null)
            {

                device.Dispose();
            }
     
        }

    }


    class AudioMixerItem
    {
        private readonly List<AudioVolumeSession> sessions = new List<AudioVolumeSession>();
        private AudioMixerManager manager;

        public readonly Guid GroupingParams = Guid.Empty;
        public AudioMixerItem(AudioMixerManager man, AudioVolumeSession sess)
        {
            this.manager = man;

            if (sess.GroupingParams != Guid.Empty)
            {
                GroupingParams = sess.GroupingParams;
            }

            Name = sess.DisplayName;

            volume = sess.GetVolumeLevel();

            mute = sess.Mute;

            AddSession(sess);

        }

        public string Name { get; private set; } = "Unknown";

        private int volume = 0;
        public int Volume
        {
            get
            {
                return volume;
            }

            set
            {
                if (volume != value)
                {
                    int _volume = value;

                    float _sessionVolume = _volume / 100.0f;

                    foreach (var sess in sessions)
                    {

                        sess.Volume = _sessionVolume;
                    }
                }
            }
        }

        private bool mute = false;
        public bool Mute
        {
            get
            {
                return mute;
            }

            set
            {
                if (mute != value)
                {
                    bool _mute = value;
                    foreach (var sess in sessions)
                    {
                        sess.Mute = _mute;
                    }
                }
            }
        }

 
        public void AddSession(AudioVolumeSession session)
        {
            if (session != null)
            {
                if (session.GroupingParams == this.GroupingParams)
                {
                    sessions.Add(session);
                    session.VolumeChanged += Session_VolumeChanged;
                }
            }
        }

        public event Action<int> VolumeChanged;
        public event Action<bool> MuteChanged;

        private void Session_VolumeChanged(int _volume, bool _mute)
        {
            Debug.WriteLine("Session_VolumeChanged(...) " + _volume + " "  + mute + " "+ this.Name);

            if (_volume >= 0)
            {
                if (_volume != volume)
                {
                    volume = _volume;

                    VolumeChanged?.Invoke(volume);
                }
            }


            if (_mute != mute)
            {
                mute = _mute;

                MuteChanged?.Invoke(mute);
            }
        }


        public void RemoveSession(AudioVolumeSession session)
        {
            if (session != null)
            {
                session.VolumeChanged -= Session_VolumeChanged;
                sessions.Remove(session);
            }
        }

        public void UpdateVolume()
        {
            Debug.WriteLine("UpdateVolume() " + this.Name);

            foreach(var sess in sessions)
            {

                sess.UpdateVolume();
            }
        }


        public void Dispose()
        {
            for(int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];

                session.VolumeChanged -= Session_VolumeChanged;

                session.CleanUp();
                session = null;
            }

            sessions.Clear();
            manager = null;

        }
    }



    class AudioVolumeSession : IAudioSessionEventsHandler
    {

        private readonly bool isDeviceSession = false;

        private readonly AudioSessionControl session;
        public readonly MMDevice Device;

        public AudioVolumeSession(MMDevice device, AudioSessionControl session = null)
        {
            this.Device = device;         
            this.session = session;

            this.isDeviceSession = (session == null);

            Setup();
        }


        public bool IsEnabled { get; private set; } = false;

        public int ProcessId { get; private set; } = -1;

        public Guid GroupingParams { get; private set; } = Guid.Empty;
        public string DisplayName { get; private set; } = "";

        private bool mute = false;
        public bool Mute
        {

            get
            {
                return mute;
            }
            set
            {
                if (mute != value)
                {
                    bool _mute = value;
                   // mute = value;

                    if (!isDeviceSession)
                    {
                        session.SimpleAudioVolume.Mute = _mute;

                        if (!_mute)
                        {
                            Device.AudioEndpointVolume.Mute = false;
                        }
                    }
                    else
                    {

                        Device.AudioEndpointVolume.Mute = _mute;
                    }
                }
            }

        }

        private float volume = 0;
        public float Volume
        {
            get
            {
                return volume;
            }
            set
            {
                if (volume != value)
                {
                    //volume = value;

                    float _volume = value;
                    if (!isDeviceSession)
                    {
                        var newVolume = _volume / Device.AudioEndpointVolume.MasterVolumeLevelScalar;

                        if (newVolume <= 1)
                        {
                            session.SimpleAudioVolume.Volume = newVolume;
                        }
                        else
                        {
                            Device.AudioEndpointVolume.MasterVolumeLevelScalar = _volume;
                            session.SimpleAudioVolume.Volume = 1;
                        }
                        session.SimpleAudioVolume.Mute = false;
                    }
                    else
                    {
                        Device.AudioEndpointVolume.MasterVolumeLevelScalar = _volume;
                    }

                    Device.AudioEndpointVolume.Mute = false;
                }
            }
        }


        public int GetVolumeLevel()
        {
            int vol = -1;

            if (!isDeviceSession)
            {
                vol = (int)(Math.Round(volume * Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
            }
            else
            {
                vol = (int)(Math.Round(volume * 100));
            }


            return vol;
        }


        public void UpdateVolume()
        {
            if (!isDeviceSession)
            {

                float _volume = session.SimpleAudioVolume.Volume; 
                bool _mute = session.SimpleAudioVolume.Mute;

                if (Device.AudioEndpointVolume.Mute)
                {
                    _mute = true;
                }

                OnVolumeChanged(_volume, _mute);
            }
            else
            {
                //...
            }
        }

        public void Setup()
        {
            Debug.WriteLine("Setup()");

            if (!isDeviceSession)
            {
                IsEnabled = (session.State != AudioSessionState.AudioSessionStateExpired);
            }
            else
            {
                IsEnabled = (Device.State == DeviceState.Active);
            }

            if (!IsEnabled)
            {
                Debug.WriteLine("IsEnabled " + IsEnabled);
                return;
            }


            if (!isDeviceSession)
            {

                if (!session.IsSystemSoundsSession)
                {

                    ProcessId = (int)session.GetProcessID;

                    if (ProcessId > 0)
                    {

                        try
                        {
                           
                            DisplayName = session.DisplayName;
  
                            var process = Process.GetProcesses().FirstOrDefault(p => p.Id == ProcessId);//Process.GetProcessById(ProcessId);

                            if (process != null)
                            {
                                //process.EnableRaisingEvents = true;

                                //process.Exited += (o, a) =>
                                //{
                                //    Debug.WriteLine("process.Exited ");
                                //};

                                if (string.IsNullOrEmpty(DisplayName))
                                {
                                    if (process.MainWindowHandle != IntPtr.Zero)
                                    {
                                        DisplayName = process.MainWindowTitle;
                                    }
                                }

                                if (string.IsNullOrEmpty(DisplayName))
                                {
                                    DisplayName = process.ProcessName;
                                }
                            }
                            else
                            {
                                //...

                                IsEnabled = false;
                            }

                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);

                            IsEnabled = false;
                        }

                    }
                    else
                    {
                        IsEnabled = false;
                    }

                    if (!IsEnabled)
                    {

                        return;
                    }

                    GroupingParams = session.GetGroupingParam();             
                }
                else
                {
                    DisplayName = "System Sounds";
                    var identifier = session.GetSessionIdentifier;

                }

                mute = session.SimpleAudioVolume.Mute;
                volume = session.SimpleAudioVolume.Volume;

                session.RegisterEventClient(this);

            }
            else
            {

                this.Device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;

                DisplayName = this.Device.FriendlyName;

                mute = this.Device.AudioEndpointVolume.Mute;
                volume = Device.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
        }


        public void CleanUp()
        {

            if (session != null)
            {
                session.UnRegisterEventClient(this);
            }

            if (isDeviceSession)
            {
                if (this.Device.AudioEndpointVolume != null)
                {
                    this.Device.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                }
            }
        }



        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {

            if (isDeviceSession)
            {
                OnVolumeChanged(data.MasterVolume, data.Muted);

            }
            else
            {
                //...
                Debug.WriteLine("!!!!!!!!!!!!!!! AudioEndpointVolume_OnVolumeNotification(...) " + isDeviceSession);
            }
        }


        public event Action<int, bool> VolumeChanged;

        public void OnVolumeChanged(float _volume, bool _mute)
        {
            Debug.WriteLine("OnVolumeChanged(...) " + _volume + " " + _mute + " " + this.DisplayName );

            int volumeLevel = -1;
            //if( this.volume!=_volume)
            {
                this.volume = _volume;

                if (!isDeviceSession)
                {
                    volumeLevel = (int)(Math.Round(volume * Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
                }
                else
                {
                    volumeLevel = (int)(Math.Round(Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
                }
            }

            //if(_mute!= this.mute)
            {
                this.mute = _mute;

               // MuteChanged?.Invoke(null, new MuteEventArgs(this.mute));
            }

            VolumeChanged?.Invoke(volumeLevel, _mute);

        }


        public void OnDisplayNameChanged(string displayName)
        {
            Debug.WriteLine("OnDisplayNameChanged(...) " + displayName);

            this.DisplayName = displayName;

        }

        public void OnIconPathChanged(string iconPath)
        {
            Debug.WriteLine("OnIconPathChanged(...) " + iconPath);

        }

        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
        {
            Debug.WriteLine("OnChannelVolumeChanged(...) " + channelCount + " " + newVolumes + " " + channelIndex);

        }

        public void OnGroupingParamChanged(ref Guid groupingId)
        {
            Debug.WriteLine("OnGroupingParamChanged(...) " + groupingId);

            this.GroupingParams = groupingId;


        }

        public void OnStateChanged(NAudio.CoreAudioApi.Interfaces.AudioSessionState state)
        {
            Debug.WriteLine("OnGroupingOnStateChangedParamChanged(...) " + state + " " + this.DisplayName);

            IsEnabled = state != (AudioSessionState.AudioSessionStateExpired);

        }


        public void OnSessionDisconnected(NAudio.CoreAudioApi.Interfaces.AudioSessionDisconnectReason disconnectReason)
        {
            Debug.WriteLine("OnSessionDisconnected(...) " + disconnectReason);

            this.CleanUp();
        }
    }


    public class IconExtractor
    {
        public static Icon Extract(string file, int number, bool largeIcon)
        {
            IntPtr large;
            IntPtr small;
            ExtractIconEx(file, number, out large, out small, 1);
            try
            {
                return Icon.FromHandle(largeIcon ? large : small);
            }
            catch
            {
                return null;
            }
        }

        [DllImport("Shell32.dll")]
        private static extern int ExtractIconEx(string sFile, int iIndex, out IntPtr piLargeVersion, out IntPtr piSmallVersion, int amountIcons);
    }
}
