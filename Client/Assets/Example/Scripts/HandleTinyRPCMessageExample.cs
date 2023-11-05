using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

public class HandleTinyRPCMessageExample : MonoBehaviour
{
    private void OnEnable()
    {
        this.AddNetworkSignal<TestMessage>(OnTestMessageReceived);
        this.AddNetworkSignal<TestRPCRequest, TestRPCResponse>(RPCMessageHandler);
    }

    private void OnDisable()
    {
        this.RemoveNetworkSignal<TestMessage>(OnTestMessageReceived);
        this.RemoveNetworkSignal<TestRPCRequest, TestRPCResponse>(RPCMessageHandler);
    }

    private void OnTestMessageReceived(Session session, TestMessage message)
    {
        Debug.Log($"获取到{(session.IsServerSide ? "客户端" : "服务器")}  {session}  的消息, message = {message}");
    }

    //[MessageHandler(MessageType.RPC)] // 与 AddNetworkSignal<Request,Response>  二选一
    private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        Debug.Log($"{nameof(HandleTinyRPCMessageExample)}: Receive {session} request {request}");
        await Task.Delay(500);
        response.name = $"response  from  tinyrpc {(session.IsServerSide ? "SERVER" : "CLIENT")}  !";
    }
}
