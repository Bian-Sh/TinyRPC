namespace zFramework.TinyRPC
{
    public enum MessageType : byte
    {
        /// <summary> Ping 消息 </summary>
        Ping = 0,
        /// <summary> 普通消息 </summary>
        Normal = 1,
        /// <summary> RPC 消息 </summary>
        RPC = 2
    }
}
