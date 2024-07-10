using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    [ResponseType(typeof(Response))]
    public class Request : IRequest
    {
        // todo : code analysis, if  user try to modify this value,
        // throw exception as this memeber is for internal use only
        // as well as the Id property !
        public int rid;
        public int timeout = 5000;
        /// <inheritdoc/>
        public int Rid { get => rid; set => rid = value; }
        /// <inheritdoc/>
        public int Timeout { get => timeout; set => timeout = value; }
        bool IReusable.RequireRecycle { get; set; }
        void IDisposable.Dispose() => ObjectPool.Recycle(this);
        public virtual void OnRecycle()
        {
            rid = default;
            timeout = default;
        }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
