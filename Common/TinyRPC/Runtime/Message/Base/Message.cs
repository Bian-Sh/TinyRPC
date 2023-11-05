using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Message : IMessage
    {
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
