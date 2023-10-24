using System;
using UnityEngine;
using zFramework.TinyRPC.DataModel;
using static zFramework.TinyRPC.NormalMessageHandler;
namespace zFramework.TinyRPC
{
    public static class MessageHandlerEx
    {
        public static void AddNetworkSignal<T>(this Component component, Action<Session, T> callback) where T : class, IMessage
        {
            if (handlers.TryGetValue(typeof(T), out var handler))
            {
                (handler as NormalMessageHandler<T>).AddTask(callback);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(T)} 消息处理器实例未找到！");
            }
        }
        public static void RemoveNetworkSignal<T>(this Component component, Action<Session, T> callback) where T : class, IMessage
        {
            if (handlers.TryGetValue(typeof(T), out var handler))
            {
                (handler as NormalMessageHandler<T>).RemoveTask(callback);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}: {typeof(T)} 消息处理器实例未找到！");
            }
        }
    }
}