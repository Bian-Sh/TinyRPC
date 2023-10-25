using System;

namespace zFramework.TinyRPC.Messages
{
    [Serializable]
    [ResponseType(typeof(Response))]
    public class Request : IRequest
    {
        public int id;
        public int timeout = 5000;
        /// <inheritdoc/>
        public int Id { get => id; set => id = value; }
        /// <inheritdoc/>
        public int Timeout { get => timeout; set => timeout = value; }
    }
}
