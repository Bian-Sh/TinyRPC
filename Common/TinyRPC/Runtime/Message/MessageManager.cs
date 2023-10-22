using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.DataModel;
using zFramework.TinyRPC.Settings;

namespace zFramework.TinyRPC
{
    // 获取所有的消息处理器解析并缓存
    // 消息处理器是被 MessageHandlerAttribute 标记的方法
    // 约定 MessageHandlerAttribute 只会出现在静态方法上
    public static class MessageManager
    {
        static readonly Dictionary<Type, NormalHandlerInfo> normalHandlers = new();
        static readonly Dictionary<Type, RpcHandlerInfo> rpcHandlers = new();
        static readonly Dictionary<Type, Type> rpcMessagePairs = new(); // RPC 消息对，key = Request , value = Response
        static readonly Dictionary<int, RpcInfo> rpcInfoPairs = new(); // RpcId + RpcInfo

        //通过反射获取所有的RPC消息映射
        // 约定消息必须存在于同一个叫做 ：com.zframework.tinyrpc.generate 程序集中
        // 此程序集将根据 proto 文件描述的继承关系一键生成
        // 计划将其放在 Packages 文件夹
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Awake()
        {
            rpcInfoPairs.Clear();
            normalHandlers.Clear();
            rpcHandlers.Clear();
            rpcMessagePairs.Clear();

            // add ping message and its handler internal 
            rpcMessagePairs.Add(typeof(Ping), typeof(Ping));
            RegistHandler(typeof(TCPServer));

            RegistRPCMessagePairs();
            RegistAllHandlers();
            Debug.Log($"{nameof(MessageManager)}: TinyRPC Awake ~");
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
            // for log only
            foreach (var item in rpcMessagePairs)
            {
                Debug.Log($"{nameof(MessageManager)}: RPC Pair Added , request = {item.Key.Name}, response = {item.Value.Name}");
            }
        }

        public static void RegistAllHandlers()
        {
            // store all message handlers
            var handlers = AppDomain.CurrentDomain.GetAssemblies()
                .Where(v => TinyRpcSettings.Instance.AssemblyNames.Contains(v.FullName.Split(',')[0]))
                .SelectMany(v => v.GetTypes())
                .Where(v => v.GetCustomAttribute<MessageHandlerProviderAttribute>() != null);

            foreach (var handler in handlers)
            {
                RegistHandler(handler);
            }
            Debug.Log($"{nameof(MessageManager)}:  rpc handles count = {rpcHandlers.Count()}");
            foreach (var item in rpcHandlers)
            {
                Debug.Log($"{nameof(MessageManager)}:  for {item.Key.Name} - handler = {item.Value.method.Name}");
            }
        }

        public static void RegistHandler(Type type)
        {
            // 网络消息处理器必须是静态方法
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(v => v.GetCustomAttribute<MessageHandlerAttribute>() != null);
            foreach (var method in methods)
            {
                //log error if method is not static
                if (!method.IsStatic)
                {
                    Debug.LogError($"MessageHandler {method.DeclaringType.Name}.{method.Name} 必须是静态方法！");
                    continue;
                }
                var param = method.GetParameters();
                if (param.Length == 0)
                {
                    Debug.LogError($"MessageHandler {method.DeclaringType.Name}.{method.Name} 消息处理器至少有一个参数！");
                    continue;
                }
                var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
                switch (attr.type)
                {
                    case MessageType.Normal:
                        if (param.Length != 2
                            || (param.Length == 2 && param[0].ParameterType != typeof(Session) && !typeof(Message).IsAssignableFrom(param[1].ParameterType)))
                        {
                            Debug.LogError($"常规消息处理器 {method.Name} 必须有2个参数, 左侧 Session，右侧 Message!");
                            continue;
                        }
                        var msgType = param[1].ParameterType;
                        if (!normalHandlers.TryGetValue(msgType, out var info))
                        {
                            info = new NormalHandlerInfo
                            {
                                method = method,
                                Message = msgType
                            };
                            normalHandlers.Add(msgType, info);
                        }
                        else
                        {
                            Debug.LogError($"{nameof(MessageManager)}: 请不要重复注册 {msgType.Name} 处理器，此消息已被 {info.method.DeclaringType.Name}.{info.method.Name}中处理 ");
                        }
                        break;
                    case MessageType.RPC:
                        if (param.Length != 3
                        || (param.Length == 3 && param[0].ParameterType != typeof(Session)
                        && !typeof(Request).IsAssignableFrom(param[1].ParameterType)
                        && !typeof(Response).IsAssignableFrom(param[2].ParameterType)))
                        {
                            Debug.LogError($"RPC消息处理器 {method.Name} 必须有3个参数, 左侧 Session，中间 Request，右侧 Response!");
                            continue;
                        }
                        var reqType = param[1].ParameterType;
                        var rspType = param[2].ParameterType;
                        if (!rpcHandlers.TryGetValue(reqType, out var rpcInfo))
                        {
                            rpcInfo = new RpcHandlerInfo
                            {
                                method = method,
                                Request = reqType,
                                Response = rspType
                            };
                            rpcHandlers.Add(reqType, rpcInfo);
                        }
                        else
                        {
                            Debug.LogError($"{nameof(MessageManager)}: 请不要重复注册 {reqType.Name} 处理器，此消息已被 {rpcInfo.method.DeclaringType.Name}.{rpcInfo.method.Name}中处理 ");
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        internal static void HandleNormalMessage(Session session, IMessage message)
        {
            if (normalHandlers.TryGetValue(message.GetType(), out var info))
            {
                info.method.Invoke(null, new object[] { session, message });
            }
        }

        internal static async void HandleRpcRequest(Session session, IRequest request)
        {
            var type = request.GetType();
            IResponse response;
            if (rpcHandlers.TryGetValue(type, out var info))
            {
                if (rpcMessagePairs.TryGetValue(type, out var responseType))
                {
                    response = Activator.CreateInstance(responseType) as IResponse;
                    response.Id = request.Id;
                    await (Task)info.method.Invoke(null, new object[] { session, request, response });
                }
                else
                {
                    response = new Response
                    {
                        Id = request.Id,
                        Error = $"RPC 消息 {request.GetType().Name} 没有找到对应的 Response 类型！"
                    };
                }
            }
            else
            {
                response = new Response
                {
                    Id = request.Id,
                    Error = $"RPC 消息 {request.GetType().Name} 没有找到对应的处理器！"
                };
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

        // 获取消息对应的 Response 类型
        public static Type GetResponseType([NotNull] IRequest request)
        {
            if (!rpcMessagePairs.TryGetValue(request.GetType(), out var type))
            {
                throw new Exception($"RPC 消息  Request-Response 为正确完成映射，请参考示例正确注册映射关系！");
            }
            return type;
        }
        class NormalHandlerInfo : BaseHandlerInfo
        {
            public Type Message;
        }
        class BaseHandlerInfo
        {
            public MethodInfo method;
        }
        class RpcHandlerInfo : BaseHandlerInfo
        {
            public Type Request;
            public Type Response;
        }
    }
}
