using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Message : IMessage
    {
        readonly NotImplementedException exception = new("Normal message do not provide id");
        /// <inheritdoc/>
        public int Id { get => throw exception; set => throw exception; }
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
