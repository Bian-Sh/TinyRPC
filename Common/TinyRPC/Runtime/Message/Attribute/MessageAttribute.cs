namespace zFramework.TinyRPC
{
    /// <summary>
    /// 标记一个类为消息类，同时分配一个消息ID
    /// </summary>
    public class MessageAttribute : BaseAttribute
    {
        /// <summary>
        /// 内部消息 ID，生成消息实体类时自动分配
        /// </summary>
        public int MessageId { get; set; }
        public MessageAttribute(int messageId)
        {
            MessageId = messageId;
        }
    }
}
