using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Generated;
namespace zFramework.TinyRPC.Example
{
    // 这个示例代码演示了如何使用特性标记的消息处理器
    // MessageHandlerAttribute 用于标记消息处理器,必须是静态方法
    // MessageHandlerProviderAttribute 用于标记消息处理器提供者

    // this example show how to use attribute marked message handler
    // MessageHandlerAttribute is used to mark message handler, it must be static
    // MessageHandlerProviderAttribute is used to mark message handler provider
    [MessageHandlerProvider]
    public class TestRpcServerProvider
    {
        [MessageHandler(MessageType.RPC)]
        static Task OnLoginRequest(Session session, C2S_Login request, S2C_Login response)
        {
            Debug.Log($"{nameof(TestRpcServerProvider)}:  Attribute marked RPC Handler receive  message correctly, for more info see below!");
            Debug.Log($"{nameof(TestRpcServerProvider)}: Receive RPC Request ：{request.name} ");
            response.success = true;
            response.token = "RPC Server Say Login Success";
            return Task.FromResult(response);
        }

        [MessageHandler(MessageType.Normal)]
        static void OnAttributeRegistTestMessage(Session session, AttributeRegistTestMessage message)
        {
            Debug.Log($"{nameof(TestRpcServerProvider)}:  Attribute marked  NORMAL Message Handler receive  message correctly, for more info see below!");
            Debug.Log($"{nameof(TestRpcServerProvider)}: Receive Normal Message ：{message.desc} ");
        }
    }
}