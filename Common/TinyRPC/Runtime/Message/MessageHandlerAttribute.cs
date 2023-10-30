using System;
namespace zFramework.TinyRPC
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MessageHandlerAttribute : Attribute
    {
        public MessageType type;
        public int priority;
        /// <summary>
        /// 消息处理器标记
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="priority">priority 仅在注册 normal message 处理器时生效</param>
        public MessageHandlerAttribute(MessageType type, int priority = 0)
        {
            this.type = type;
            this.priority = priority;
        }
    }
}
