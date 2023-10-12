namespace zFramework.TinyRPC.DataModel
{
    public interface IMessage
    {
        /// <summary>
        /// 消息 id , 系统中自增，用于消息身份识别
        /// </summary>
        int Id { get; set; }
    }
}
