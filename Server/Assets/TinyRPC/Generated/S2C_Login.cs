using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class S2C_Login : Response
    {
        public bool success;
        public string token;
        public string errorDesc;
    }
}