using System;
using zFramework.TinyRPC;
using zFramework.TinyRPC.DataModel;

[Serializable]
    [ResponseType(typeof(TestRPCResponse))]
    public class TestRPCRequest : Request
    {
        public string name;
    }
