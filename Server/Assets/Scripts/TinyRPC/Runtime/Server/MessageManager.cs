using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    public struct RpcInfo
    {
        public int id;
        public TaskCompletionSource<Response> task;
    }

    // 获取所有的消息处理器解析并缓存
    // 消息处理器是被 MessageHandlerAttribute 标记的方法
    public static class MessageManager
    {
        static readonly Dictionary<Type, NormalHandlerInfo> normalHandlers = new();
        static readonly Dictionary<Type, RpcHandlerInfo> rpcHandlers = new();
        static readonly Dictionary<MonoBehaviour, List<Type>> allMessages = new();
        static readonly Dictionary<Type, Type> rpcMessagePairs = new(); // RPC 消息对，key = Request , value = Response
        static readonly Dictionary<int, RpcInfo> rpcInfoPairs = new(); // RpcId + RpcInfo

        //通过反射获取所有的RPC消息映射
        // 约定消息必须存在于同一个叫做 ：com.zframework.tinyrpc.generate 程序集中
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Awake()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(v => v.FullName == "com.zframework.tinyrpc.generated");
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
        }

        public static void RegisterHandler(MonoBehaviour instance)
        {
            var type = instance.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var param = method.GetParameters();
                if (param.Length == 0)
                {
                    Debug.LogError($"MessageHandler {method.Name} 消息处理器至少有一个参数！");
                    continue;
                }
                var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
                if (attr != null)
                {
                    if (!allMessages.TryGetValue(instance, out var list))
                    {
                        list = new List<Type>();
                        allMessages.Add(instance, list);
                    }

                    switch (attr.type)
                    {
                        case MessageType.Ping:
                            Debug.LogError($"{nameof(MessageManager)}: Ping 为内置消息，不支持自定义 MessageHandler");
                            continue;
                        case MessageType.Normal:
                            if (param.Length != 2
                                || (param.Length == 2 && param[0].ParameterType != typeof(Session) && !typeof(Message).IsAssignableFrom(param[1].ParameterType)))
                            {
                                Debug.LogError($"常规消息处理器 {method.Name} 必须有2个参数, 左侧 Session，右侧 Message!");
                                continue;
                            }
                            var msgType = param[1].ParameterType;
                            list.Add(msgType);
                            if (!normalHandlers.TryGetValue(msgType, out var info))
                            {
                                info = new NormalHandlerInfo
                                {
                                    instance = instance,
                                    method = method,
                                    Message = msgType
                                };
                                normalHandlers.Add(msgType, info);
                            }
                            else
                            {
                                if (info.instance != null)
                                {
                                    Debug.LogError($"{nameof(MessageManager)}: 请不要重复注册 {msgType.Name} 处理器，此消息已被 {info.instance.GetType().Name}.{info.method.Name}中处理 ");
                                }
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
                            list.Add(reqType);
                            if (!rpcHandlers.TryGetValue(reqType, out var rpcInfo))
                            {
                                rpcInfo = new RpcHandlerInfo
                                {
                                    instance = instance,
                                    method = method,
                                    Request = reqType,
                                    Response = rspType
                                };
                                rpcHandlers.Add(reqType, rpcInfo);
                            }
                            else
                            {
                                if (rpcInfo.instance != null)
                                {
                                    Debug.LogError($"{nameof(MessageManager)}: 请不要重复注册 {reqType.Name} 处理器，此消息已被 {rpcInfo.instance.GetType().Name}.{rpcInfo.method.Name}中处理 ");
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        internal static void HandleNormalMessage(Session session, Message message)
        {
            if (normalHandlers.TryGetValue(message.GetType(), out var info))
            {
                info.method.Invoke(info.instance, new object[] { session, message });
            }
        }

        internal static void HandleRpcMessage(Session session, Request request)
        {
            if (rpcHandlers.TryGetValue(request.GetType(), out var info))
            {
                if (rpcMessagePairs.TryGetValue(info.Request, out var responseType))
                {
                    var response = Activator.CreateInstance(responseType) as Response;
                    response.id = request.id;
                    info.method.Invoke(info.instance, new object[] { session, request, response });
                    session.Send(response); //reply                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
                }
            }
        }
        internal static void HandleRpcResponse(Session session, Response response)
        {
            if (rpcInfoPairs.TryGetValue(response.id, out var rpcInfo))
            {
                rpcInfo.task.SetResult(response);
                rpcInfoPairs.Remove(response.id);
            }
        }

        internal static Task<Response> AddRpcTask(Request request, int timeout)
        {
            var tcs = new TaskCompletionSource<Response>();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            var rpcinfo = new RpcInfo
            {
                id = request.id,
                task = tcs,
            };
            rpcInfoPairs.Add(request.id, rpcinfo);
            return tcs.Task;
        }

        // 获取消息对应的 Response 类型
        public static Type GetResponseType(Request request)
        {
            return rpcMessagePairs[request.GetType()];
        }


        internal static void UnRegisterHandler(MonoBehaviour target)
        {
            if (allMessages.TryGetValue(target, out var list))
            {
                foreach (var item in list)
                {
                    if (typeof(Request).IsAssignableFrom(item))
                    {
                        rpcHandlers.Remove(item);
                    }
                    else
                    {
                        normalHandlers.Remove(item);
                    }
                }
            };
        }

        class NormalHandlerInfo : BaseHandlerInfo
        {
            public Type Message;
        }
        class BaseHandlerInfo
        {
            public MethodInfo method;
            public object instance;
        }
        class RpcHandlerInfo : BaseHandlerInfo
        {
            public Type Request;
            public Type Response;
        }
    }
}