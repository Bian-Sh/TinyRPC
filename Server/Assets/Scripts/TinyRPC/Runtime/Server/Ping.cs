using System;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    [Serializable]
    public class Ping : Message
    {
        public DateTime svrTime;// 服务器时间, 配合接受时间，client 可以计算出网络延迟
    }
}
