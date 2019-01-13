using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;
using System.Runtime.Serialization;

namespace VlcContracts
{

    public class CommunicationConst
    {
        public readonly static string PipeAddress = @"net.pipe://localhost/IPlaybackService/Pipe";

        public readonly static string TcpLocalhostAddress = @"net.tcp://localhost/IPlaybackService/tcp";
        public readonly static string TcpAddress = @"net.tcp://192.168.10.158/IPlaybackService/tcp";

    }


    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ICommunicationCallback))]
    //[ServiceContract(SessionMode = SessionMode.Required)]
    public interface ICommunicationService
    {

        [OperationContract(IsInitiating = true)]
        object Connect(string id, object[] args);

        [OperationContract(IsTerminating = true)]
        void Disconnect();

        [OperationContract]
        object SendMessage(string command, object[] args);

        [OperationContractAttribute(AsyncPattern = true)]
        IAsyncResult BeginSendMessage1(string command, object[] args, AsyncCallback callback, object asyncState);
        object EndSendMessage1(IAsyncResult result);

        [OperationContract(IsOneWay = true)]
        void PostMessage(string command, object[] args);
    }

    public interface ICommunicationCallback
    {
        [OperationContract]
        object SendMessage(string command, object[] args);

        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginSendMessage1(string command, object[] args, AsyncCallback callback, object asyncState);

        object EndSendMessage1(IAsyncResult result);

        [OperationContract(IsOneWay = true)]
        void PostMessage(string command, object[] args);
    }


    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(IPlaybackClient))]
    public interface IPlaybackService: ICommunicationService
    {
        [OperationContract]
        PlaybackOptions GetPlaybackOptions();

        void SetPlaybackOptions(string option, object value);
    }

    public interface IPlaybackClient : ICommunicationCallback
    {

    }


    [DataContract]
    public class PlaybackOptions : System.ComponentModel.INotifyPropertyChanged
    {
        
        [DataMember]
        public int Volume
        {
            get { return volume; }
            set
            {
                volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }
        private int volume = 0;

        [DataMember]
        public bool IsMute
        {
            get { return isMute; }
            set
            {
                isMute = value;
                OnPropertyChanged(nameof(IsMute));
            }
        }
        private bool isMute = false;

        [DataMember]
        public bool LoopPlayback
        {
            get { return loopPlayback; }
            set
            {
                loopPlayback = value;
                OnPropertyChanged(nameof(LoopPlayback));
            }
        }
        private bool loopPlayback = false;

        [DataMember]
        public int BlurRadius
        {
            get { return blurRadius; }
            set
            {
                blurRadius = value;
                OnPropertyChanged(nameof(BlurRadius));
            }
        }
        private int blurRadius = 0;

        [DataMember]
        public bool VideoAdjustmentsEnabled { get; set; }

        [DataMember]
        public int VideoContrast { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }




    [ServiceContract(SessionMode = SessionMode.Required)]
    public interface _ICommunicationService
    {

        [OperationContract(IsInitiating = true)]
        void Connect(string id, int pid);

        [OperationContract(IsTerminating = true)]
        void Disconnect(string id);

        [OperationContract]
        string SendMessage(string message);

        [OperationContractAttribute(AsyncPattern = true)]
        IAsyncResult BeginSendMessage1(string message, AsyncCallback callback, object asyncState);
        string EndSendMessage1(IAsyncResult result);

        [OperationContract(IsOneWay = true)]
        void PostMessage(string msg);


    }

    [ServiceContract]
    public interface IStreamedService
    {
        [OperationContract(IsOneWay = true)]
        void SendData(byte[] data);

    }


    [ServiceContract]
    public interface _IStreamedService
    {
        [OperationContract]
        Stream GetStream(string streamInfo);
    }


}
