using System;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    public class S2C_Login : Response
    {
        public bool success;
        public string token;
        public string errorDesc;
    }
}