using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace App.NetWork
{
    public class MessageHandlerManager : IDisposable
    {
        private abstract class MessageHandlerWrapperBase
        {
            public abstract void Call(object message);
        }
        private class MessageHandlerWrapper<T> :MessageHandlerWrapperBase
        {
            private Action<T> handler = null;
            public MessageHandlerWrapper(Action<T> handler)
            {
                this.handler = handler;
            }
            public override void Call(object message)
            {
                this.handler((T)message);
            }
        }
        private Dictionary<int, MessageHandlerWrapperBase> messageHandlerMap;
        public MessageHandlerManager()
        {

        }
        ~MessageHandlerManager()
        {
            this.Dispose();
        }
        public void Dispose()
        {

        }
        public bool Init()
        {
            this.messageHandlerMap = new Dictionary<int, MessageHandlerWrapperBase>();
            return true;
        }
        public void RegisterMessageHandler<T>(Action<T> handler) where T : class,new()
        {
            int messageId = 0;
            if (messageId == 0)
            {
                return;
            }
            if (this.messageHandlerMap.ContainsKey(messageId))
            {
                return;
            }
            this.messageHandlerMap[messageId] = new MessageHandlerWrapper<T>(handler);
        }
        public void CallMessageHandler(int messageId,object message)
        {
            MessageHandlerWrapperBase handler = null;
            if (this.messageHandlerMap.TryGetValue(messageId, out handler) == false)
            {
                return;
            }
            handler.Call(messageId);
        }
    }
}

