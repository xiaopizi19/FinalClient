using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace App.NetWork
{

    public sealed class ConcurrentObjectPool<T> where T:class,new()
    {
        private List<T> reuseObjList = null;
        private object dataLock = null;
        public ConcurrentObjectPool()
        {
            this.reuseObjList = new List<T>();
            this.dataLock = new object();
        }
        public T GetObject()
        {
            lock (this.dataLock)
            {
                if (this.reuseObjList.Count > 0)
                {
                    T obj = this.reuseObjList[this.reuseObjList.Count - 1];
                    this.reuseObjList.RemoveAt(this.reuseObjList.Count - 1);
                    return obj;
                }
                return new T();
            }
        }
        public void ReturnObject(T obj)
        {
            lock (this.dataLock)
            {
                this.reuseObjList.Add(obj);
            }
        }
    }
    public sealed class ConcurrentQueue<T>
    {
        private Queue<T> queue = null;
        private int maxCount = 0;
        private bool isBounded = false;
        private object dataLock = null;
        public ConcurrentQueue(int maxCount =0)
        {
            this.queue = new Queue<T>();
            this.maxCount = 0;
            this.isBounded = (maxCount > 0);
            this.dataLock = new object();
        }
        public int Count
        {
            get
            {
                lock (this.dataLock)
                {
                    return this.queue.Count;
                }
            }
        }
        public int MaxCount
        {
            get
            {
                return this.maxCount;
            }
        }
        public bool Empty()
        {
            return this.Count == 0;
        }
        public bool Full()
        {
            lock (this.dataLock)
            {
                return FullNotLock();
            }
        }
        private bool FullNotLock()
        {
            if (this.isBounded == false)
            {
                return false;
            }
            else
            {
                return this.queue.Count >= this.maxCount;
            }
        }
        public void Enqueue(T item)
        {
            lock (this.dataLock)
            {
                while (FullNotLock())
                {
                    Monitor.Wait(this.dataLock);
                }
                this.queue.Enqueue(item);
                Monitor.PulseAll(this.dataLock);
            }
        }
        public void Dequeue(ref T item)
        {
            lock (this.dataLock)
            {
                while (this.queue.Count == 0)
                {
                    Monitor.Wait(this.dataLock);
                }
                item = this.queue.Dequeue();
                Monitor.PulseAll(this.dataLock);
            }
        }
        public bool TryEnqueue(T item)
        {
            lock (this.dataLock)
            {
                if (FullNotLock())
                {
                    return false;
                }
                this.queue.Enqueue(item);
                Monitor.PulseAll(this.dataLock);
                return true;
            }
        }
        public bool TryDequeue(ref T item)
        {
            lock (this.dataLock)
            {
                if (this.queue.Count == 0)
                {
                    return false;
                }
                item = this.queue.Dequeue();
                Monitor.PulseAll(this.dataLock);
                return true;
            }
        }
        public bool Peek(ref T item)
        {
            lock (this.dataLock)
            {
                if (this.queue.Count == 0)
                {
                    return false;
                }
                item = this.queue.Peek();
                return true;
            }
        }
        public void Clear()
        {
            lock(this.dataLock)
            {
                this.queue.Clear();
                Monitor.PulseAll(this.dataLock);
            }
        }
    }
    public class NetClient : IDisposable
    {
        public delegate void ConnectCallBack(bool sucess, string errorMessage);
        public delegate void DisconnectCallBack(bool isActiveClose, string errorMessage);
        public delegate void RecvMessageCallBack(byte[] data);
        private enum StatusType
        {
            Disconnected = 0,
            Connecting = 1,
            Connected = 2,
            DisConnecting = 3,
        }
        private enum NetCommandType
        {
            None = 0,
            Connect = 1,
            Disconnect = 2,
            SendMessage = 3,
            NetEventProxy = 4,
        } 
        private enum NetEventType
        {
            None = 0,
            ActiveClose = 1,
            PeerConnectTimeOut = 2,
            PeerCantConnect = 3,
            PeerConnected = 4,
            PeerClose = 5,
            RecvMessage = 6,
            NetWorkError = 7,
        }
        private class NetCommand
        {
            public NetCommandType type = NetCommandType.None;
            public object data = null;
        }
        private class NetEvent
        {
            public NetEventType type = NetEventType.None;
            public object data = null;
        }
        private class SocketReceiveStateObject
        {
            public Socket socket = null;
            public byte[] buffer = null;
        }
        private class SocketSendStateObject
        {
            public Socket socket = null;
            public byte[] buffer = null;
        }
        private ConnectCallBack connectCb = null;
        private DisconnectCallBack disconnectCb = null;
        private RecvMessageCallBack recvMessageCb = null;
        private int socketConnectTimeOutMSec = 0;
        private int socketSendTimeOutMSec = 0;

        private ConcurrentObjectPool<NetCommand> netCommandPool = null;
        private ConcurrentObjectPool<NetEvent> netEventPool = null;
        private ConcurrentObjectPool<SocketSendStateObject> netSendPool = null;
        private ConcurrentQueue<NetCommand> netCommandQueue = null;
        private ConcurrentQueue<NetEvent> netEventQueue = null;
        private ConcurrentQueue<SocketSendStateObject> netSendQueue = null;

        private Thread netSendThread = null;
        private Thread netThread = null;
        private Socket socket = null;
        private StatusType status = StatusType.Disconnected;

        public NetClient()
        {

        }
        ~NetClient()
        {
            this.Dispose();
        }
        public void Dispose()
        {
            if (this.netThread != null)
            {
                
            }
        }
        private StatusType Status
        {
            get { return this.status; }
        }

        public bool Init(ConnectCallBack connectCb = null,DisconnectCallBack disconnectCb= null,
                       RecvMessageCallBack recvMessageCb = null, int netCommandQueueSize=0,int netEventQueueSize=0
                      , int netSendQueueSize = 0, int socketConnectTimeoutMSec = 0, int socketSendTimeoutMSec = 0)
        {
            this.connectCb = connectCb;
            this.disconnectCb = disconnectCb;
            this.recvMessageCb = recvMessageCb;
            this.socketConnectTimeOutMSec = socketConnectTimeoutMSec;
            this.socketSendTimeOutMSec = socketSendTimeoutMSec;
            this.netCommandPool = new ConcurrentObjectPool<NetCommand>();
            this.netEventPool = new ConcurrentObjectPool<NetEvent>();
            this.netSendPool = new ConcurrentObjectPool<SocketSendStateObject>();
            this.netCommandQueue = new ConcurrentQueue<NetCommand>();
            this.netEventQueue = new ConcurrentQueue<NetEvent>();
            this.netSendQueue = new ConcurrentQueue<SocketSendStateObject>();
            this.netSendThread = new Thread(NetSendThreadFunc);
            this.netThread = new Thread(NetThreadFunc);
            return true;
        }
        private void GotoStatusDisconnected()
        {
            this.status = StatusType.Disconnected;
        }
        private void GotoStatusConnecting()
        {
            this.status = StatusType.Connecting;
        }
        private void GotoStatusConnected()
        {
            this.status = StatusType.Connected;
        }
        private void GotoStatusDisconnecting()
        {
            this.status = StatusType.DisConnecting;
        }
        private void ReturnNetEventObject(NetEvent evt)
        {
            evt.type = NetEventType.None;
            evt.data = null;
            this.netEventPool.ReturnObject(evt);
        }
        private void ReturnNetCommandObject(NetCommand cmd)
        {
            cmd.type = NetCommandType.None;
            cmd.data = null;
            this.netCommandPool.ReturnObject(cmd);
        }
        private void ReturnSocketSendStateObject(SocketSendStateObject stateObj)
        {
            stateObj.socket = null;
            stateObj.buffer = null;
            this.netSendPool.ReturnObject(stateObj);
        }
        public bool Connect(string ip,int port)
        {
            if (this.status != StatusType.Disconnected)
            {
                return false;
            }
            IPAddress addr = IPAddress.TryParse(ip, out addr) ? addr : null;
            if (addr == null)
            {
                return false;
            }
            if(port<IPEndPoint.MinPort||port>IPEndPoint.MaxPort)
            {
                return false;
            }
            IPEndPoint endPoint = new IPEndPoint(addr, port);
            NetCommand cmd = this.netCommandPool.GetObject();
            cmd.type = NetCommandType.Connect;
            cmd.data = endPoint;
            this.netCommandQueue.Enqueue(cmd);
            this.GotoStatusConnecting();
            return true;
        }
        public void Disconnect()
        {
            if(this.status != StatusType.Connecting && this.status != StatusType.Connected)
            {
                return;
            }
            NetCommand cmd = this.netCommandPool.GetObject();
            cmd.type = NetCommandType.Disconnect;
            this.netCommandQueue.Enqueue(cmd);
        }
        public void SendMessage(byte[] data)
        {
            if (this.status != StatusType.Connected)
            {
                return;
            }
            NetCommand cmd = this.netCommandPool.GetObject();
            cmd.type = NetCommandType.SendMessage;
            cmd.data = data;
            this.netCommandQueue.Enqueue(cmd);
        }
        public void Update()
        {
            for(;;)
            {
                NetEvent evt = null;
                if (this.netEventQueue.TryDequeue(ref evt) == false)
                {
                    break;
                }
                if(evt.type == NetEventType.ActiveClose)
                {
                    this.GotoStatusConnected();
                    if (this.disconnectCb != null)
                    {
                        this.disconnectCb(true, "");
                    }
                } else if(evt.type == NetEventType.PeerCantConnect)
                {
                    this.GotoStatusDisconnected();
                    if (this.connectCb != null)
                    {
                        this.connectCb(false, (string)evt.data);
                    }
                }
                else if (evt.type == NetEventType.PeerConnectTimeOut)
                {
                    if (this.status == StatusType.Connecting)
                    {
                        this.GotoStatusDisconnected();
                        if (this.connectCb != null)
                        {
                            this.connectCb(false, (string)evt.data);
                        }
                    }
                }
                else if (evt.type == NetEventType.PeerConnected)
                {
                    this.GotoStatusConnected();
                    if (this.connectCb != null)
                    {
                        this.connectCb(true, "");
                    }
                }
                else if (evt.type == NetEventType.PeerClose)
                {
                    this.GotoStatusDisconnected();
                    if (this.disconnectCb != null)
                    {
                        this.disconnectCb(false, "");
                    }
                }
                else if (evt.type == NetEventType.RecvMessage)
                {
                    if (this.recvMessageCb != null)
                    {
                        this.recvMessageCb((byte[])evt.data);
                    }
                }
                else if (evt.type == NetEventType.NetWorkError)
                {
                    this.GotoStatusConnected();
                    if (this.disconnectCb != null)
                    {
                        this.disconnectCb(false, (string)evt.data);
                    }
                }
                this.ReturnNetEventObject(evt);
            }
        }
        private void NetThreadFunc()
        {
            for(;;)
            {
                NetCommand cmd = null;
                this.netCommandQueue.Dequeue(ref cmd);
                if(cmd == null)
                {
                    break;
                }
                if(cmd.type == NetCommandType.Connect)
                {
                    this.ProcessNetCommandConnect((EndPoint)cmd.data);
                }
                else if (cmd.type == NetCommandType.Disconnect)
                {
                    this.ProcessNetCommandDisconnect();
                }
                else if(cmd.type == NetCommandType.NetEventProxy)
                {
                    NetEvent evt = (NetEvent)cmd.data;
                    this.ProcessNetCommandNetEventProxy(evt);
                }
                else if (cmd.type == NetCommandType.SendMessage)
                {
                    this.ProcessNetCommandSendMessage((byte[])cmd.data);
                }
                this.ReturnNetCommandObject(cmd);
            }
        }
        private void NetSendThreadFunc()
        {
            for(;;)
            {
                SocketSendStateObject stateObj = null;
                this.netSendQueue.Dequeue(ref stateObj);
                if(stateObj == null)
                {
                    break;
                }
                try
                {
                    stateObj.socket.Send(stateObj.buffer);
                }
                catch (ObjectDisposedException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    NetEvent evt = this.netEventPool.GetObject();
                    evt.type = NetEventType.NetWorkError;
                    evt.data = e.Message;
                    NetCommand cmd = this.netCommandPool.GetObject();
                    cmd.type = NetCommandType.NetEventProxy;
                    cmd.data = evt;
                    this.netCommandQueue.Enqueue(cmd);
                    continue;
                }
                this.ReturnSocketSendStateObject(stateObj);
            }
        }
        private void OnSocketAsyncReceiveFinish(IAsyncResult result)
        {
            SocketReceiveStateObject stateObj = (SocketReceiveStateObject)result.AsyncState;
            Socket socket = stateObj.socket;
            int readBytes = 0;
            try
            {
                readBytes = socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.NetWorkError;
                evt.data = e.Message;
                NetCommand cmd = this.netCommandPool.GetObject();
                cmd.type = NetCommandType.NetEventProxy;
                cmd.data = evt;
                this.netCommandQueue.Enqueue(cmd);
                return;
            }
            if (readBytes == 0)
            {
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.PeerClose;
                NetCommand cmd = this.netCommandPool.GetObject();
                cmd.type = NetCommandType.NetEventProxy;
                cmd.data = evt;
                this.netCommandQueue.Enqueue(cmd);
            }
            else
            {
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.RecvMessage;
                evt.data = new byte[readBytes];
                Buffer.BlockCopy(stateObj.buffer, 0, (byte[])evt.data, 0, readBytes);
                this.netEventQueue.Enqueue(evt);
                socket.BeginReceive(stateObj.buffer, 0, stateObj.buffer.Length, SocketFlags.None, OnSocketAsyncReceiveFinish, stateObj);
            }
        }
        private void OnSocketAsyncConnectFinish(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            try
            {
                socket.EndConnect(result);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch(Exception e)
            {
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.PeerCantConnect;
                evt.data = e.Message;
                NetCommand cmd = this.netCommandPool.GetObject();
                cmd.type = NetCommandType.NetEventProxy;
                cmd.data = evt;
                this.netCommandQueue.Enqueue(cmd);
                return;
            }
            NetEvent evts = this.netEventPool.GetObject();
            evts.type = NetEventType.PeerConnected;
            this.netEventQueue.Enqueue(evts);
            SocketReceiveStateObject stateObj = new SocketReceiveStateObject();
            stateObj.socket = socket;
            stateObj.buffer = new byte[10240];
            socket.BeginReceive(stateObj.buffer, 0, stateObj.buffer.Length, SocketFlags.None, OnSocketAsyncReceiveFinish, stateObj);

        }
        private void DoTimeoutBeginConnect(Socket socket,EndPoint endPoint)
        {
            IAsyncResult result = socket.BeginConnect(endPoint, OnSocketAsyncConnectFinish, socket);
            if (this.socketConnectTimeOutMSec > 0 && result.AsyncWaitHandle.WaitOne(this.socketConnectTimeOutMSec, true) == false)
            {
                socket.Close();
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.PeerConnectTimeOut;
                evt.data = new SocketException((int)SocketError.TimedOut).Message;
                NetCommand cmd = this.netCommandPool.GetObject();
                cmd.type = NetCommandType.NetEventProxy;
                cmd.data = evt;
                this.netCommandQueue.Enqueue(cmd);
            }
        }
        private void ProcessNetCommandConnect(EndPoint endPoint)
        {
            this.socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.socket.NoDelay = true;
            if (this.socketSendTimeOutMSec > 0)
            {
                this.socket.SendTimeout = this.socketSendTimeOutMSec;
            }
        }
        private void ProcessNetCommandDisconnect()
        {
            if (this.socket != null)
            {
                this.socket.Close();
                this.socket = null;
                this.netSendQueue.Clear();
                NetEvent evt = this.netEventPool.GetObject();
                evt.type = NetEventType.ActiveClose;
                this.netEventQueue.Enqueue(evt);
            }
        }
        private void ProcessNetCommandNetEventProxy(NetEvent evt)
        {
            if (evt.type == NetEventType.PeerConnectTimeOut || evt.type == NetEventType.PeerCantConnect || evt.type == NetEventType.PeerClose || evt.type == NetEventType.NetWorkError)
            {
                if (this.socket != null)
                {
                    this.socket.Close();
                    this.socket = null;
                    this.netSendQueue.Clear();
                }
            }
            this.netEventQueue.Enqueue(evt);
        }
        private void ProcessNetCommandSendMessage(byte[] data)
        {
            if (this.socket == null)
            {
                return;
            }
            if(this.socket.Connected == false)
            {
                return;
            }
            SocketSendStateObject stateObj = this.netSendPool.GetObject();
            stateObj.socket = this.socket;
            stateObj.buffer = data;
            this.netSendQueue.Enqueue(stateObj);
        }
    }
}

