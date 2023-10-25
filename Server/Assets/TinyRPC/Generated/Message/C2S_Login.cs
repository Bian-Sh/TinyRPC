using System;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    [ResponseType(typeof(S2C_Login))]
    public class C2S_Login : Request
    {
        public string name;
        public string password;
    }
}