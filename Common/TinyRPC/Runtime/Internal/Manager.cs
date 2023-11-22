using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
using zFramework.TinyRPC.Settings;
using static zFramework.TinyRPC.ObjectPool;

namespace zFramework.TinyRPC
{
    // 获取所有的消息处理器解析并缓存
    // 消息处理器注册方式有 2 种：
    // 1. 使用  MessageHandlerProviderAttribute +  MessageHandlerAttribute 标记,前者标记类型，后者标记方法
    // 2. 通过 UnityEngine.Component 扩展方法 AddNetworkSignal  注册
    //
    // 约定 MessageHandlerAttribute 只能出现在静态方法上
    public static class Manager
    {
        internal static readonly Dictionary<Type, INormalMessageHandler> NormalMessageHandlers = new();
        internal static readonly Dictionary<Type, IRpcMessageHandler> RpcMessageHandlers = new();
        internal static readonly Dictionary<string, Type> MessageNameTypePairs = new(); // 记录了全部消息类型，key = 消息Type名，value = 消息类型
        static readonly Dictionary<Type, Type> rpcMessagePairs = new(); // RPC 消息对，key = Request , value = Response

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Awake()
        {
            // regist rpc message pairs must before RegistGeneratedMessageHandlers
            RegistMessagePairs();
            RegistMessageHandlers();
            // if user has regist attribute marked handler task , then regist them
            RegistAttributeMarkedHandlerTask();
        }

        public static void RegistAttributeMarkedHandlerTask()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(v => TinyRpcSettings.Instance.assemblyNames.Exists(item => v.FullName.StartsWith($"{item},")))
                .SelectMany(v => v.GetTypes())
                .Where(v => v.GetCustomAttribute<MessageHandlerProviderAttribute>() != null);

            if (types.Count() == 0)
            {
                return;
            }
            Debug.Log($"{nameof(Manager)}: {types.Count()} 个消息处理器提供者被注册，分别是：\n{string.Join("\n", types.Select(v => v.Name))}");

