using System;
using System.Collections.Generic;
using UnityEngine;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    public class NormalMessageHandler : INormalMessageHandler
    {
        public readonly static Dictionary<Type, INormalMessageHandler> handlers = new();

        internal static void HandleNormalMessage(Session session, IMessage message)
        {
            if (handlers.TryGetValue(message.GetType(), out var handler))
            {
                handler.Invoke(session, message);
            }
            else
            {
                Debug.LogWarning($"{nameof(NormalMessageHandler)}: no handler for message type {message.GetType()}");
            }
        }

        public virtual void Invoke(Session session, IMessage message) { }
    }
    public class NormalMessageHandler<T> : NormalMessageHandler where T : IMessage
    {
        readonly List<Action<Session, T>> tasks = new();
        /// <summary>
        ///  实例化时自动注册到 handlers
        /// </summary>
        /// <exception cref="ArgumentException">如果已经存在相同类型的 handler</exception>
        public NormalMessageHandler() => handlers.Add(typeof(T), this);

        public override void Invoke(Session session, IMessage message)
        {
            if (message is T t)
            {
                foreach (var task in tasks)
                {
                    try
                    {
                        task.Invoke(session, t);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"{nameof(NormalMessageHandler<T>)}: exception occured when task execute ,{e}");
                    }
                }
            }
        }
        public void AddTask(Action<Session, T> task)
        {
            if (tasks.Contains(task))
            {
                Debug.LogWarning($"{nameof(NormalMessageHandler<T>)}: task already exists");
                return;
            }
            tasks.Add(task);
        }

        internal void RemoveTask(Action<Session, T> task) => tasks.Remove(task);
    }
}
