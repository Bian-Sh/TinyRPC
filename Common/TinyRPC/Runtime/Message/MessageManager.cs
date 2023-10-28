using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC
{
    // 获取所有的消息处理器解析并缓存
    // 消息处理器注册方式有 2 种：
    // 1. 使用 TinyMessageHandlerAttribute 标记方法，使用代码生成插入到 MessageHandlerRegister.Awake 中
    // 2. 用户自己通过 UnityEngine.Component 扩展方法 AddNetworkSignal 、AddRpcSignal 注册
    //
    // 约定 TinyMessageHandlerAttribute 只能出现在静态方法上
    public static class MessageManager
    {
        internal static readonly Dictionary<Type, INormalMessageHandler> NormalMessageHandlers = new();
        internal static readonly Dictionary<Type, IRpcMessageHandler> RpcMessageHandlers = new();
        static readonly Dictionary<Type, Type> rpcMessagePairs = new(); // RPC 消息对，key = Request , value = Response
        static readonly Dictionary<int, RpcInfo> rpcInfoPairs = new(); // RpcId + RpcInfo

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Awake()
        {
            // add ping message and its handler internal 
            RegistPingMessageAndHandlerInternal();
            // regist rpc message pairs must before RegistGeneratedMessageHandlers
            RegistRPCMessagePairs();
            RegistGeneratedMessageHandlers();
        }

        private static void RegistPingMessageAndHandlerInternal()
        {
            rpcMessagePairs.Add(typeof(Ping), typeof(Ping));
            var handler = new RpcMessageHandler<Ping, Ping>();
            handler.AddTask(TCPServer.OnPingReceived);
            RpcMessageHandlers.Add(typeof(Ping), handler);
        }

        // 注册所有位于 “com.zframework.tinyrpc.generated” 程序集下的消息处理器
        private static void RegistGeneratedMessageHandlers()
        {
            var types = Assembly.Load("com.zframework.tinyrpc.generated")
                .GetTypes();

            // use reflection to regist rpc message handlers
            var requests = types.Where(type => type.IsSubclassOf(typeof(Request)));
            foreach (var type in requests)
            {
                var handler = Activator.CreateInstance(typeof(RpcMessageHandler<,>)
                    .MakeGenericType(type, GetResponseType(type))) as IRpcMessageHandler;
                RpcMessageHandlers.Add(type, handler);
            }

            // use reflection to regist normal message handlers
            var messages = types.Where(type => type.IsSubclassOf(typeof(Message)));
            foreach (var type in messages)
            {
                var handler = Activator.CreateInstance(typeof(NormalMessageHandler<>).MakeGenericType(type)) as INormalMessageHandler;
                NormalMessageHandlers.Add(type, handler);
            }
        }

        public static void RegistRPCMessagePairs()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(v => v.FullName.StartsWith("com.zframework.tinyrpc.generated"));

            if (assembly != null)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsSubclassOf(typeof(Request)))
                    {
                        var attr = type.GetCustomAttribute<ResponseTypeAttribute>();
                        if (attr != null)
                        {
                            rpcMessagePairs.Add(type, attr.Type);
                        }
                        else
                        {
                            Debug.LogError($"{nameof(MessageManager)}: 请务必为 {type.Name} 通过 ResponseTypeAttribute 配置 Response 消息！");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"{nameof(MessageManager)}: 请保证 生成的网络消息在 “com.zframework.tinyrpc.generated” 程序集下");
            }
        }

        internal static void HandleNormalMessage(Session session, IMessage message)
        {
            if (NormalMessageHandlers.TryGetValue(message.GetType(), out var handler))
            {
                handler.Invoke(session, message);
            }
            else
            {
                Debug.LogWarning($"{nameof(MessageManager)}: no handler for message type {message.GetType()}");
            }
        }

        internal static async void HandleRpcRequest(Session session, IRequest request)
        {
            var type = request.GetType();
            IResponse response;
            if (RpcMessageHandlers.TryGetValue(type, out var handler))
            {
                if (rpcMessagePairs.TryGetValue(type, out var responseType))
                {
                    response = Activator.CreateInstance(responseType) as IResponse;
                    response.Id = request.Id;
                    await handler.Invoke(session, request, response);
                }
                else
                {
                    var error = $"RPC 消息 {request.GetType().Name} 没有找到对应的 Response 类型！";
                    response = new Response
                    {
                        Id = request.Id,
                        Error = error
                    };
                    Debug.LogWarning($"{nameof(MessageManager)}: {error}");
                }
            }
            else
            {
                var error = $"RPC 消息 {request.GetType().Name} 没有找到对应的处理器！";
                response = new Response
                {
                    Id = request.Id,
                    Error = error
                };
                Debug.LogWarning($"{nameof(MessageManager)}: {error}");
            }
            session.Reply(response);
        }
        internal static void HandleRpcResponse(Session session, IResponse response)
        {
            if (rpcInfoPairs.TryGetValue(response.Id, out var rpcInfo))
            {
                rpcInfo.task.SetResult(response);
                rpcInfoPairs.Remove(response.Id);
            }
        }

        internal static Task<IResponse> AddRpcTask(IRequest request)
        {
            var tcs = new TaskCompletionSource<IResponse>();
            var cts = new CancellationTokenSource();
            var timeout = Mathf.Max(request.Timeout, 5000); //至少等待 5 秒的响应机会，这在发生复杂操作时很有效
            cts.CancelAfter(timeout);
            var exception = new TimeoutException($"RPC Call Timeout! Request: {request}");
            cts.Token.Register(() => tcs.TrySetException(exception), useSynchronizationContext: false);
            var rpcinfo = new RpcInfo
            {
                id = request.Id,
                task = tcs,
            };
            rpcInfoPairs.Add(request.Id, rpcinfo);
            return tcs.Task;
        }

        // 获取 IRequest 对应的 Response 实例
        public static IResponse CreateResponse([NotNull] IRequest request)
        {
            IResponse response;
            if (!rpcMessagePairs.TryGetValue(request.GetType(), out var type))
            {
                //fallback Response is the base type , thus Response.
                type = typeof(Response);
            }
            response = Activator.CreateInstance(type) as IResponse;
            response.Id = request.Id;
            response.Error = response is Response ? $"RPC 消息 {request.GetType().Name} 没有找到对应的 Response 类型！" : "";

            return response;
        }


        // 获取消息对应的 Response 类型
        public static Type GetResponseType([NotNull] IRequest request)
        {
            if (!rpcMessagePairs.TryGetValue(request.GetType(), out var type))
            {
                throw new Exception($"RPC 消息  Request-Response 为正确完成映射，请参考示例正确注册映射关系！");
            }
            return type;
        }
        public static Type GetResponseType(Type request)
        {
            if (!request.IsSubclassOf(typeof(Request)) && request != typeof(Ping))
            {
                throw new ArgumentException($"指定的参数必须是 Request 的子类！");
            }
            if (!rpcMessagePairs.TryGetValue(request, out var type))
            {
                throw new Exception($"RPC 消息  Request-Response 为正确完成映射，请参考示例正确注册映射关系！");
            }
            return type;
        }
    }
}
