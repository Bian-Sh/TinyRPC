using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class Response :  IResponse
    {
        public string error;
        public int id;
        /// <inheritdoc/>
        public int Id { get => id; set => id = value; }
        /// <inheritdoc/>
        public string Error { get => error; set => error = value; }
    }
}
