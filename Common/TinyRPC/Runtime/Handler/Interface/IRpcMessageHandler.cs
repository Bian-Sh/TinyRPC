using System.Reflection;
using System.Threading.Tasks;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    internal interface IRpcMessageHandler
    {
        /// <summary>
        ///  注册用户通过 MessageHandlerAtrribute 标记的消息处理器
        /// </summary>
        /// <param name="method">用户消息处理器函数信息</param>
        void Bind(MethodInfo method);
        Task Dispatch(Session session, IRequest request,IResponse response);
    }
}