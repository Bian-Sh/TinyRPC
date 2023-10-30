using System;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    public class Response : IResponse
    {
        public string error;
        public int id;
        /// <inheritdoc/>
        public int Id { get => id; set => id = value; }
        /// <inheritdoc/>
        public string Error { get => error; set => error = value; }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
