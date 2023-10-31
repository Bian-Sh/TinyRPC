using System;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    [ResponseType(typeof(TestRPCResponse))]
    public class TestRPCRequest : Request
    {
        public string name;
    }
}
