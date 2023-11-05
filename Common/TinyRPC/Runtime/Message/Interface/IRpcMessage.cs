namespace zFramework.TinyRPC.Messages
{
    public interface IRpcMessage :IMessage
    {
        /// <summary>
        /// 消息 id , 系统中自增，用于 RPC 消息身份识别
        /// </summary>
        int Rid { get; set; }
    }
}
