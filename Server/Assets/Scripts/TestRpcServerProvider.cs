using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Generated;
namespace zFramework.TinyRPC.Example
{
    //todo: 将要通过代码生成的方式自动的
    // 现在这个 RPC 消息还不能使用，因为没有注册到 RpcMessageHandlers 中
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