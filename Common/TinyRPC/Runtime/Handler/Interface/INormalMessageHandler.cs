using System.Reflection;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    internal interface INormalMessageHandler
    {
        /// <summary>
        ///  内部调用接口，用于注册用户通过 MessageHandlerAtrribute 标记的消息处理器
        /// </summary>
        /// <param name="method">用户消息处理器函数信息</param>
        /// <param name="priority">优先级,仅在常规消息处理器中生效</param>
        void Bind(MethodInfo method, int priority);
        void Dispatch(Session session, IMessage message);
    }
}