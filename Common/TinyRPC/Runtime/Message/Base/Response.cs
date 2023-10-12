using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class Response : Message, IResponse
    {
        public string error;
        /// <inheritdoc/>
        public string Error { get => error; set => error = value; }
    }
}
