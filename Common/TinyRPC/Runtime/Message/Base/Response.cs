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
        public bool IsRecycled { get; set; }
        public virtual void OnRecycle()
        {
            IsRecycled = true;
            error = default;
            rid = default;
        }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
