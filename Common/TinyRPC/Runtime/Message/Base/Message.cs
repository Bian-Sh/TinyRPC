using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Message : IMessage
    {
        bool IReusable.RequireRecycle { get; set; }
        void IDisposable.Dispose() => ObjectPool.Recycle(this);
        public virtual void OnRecycle() { }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
