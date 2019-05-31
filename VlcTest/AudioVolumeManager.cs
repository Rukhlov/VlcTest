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

    class AudioVolumeManager
    {

        public AudioVolumeManager()
        {
            using (var deviceEnumerator = new MMDeviceEnumerator())
            {
                device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            }

            this.Session = new List<AudioVolumeController>();


        }

        public void Update()
        {

            this.Session.Clear();

            var sessions = device?.AudioSessionManager?.Sessions;
            if (sessions == null)
            {
                return;
            }

            AudioVolumeController deviceController = new AudioVolumeController(this.Device);
            this.Session.Add(deviceController);

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.State != AudioSessionState.AudioSessionStateExpired)
                {
                   
                    if (!session.IsSystemSoundsSession)
                    {
                        var pid = session.GetProcessID;
                        if (pid > 0)
                        {
                            try
                            {
                                var process = Process.GetProcessById((int)pid);

                               // if (process.MainWindowHandle != IntPtr.Zero)
                                {
                                    AudioVolumeController _session = new AudioVolumeController(this.device, session);

                                    Session.Add(_session);
                                }
                            }
                            catch (ArgumentException)
                            {

                            }
                        }
                    }
                    else
                    {
                        AudioVolumeController _session = new AudioVolumeController(this.device, session);

                        Session.Add(_session);

                    }
                }

            }

        }

        private MMDevice device;
        public MMDevice Device
        {
            get
            {
                return device;
            }
        }

        public readonly List<AudioVolumeController> Session;


        private bool ProcessExists(uint processId)
        {
            try
            {
                var process = Process.GetProcessById((int)processId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (device != null)
            {
                device.Dispose();
            }
        }

    }

    class AudioVolumeController : IAudioSessionEventsHandler
    {

        private readonly bool isDeviceSession = false;

        private readonly AudioSessionControl session;
        public readonly MMDevice Device;

        public AudioVolumeController(MMDevice device, AudioSessionControl session = null)
        {
            this.Device = device;         
            this.session = session;
            this.isDeviceSession = (session == null);

            this.Device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
            if (session != null)
            {
                Process process = Process.GetProcessById((int)session.GetProcessID);
                if (session.IsSystemSoundsSession)
                {

                }
                else
                {

                }

                session.RegisterEventClient(this);
            }
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Debug.WriteLine("AudioEndpointVolume_OnVolumeNotification(...) " + data.MasterVolume + " " + data.Muted);
        }

        public string DisplayName
        {
            get
            {
                var displayName = "Unknown";
                if (isDeviceSession)
                {
                    displayName =  this.Device.FriendlyName;
                }
                else
                {
                    displayName = session?.DisplayName;
                }
                return displayName;
            }
        }

        public event EventHandler DeviceChanged;
        public event EventHandler<MuteEventArgs> MuteChanged;
        public event EventHandler<VolumeEventArgs> VolumeChanged;

        public bool Mute
        {

            get
            {
                bool mute = false;
                if (!isDeviceSession)
                {
                    mute = session?.SimpleAudioVolume?.Mute ?? mute;
                }
                else
                {
                    mute = Device?.AudioEndpointVolume?.Mute ?? mute;
                }
                return mute;
            }
            set
            {
                bool muted = value;

                if (!isDeviceSession)
                {
                
                    session.SimpleAudioVolume.Mute = muted;
                    if (!muted)
                    {
                        Device.AudioEndpointVolume.Mute = false;
                    }
                }
                else
                {

                    Device.AudioEndpointVolume.Mute = muted;
                }

                UpdateMuted();

                if (MuteChanged != null)
                {
                    MuteChanged(this, new MuteEventArgs(muted));
                }
            }

        }

        public float Volume
        {
            get
            {
                float volume = -1;
                if (!isDeviceSession)
                {
                    volume = session?.SimpleAudioVolume?.Volume ?? volume;

                }
                else
                {
                    volume = Device?.AudioEndpointVolume?.MasterVolumeLevelScalar ?? volume;
                }
                return volume;
            }
            set
            {
                SetVolume(value);
            }
        }

        public void SetVolume(float volume)
        {
            if (!isDeviceSession)
            {
                var newVolume = volume / Device.AudioEndpointVolume.MasterVolumeLevelScalar;

                if (newVolume <= 1)
                {
                    session.SimpleAudioVolume.Volume = newVolume;
                }
                else
                {
                    Device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                    session.SimpleAudioVolume.Volume = 1;
                }
                session.SimpleAudioVolume.Mute = false;
            }
            else
            {
                Device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
            }

            Device.AudioEndpointVolume.Mute = false;

            if (VolumeChanged != null)
            {
                VolumeChanged(this, new VolumeEventArgs(volume));
            }

            UpdateMuted();

            if (MuteChanged != null)
            {
                MuteChanged(this, new MuteEventArgs(false));
            }
        }

        public void UpdateVolume()
        {
            if (!isDeviceSession)
            {
                float volume = session.SimpleAudioVolume.Volume;
                var Value = (int)(Math.Round(volume * Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
            }
            else
            {
                var Value = (int)(Math.Round(Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));

                if (VolumeChanged != null)
                {
                    VolumeChanged(null, new VolumeEventArgs(Device.AudioEndpointVolume.MasterVolumeLevelScalar));
                }
            }
        }

        public void UpdateMuted()
        {
            bool mute;
            if (!isDeviceSession)
            {
                mute = session.SimpleAudioVolume.Mute;
                if (Device.AudioEndpointVolume.Mute)
                {
                    mute = true;
                }
            }
            else
            {
                mute = Device.AudioEndpointVolume.Mute;
                if (MuteChanged != null)
                {
                    MuteChanged(null, new MuteEventArgs(Device.AudioEndpointVolume.Mute));
                }
            }

        }


        public void OnVolumeChanged(float volume, bool isMuted)
        {
            Debug.WriteLine("OnVolumeChanged(...) " + volume + " " + isMuted);


        }


        public void OnDisplayNameChanged(string displayName)
        {
            Debug.WriteLine("OnDisplayNameChanged(...) " + displayName);


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

        }

        public void OnStateChanged(NAudio.CoreAudioApi.Interfaces.AudioSessionState state)
        {
            Debug.WriteLine("OnGroupingOnStateChangedParamChanged(...) " + state);

        }

        public void OnSessionDisconnected(NAudio.CoreAudioApi.Interfaces.AudioSessionDisconnectReason disconnectReason)
        {
            Debug.WriteLine("OnSessionDisconnected(...) " + disconnectReason);
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
