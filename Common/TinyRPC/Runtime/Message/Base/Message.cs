using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Message : IMessage
    {
        public bool IsRecycled { get; set; }

        public virtual void OnRecycle()
        {
            IsRecycled = true;
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
