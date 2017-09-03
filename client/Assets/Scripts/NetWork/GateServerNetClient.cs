using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using App.Base;
namespace App.NetWork
{
    public sealed class GateServerNetClient : SingleTon<GateServerNetClient>
    {
        public enum StatusType
        {
            Disconnected = 0,
            InLoginProcess =1,
            Connected = 2,
            InLogoutProcess = 3,
        }
        private MessageHandlerManager messageHandlers = null;
        private NetClient netClient = null;
        private StatusType status = StatusType.Disconnected;
        private string serverIp = "";
        private int serverPort = 0;
        public MessageHandlerManager Handlers
        {
            get { return this.messageHandlers; }
        }
        public GateServerNetClient()
        {

        }
        private void GotoStatusDisconnected()
        {
            this.serverIp = "";
            this.serverPort = 0;
            this.status = StatusType.Disconnected;
        }
        private void GotoStatusInLoginProcess()
        {
            this.status = StatusType.InLoginProcess;
        }
        private void GotoStatusConnected()
        {
            this.status = StatusType.Connected;
        }
        private void GotoStatusInLogoutProcess()
        {
            this.status = StatusType.InLogoutProcess;
        }
        public bool Init()
        {
            this.messageHandlers = new MessageHandlerManager();
            this.netClient = new NetClient();
            if (this.messageHandlers.Init() == false)
            {
                return false;
            }
            if (this.netClient.Init(OnConnect, OnDisconnect, OnRecvMessage, 1000, 1000, 1000, 5 * 1000, 10 * 1000) == false)
            {
                return false;
            }
            this.GotoStatusInLoginProcess();
            this.netClient.Connect(this.serverIp, this.serverPort);
            return true;
        }
        public void Dispose()
        {

        }
        public void Update()
        {

        }
        private void OnConnect(bool sucess,string errorMessage)
        {

        }
        private void OnDisconnect(bool isActiveClose,string errorMessage)
        {

        }
        private void OnRecvMessage(byte[] data)
        {
            int messageId = 0;
            object message = (object)data;
            this.messageHandlers.CallMessageHandler(messageId, message);
        }
        public void SendMessage<T>(T message) where T : class ,new()
        {
            byte[] data = null;
            if(data == null)
            {
                return;
            }
            this.netClient.SendMessage(data);
        }
    }
}

