using EasyButtons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;
using zFramework.TinyRPC.Messages;
using static zFramework.TinyRPC.MessageManager;

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

    [Button]
    private static void RegistGeneratedMessageHandlers()
    {
        Dictionary<Type, IRpcMessageHandler> RpcMessageHandlers = new();
        RegistRPCMessagePairs();
        // regist rpc message handlers
        var types = Assembly.Load("com.zframework.tinyrpc.generated")
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Request)));
        Debug.Log($"{nameof(MessageManager)}: types coutnt = {types.Count()}");
        // log types
        foreach (var type in types)
        {
            Debug.Log($"{nameof(MessageManager)}: type is {type}");
        }
        // use reflection to regist rpc message handlers
        foreach (var type in types)
        {
            Debug.Log($"{nameof(MessageManager)}: type is {type}");
            var handler = Activator.CreateInstance(typeof(RpcMessageHandler<,>).MakeGenericType(type, GetResponseType(type))) as IRpcMessageHandler;
            //RpcMessageHandlers.Add(type, handler);
            RpcMessageHandlers[type] = handler;
        }

        // log these handlers
        foreach (var handler in RpcMessageHandlers)
        {
            Debug.Log($"{nameof(MessageManager)}: {handler.Key}  {handler.Value}");
        }

    }

}
