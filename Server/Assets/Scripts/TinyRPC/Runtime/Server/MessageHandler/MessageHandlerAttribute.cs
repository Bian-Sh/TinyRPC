using System;
namespace zFramework.TinyRPC
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MessageHandlerAttribute : Attribute
    {
        public MessageType type;
        public MessageHandlerAttribute(MessageType type)
        {
            this.type = type;
        }
    }
}
