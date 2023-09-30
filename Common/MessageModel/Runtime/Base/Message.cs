using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class Message
    {
        /// <summary>
        /// 消息 id , 系统中自增，用于 rpc 消息身份识别
        /// </summary>
        public int id; 
    }
}
