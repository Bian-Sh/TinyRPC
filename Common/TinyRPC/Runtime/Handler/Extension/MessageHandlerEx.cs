using System;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    using static MessageManager;
    public static class MessageHandlerEx
    {
        /// <summary>
        ///  添加常规网络消息的监听
        /// </summary>
        /// <typeparam name="T">请求类型</typeparam>
        /// <param name="component">Unity 组件</param>
        /// <param name="callback">收到网络消息时的回调</param>
        /// <param name="priority">优先级</param>
        public static void AddNetworkSignal<T>(this Component component, Action<Session, T> callback, int priority = 1) where T : class, IMessage
        {
            if (NormalMessageHandlers.TryGetValue(typeof(T), out var handler))
            {
                (handler as NormalMessageHandler<T>).AddTask(callback, priority);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(T)} 消息处理器实例未找到！");
            }
        }
        public static void RemoveNetworkSignal<T>(this Component component, Action<Session, T> callback) where T : class, IMessage
        {
            if (NormalMessageHandlers.TryGetValue(typeof(T), out var handler))
            {
                (handler as NormalMessageHandler<T>).RemoveTask(callback);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(T)} 消息处理器实例未找到！");
            }
        }

        public static void AddNetworkSignal<Request, Response>(this Component component, Func<Session, Request, Response,Task> task) where Request : IRequest where Response : IResponse
        {
            if (RpcMessageHandlers.TryGetValue(typeof(Request), out var handler))
            {
                (handler as RpcMessageHandler<Request, Response>).AddTask(task);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(Request)} 消息处理器实例未找到！");
            }
        }

        public static void RemoveNetworkSignal<Request, Response>(this Component component) where Request : IRequest where Response : IResponse
        { 
            if (RpcMessageHandlers.TryGetValue(typeof(Request), out var handler))
            {
                (handler as RpcMessageHandler<Request, Response>).RemoveTask();
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(Request)} 消息处理器实例未找到！");
            }
        }
    }
}