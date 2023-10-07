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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Awake()
        {
            rpcInfoPairs.Clear();
            StoreRPCMessagePairs();
            RegisterAllHandlers();
        }

        public static void StoreRPCMessagePairs()
        {
            rpcMessagePairs.Clear();
            // store all rpc message pairs
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
                Debug.Log($"{nameof(MessageManager)}: 请保证 生成的网络消息在 “com.zframework.tinyrpc.generated” 程序集下");
            }
        }

        public static void RegisterAllHandlers()
        {
            normalHandlers.Clear();
            rpcHandlers.Clear();

            // store all message handlers
            var handlers = AppDomain.CurrentDomain.GetAssemblies()
                .Where(v => TinyRpcSettings.Instance.AssemblyNames.Contains(v.FullName.Split(',')[0]))
                .SelectMany(v => v.GetTypes())
                .Where(v => v.GetCustomAttribute<MessageHandlerProviderAttribute>() != null);
            foreach (var handler in handlers)
            {
                RegisterHandler(handler);
            }
        }

        public static void RegisterHandler(Type type)
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

        internal static void HandleNormalMessage(Session session, Message message)
        {
            if (normalHandlers.TryGetValue(message.GetType(), out var info))
            {
                info.method.Invoke(null, new object[] { session, message });
            }
        }

        internal static async void HandleRpcRequest(Session session, Request request)
        {
            if (rpcHandlers.TryGetValue(request.GetType(), out var info))
            {
                if (rpcMessagePairs.TryGetValue(info.Request, out var responseType))
                {
                    var response = Activator.CreateInstance(responseType) as Response;
                    response.id = request.id;
                    Debug.Log($"{nameof(MessageManager)}:  1111");
                    var task = info.method.Invoke(null, new object[] { session, request, response });
                    await (task as Task);
                    Debug.Log($"{nameof(MessageManager)}:  4444");
                    session.Reply(response);
                    return;
                }
            }
            var response_fallback = new Response
            {
                id = request.id,
                error = $"RPC 消息 {request.GetType().Name} 没有找到对应的处理器！"
            };
            session.Reply(response_fallback);
        }
        internal static void HandleRpcResponse(Session session, Response response)
        {
            if (rpcInfoPairs.TryGetValue(response.id, out var rpcInfo))
            {
                rpcInfo.task.SetResult(response);
                rpcInfoPairs.Remove(response.id);
            }
        }

        internal static Task<Response> AddRpcTask(Request request)
        {
            var tcs = new TaskCompletionSource<Response>();
            var cts = new CancellationTokenSource();
            var timeout = Mathf.Max(request.timeout, 5);
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
        public static Type GetResponseType([NotNull] Request request)
        {
            if (!rpcMessagePairs.TryGetValue(request.GetType(), out var type))
            {
                throw new Exception($"RPC 消息  Request-Response 为正确完成映射，请参考示例正确注册映射关系！");
            }
            return type;
        }

        //todo: Normal Message 与 RPC Message 的处理器注册机制不一样，需要分开处理
        // Normal Message 的处理器注册机制：
        // 使用 AddSignal<T>(OnXXXXMessageReceived) 添加消息处理器，T 为消息类型
        // 使用 RemoveSignal<T>(OnXXXXMessageReceived) 移除消息处理器，T 为消息类型
        // Normal 消息，必须有2个参数，第一个参数为 Session，第二个参数为 Message 
        // 示例如下：
        /*
         MessageManager.AddSignal<TestClass>(OnTestClassMessageReceived);
         private static void OnTestClassMessageReceived(Session session, TestClass message)
         {
             Debug.Log($"{nameof(MessageManager)}: 收到 {session} message {message}");
         }
         */

        //todo ：加入 message id 机制, 用于快速获取消息Type,方便调试
        // 我需要写一些代码分析器，实现以下功能：
        // 约定：MessageHandlerAttribute 的出现触发代码分析器
        //
        // 关于 IL 代码注入：
        //1. 取消 MessageHandlerAttribute 的使用，改为自动注册，但要求 il 代码注入
        // 2. 使用 IL 代码注入，自动为包含了 MessageHandler 的 Type 生成静态构造函数（如果有则插入逻辑），用于注册 MessageHandler
        // 3. 如果用户删除了 MessageHandlerAttribute，自动删除静态构造函数
        // 4. 如果用户自己写了静态构造函数，不要自动删除，但剔除 MessageHandlerAttribute 的注册逻辑
        //
        // 关于撰写 Server 端 handler是否符合规范：
        // 0. 不管是 Normal 消息还是 RPC 消息，必须是静态方法且 RPC 消息返回值必须是 Task
        // 1. 如果是 RPC 消息，必须有3个参数，第一个参数为 Session，第二个参数为 Request，第三个参数为 Response
        // 2. 如果是 Normal 消息，必须有2个参数，第一个参数为 Session，第二个参数为 Message
        // 3.  RPC Hanlder 理应在客户端上也能实现，进而实现 Server call Client 
        // 4.  RPC Handler 的 Request 需要上报 RPC Server（可能是客户端）没有实现 Handler 的情况

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