            foreach (var handlerProvider in types)
            {
                // get all methods marked with MessageHandlerAttribute , which must be static
                var methods = handlerProvider.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method => method.GetCustomAttribute<MessageHandlerAttribute>() != null);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
                    if (attr.type == MessageType.RPC)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 3)
                        {
                            // validate parameter , they are must be Session + IRequest + IResponse and return Task
                            var session = parameters[0];
                            if (session.ParameterType != typeof(Session))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 第一个参数必须是 Session 类型！");
                                continue;
                            }
                            var request = parameters[1];
                            if (!request.ParameterType.IsSubclassOf(typeof(Request)))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 第二个参数必须是 Request 类型！");
                                continue;
                            }
                            var response = parameters[2];
                            if (!response.ParameterType.IsSubclassOf(typeof(Response)))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 第三个参数必须是 Response 类型！");
                                continue;
                            }
                            // check response type is match request type
                            var responseType = GetResponseType(request.ParameterType);
                            if (responseType != response.ParameterType)
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name}  响应类型是{response.Name},但期望值是 {responseType.Name}！");
                                continue;
                            }
                            // check return type is Task
                            if (method.ReturnType != typeof(Task))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name}  必须返回 Task 类型！");
                                continue;
                            }
                            // now get the specify handler with request type
                            var handler = RpcMessageHandlers[request.ParameterType];
                            // add this method to handler directly
                            handler.Bind(method);
                            Debug.Log($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} RPC 消息处理器处理逻辑注册成功！");
                        }
                        else
                        {
                            Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} RPC 消息处理器注册失败，参数数量不匹配！");
                        }
                    }
                    else if (attr.type == MessageType.Normal)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 2)
                        {
                            // validate parameter , they are must be Session + IMessage and return void
                            var session = parameters[0];
                            if (session.ParameterType != typeof(Session))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 第一个参数必须是 Session 类型！");
                                continue;
                            }
                            var message = parameters[1];
                            if (!message.ParameterType.IsSubclassOf(typeof(Message)))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 第二个参数必须是 Message 类型！");
                                continue;
                            }
                            // check return type is void
                            if (method.ReturnType != typeof(void))
                            {
                                Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name}  必须返回 void 类型！");
                                continue;
                            }
                            // now get the specify handler with request type
                            var handler = NormalMessageHandlers[message.ParameterType];
                            // add this method to handler directly
                            handler.Bind(method, attr.priority);
                            Debug.Log($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 消息处理器处理逻辑注册成功！");
                        }
                        else
                        {
                            Debug.LogError($"{nameof(Manager)}: {handlerProvider.Name}.{method.Name} 常规消息处理器注册失败，参数数量不匹配！");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 根据 generated  的消息和 内置的 Ping消息，注册消息处理器实例
        /// </summary>
        public static void RegistMessageHandlers()
        {
            //Clean message pairs each time, allowing for multiple calls to this API, suitable for hotfix usage scenarios.
            NormalMessageHandlers.Clear();
            RpcMessageHandlers.Clear();
            // regist internal ping handler
            var pingHandler = new RpcMessageHandler<Ping, Ping>();
            pingHandler.AddTask(TinyServer.OnPingReceived);
            RpcMessageHandlers.Add(typeof(Ping), pingHandler);

            var types = Assembly.Load("com.zframework.tinyrpc.generated")
                .GetTypes();

            // use reflection to regist rpc message handlers
            var requests = types.Where(type => type.IsSubclassOf(typeof(Request)));
            foreach (var type in requests)
            {
                var handler = Activator.CreateInstance(typeof(RpcMessageHandler<,>)
                    .MakeGenericType(type, GetResponseType(type))) as IRpcMessageHandler;
                try
                {
                    RpcMessageHandlers.Add(type, handler);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{nameof(Manager)}: RPC 消息对 {type.Name} - {GetResponseType(type).Name} 注册失败， {e} ");
                }
            }

            // use reflection to regist normal message handlers
            var messages = types.Where(type => type.IsSubclassOf(typeof(Message)));
            foreach (var type in messages)
            {
                var handler = Activator.CreateInstance(typeof(NormalMessageHandler<>).MakeGenericType(type)) as INormalMessageHandler;
                try
                {
                    NormalMessageHandlers.Add(type, handler);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{nameof(Manager)}: 消息 {type.Name} 注册失败， {e} ");
                }
            }
        }

        public static void RegistMessagePairs()
        {
            //Clean message pairs each time, allowing for multiple calls to this API, suitable for hotfix usage scenarios.
            rpcMessagePairs.Clear();
            MessageNameTypePairs.Clear();
            // regist internal ping message 
            rpcMessagePairs.Add(typeof(Ping), typeof(Ping));
            MessageNameTypePairs.Add(nameof(Ping), typeof(Ping));
            // regist response for fallback 
            MessageNameTypePairs.Add(nameof(Response), typeof(Response));

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(v => v.FullName.StartsWith("com.zframework.tinyrpc.generated"));

            if (assembly != null)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    // store all messages by their type name , which is used to get type by name
                    if (typeof(IMessage).IsAssignableFrom(type))
                    {
                        MessageNameTypePairs.Add(type.Name, type);
                        // store rpc message pairs
                        if (type.IsSubclassOf(typeof(Request)))
                        {
                            var attr = type.GetCustomAttribute<ResponseTypeAttribute>();
                            if (attr != null)
                            {
                                try
                                {
                                    rpcMessagePairs.Add(type, attr.Type);
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"{nameof(Manager)}: RPC 消息对 {type.Name} - {attr.Type} 注册失败， {e} ");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException($"{nameof(Manager)}: 请务必为 {type.Name} 通过 ResponseTypeAttribute 配置 Response 消息！");
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"{nameof(Manager)}: 请保证 生成的网络消息在 “com.zframework.tinyrpc.generated” 程序集下");
            }
        }

        internal static void HandleMessage(Session session, IMessage message)
        {
            if (NormalMessageHandlers.TryGetValue(message.GetType(), out var handler))
            {
                handler.Dispatch(session, message);
            }
            else
            {
                Debug.LogWarning($"{nameof(Manager)}: no handler for message type {message.GetType()}");
            }
        }

        internal static async void HandleRequest(Session session, IRequest request)
        {
            var type = request.GetType();
            var error = string.Empty;
            IResponse response;
            if (!rpcMessagePairs.TryGetValue(type, out var responseType))
            {
                // 几乎没有可能发生，因为在注册时已经做了检查，除非消息程序集 "com.zframework.tinyrpc.generated" 被恶意修改
                error = $"RPC 请求 {request.GetType().Name} 没有找到对应的 Response 类型！";
                response = new Response
                {
                    Rid = request.Rid,
                    Error = error
                };
                Debug.LogWarning($"{nameof(Manager)}: {error}");
                session.Reply(response);
                return;
            }

            response = Allocate(responseType) as IResponse;
            response.Rid = request.Rid;

            if (RpcMessageHandlers.TryGetValue(type, out var handler))
            {
                try
                {
                    await handler.Dispatch(session, request, response);
                }
                catch (Exception e)
                {
                    // MessageHandler内部异常，告知用户~
                    error = $"RPC 消息 {request.GetType().Name} MessageHandler 执行过程中发生异常！{e}";
                    Debug.LogError($"{nameof(Manager)}: {error}");
                }
            }
            else
            {
                // 几乎没有可能发生，因为使用了反射技术为所有 request 注册了消息处理器
                error = $"RPC 消息 {request.GetType().Name} 没有找到对应的处理器！";
                Debug.LogWarning($"{nameof(Manager)}: {error}");
            }
            //只有存在错误时赋值，避免handler内部提供的错误被覆盖
            if (!string.IsNullOrEmpty(error))
            {
                response.Error = error;
            }
            session.Reply(response);
        }

        // 获取消息对应的 Response 类型
        public static Type GetMessageType(string name)
        {
            if (MessageNameTypePairs.TryGetValue(name, out var type))
            {
                return type;
            }
            else
            {
                throw new Exception($"没有找到名为 {name} 的消息类型！");
            }
        }
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
