using System;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    public class TestMessage : Message
    {
        public string message; // 消息内容
        public int age; // 年龄(fake 消息内容)
    }
}
