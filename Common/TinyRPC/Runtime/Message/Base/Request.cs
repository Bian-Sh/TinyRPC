using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    [ResponseType(typeof(Response))]
    public class Request : Message , IRequest
    {
        public int timeout = 5000;
        /// <inheritdoc/>
        public int Timeout { get => timeout; set => timeout = value; }
    }
}
