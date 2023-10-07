using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class Response : Message
    {
        public string error;
    }
}
