#pragma warning disable IDE0060 // 删除未使用的参数
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    using static Manager;
    public static class MessageHandlerEx
    {
        #region For Monobehaviour
        /// <summary>
        ///  添加常规网络消息的监听
        /// </summary>
        /// <typeparam name="T">请求类型</typeparam>
        /// <param name="component">Unity 组件</param>
        /// <param name="task">收到网络消息时的回调</param>
        /// <param name="priority">优先级，值越大越优先</param>
        public static void AddNetworkSignal<T>(this Component component, Action<Session, T> task, int priority = 1) where T : class, IMessage
        {
            AddNetworkSignal(task, priority);
        }
        public static void RemoveNetworkSignal<T>(this Component component, Action<Session, T> task) where T : class, IMessage
        {
            RemoveNetworkSignal(task);
        }

        public static void AddNetworkSignal<Request, Response>(this Component component, Func<Session, Request, Response, Task> task) where Request : IRequest where Response : IResponse
        {
            AddNetworkSignal(task);
        }

        public static void RemoveNetworkSignal<Request, Response>(this Component component, Func<Session, Request, Response, Task> task) where Request : IRequest where Response : IResponse
        {
            RemoveNetworkSignal(task);
        }
        #endregion

        #region For Static Type Or MessageHandlerAttribute

        /// <summary>
        ///  添加常规网络消息的监听
        /// </summary>
        /// <typeparam name="T">请求类型</typeparam>
        /// <param name="component">Unity 组件</param>
        /// <param name="task">收到网络消息时的回调</param>
        /// <param name="priority">优先级，值越大越优先</param>
        public static void AddNetworkSignal<T>(Action<Session, T> task, int priority = 1) where T : class, IMessage
        {
            if (NormalMessageHandlers.TryGetValue(typeof(T), out var handler))
            {
#if UNITY_EDITOR
                // we should validate the task here , the task need to be added into handler must not marked with MessageHandlerAttribute as it will be added by reference later !
                // as RuntimeInitializeOnLoadMethod is more earlier  then This API, so I validate task  here,  error occurs in editor mode only.
                var method = task.Method;
                var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
                if (attr != null)
                {
                    throw new ArgumentException($" {method.DeclaringType}.{method.Name} 注册失败，该方法已经被 {attr} 标记，请不要重复注册！");
                }
#endif
                (handler as NormalMessageHandler<T>).AddTask(task, priority);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}:监听 {task.Method.DeclaringType}.{task.Method.Name} 注册失败， {typeof(T)} 消息处理器实例未找到！");
            }
        }
        public static void RemoveNetworkSignal<T>(Action<Session, T> task) where T : class, IMessage
        {
            if (NormalMessageHandlers.TryGetValue(typeof(T), out var handler))
            {
                (handler as NormalMessageHandler<T>).RemoveTask(task);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}:监听 {task.Method.DeclaringType}.{task.Method.Name} 注销失败， {typeof(T)} 消息处理器实例未找到！");
            }
        }

        public static void AddNetworkSignal<Request, Response>(Func<Session, Request, Response, Task> task) where Request : IRequest where Response : IResponse
        {
            if (RpcMessageHandlers.TryGetValue(typeof(Request), out var handler))
            {
#if UNITY_EDITOR
                // we should validate the task here , the task want to be added must not marked with MessageHandlerAttribute as it will be added by reference later !
                // as RuntimeInitializeOnLoadMethod is more earlier  then This API, so I validate task  here,  error occurs in editor mode only.
                var method = task.Method;
                var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
                if (attr != null)
                {
                    throw new ArgumentException($" {method.DeclaringType}.{method.Name} 注册失败，该方法已经被 {attr} 标记，请不要重复注册！");
                }
#endif
                (handler as RpcMessageHandler<Request, Response>).AddTask(task);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}:监听 {task.Method.DeclaringType}.{task.Method.Name} 注册失败， {typeof(Request)} 消息处理器实例未找到！");
            }
        }

        public static void RemoveNetworkSignal<Request, Response>(Func<Session, Request, Response, Task> task) where Request : IRequest where Response : IResponse
        {
            if (RpcMessageHandlers.TryGetValue(typeof(Request), out var handler))
            {
                (handler as RpcMessageHandler<Request, Response>).RemoveTask(task);
            }
            else
            {
                Debug.LogError($"{nameof(MessageHandlerEx)}:监听 {task.Method.DeclaringType}.{task.Method.Name} 注销失败， {typeof(Request)} 消息处理器实例未找到！");
            }
        }
        #endregion
    }
}
#pragma warning restore IDE0060 // 删除未使用的参数