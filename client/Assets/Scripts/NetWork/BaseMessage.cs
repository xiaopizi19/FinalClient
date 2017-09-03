using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace App.NetWork
{
    public abstract class BaseMessage
    {
        public delegate BaseMessage CreateFunc();
        public abstract void EncodeToStream(byte[] data);
        public abstract void DecodeFromStream(byte[] data);
        private uint headerId = 0;
        public uint HeaderId
        {
            get { return this.headerId; }
            set { this.headerId = value; }
        }

    }
}