using System;
using System.Diagnostics;
using UnityEngine;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    [ResponseType(typeof(Response))]
    public class Request : IRequest
    {
        // todo : code analysis, if found user try to modify this value, throw exception as this memeber is for internal use only
        // as well as the Id property !
        public int id;
        public int timeout = 5000;
        /// <inheritdoc/>
        public int Id { get => id; set => id = value; }
        /// <inheritdoc/>
        public int Timeout { get => timeout; set => timeout = value; }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
