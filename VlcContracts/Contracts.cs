using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;

namespace VlcContracts
{

    public class CommunicationConst
    {
        public readonly static string PipeAddress = @"net.pipe://localhost/ICommunicationTest/Pipe";

        public readonly static string TcpLocalhostAddress = @"net.tcp://localhost/ICommunicationTest/tcp";
        public readonly static string TcpAddress = @"net.tcp://192.168.10.158/ICommunicationTest/tcp";

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

    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ICommunicationCallback))]
    //[ServiceContract(SessionMode = SessionMode.Required)]
    public interface ICommunicationService
    {

        [OperationContract(IsInitiating = true)]
        bool Connect(string id, object[] args);

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

        //[OperationContract(IsOneWay = true)]
        //void PostData(byte[] data);
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
