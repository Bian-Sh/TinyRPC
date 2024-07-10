using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Response : IResponse
    {
        public string error;
        public int rid;
        /// <inheritdoc/>
        public int Rid { get => rid; set => rid = value; }
        /// <inheritdoc/>
        public string Error { get => error; set => error = value; }
        bool IReusable.RequireRecycle { get; set; }
        void IDisposable.Dispose() => ObjectPool.Recycle(this);
        public virtual void OnRecycle()
        {
            error = default;
            rid = default;
        }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
