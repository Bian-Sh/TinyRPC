using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class C2S_Login : Request
    {
        public string name;
        public string password;
    }
}