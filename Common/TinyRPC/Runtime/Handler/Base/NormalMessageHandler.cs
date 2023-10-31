using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC
{
    public class NormalMessageHandler<T> : INormalMessageHandler where T : IMessage
    {
        readonly List<HandlerInfo> handlerInfos = new();
        public void Dispatch(Session session, IMessage message)
        {
            if (message is T t)
            {
                foreach (var handlerInfo in handlerInfos)
                {
                    try
                    {
                        handlerInfo.task.Invoke(session, t);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"{nameof(NormalMessageHandler<T>)}: exception occured when task execute ,{e}");
                    }
                }
            }
        }

        /// <summary>
        ///  添加任务
        /// </summary>
        /// <param name="task">任务</param>
        /// <param name="priority">优先级，值越大越优先</param>
        public void AddTask(Action<Session, T> task, int priority)
        {
            if (handlerInfos.Exists(v => v.task == task))
            {
                Debug.LogWarning($"{nameof(NormalMessageHandler<T>)}: task already exists");
                return;
            }
            handlerInfos.Add(new HandlerInfo { task = task, priority = priority });
            handlerInfos.Sort((a, b) => b.priority - a.priority);
        }

        ///  <inheritdoc/>
        public void Bind(MethodInfo method, int priority)
        {
            var genericTask = Delegate.CreateDelegate(typeof(Action<Session, T>), method);
            handlerInfos.Add(new HandlerInfo { task = genericTask as Action<Session, T>, priority = priority });
            handlerInfos.Sort((a, b) => b.priority - a.priority);
        }

        internal void RemoveTask(Action<Session, T> task)
        {
            var index = handlerInfos.FindIndex(v => v.task == task);
            if (index != -1)
            {
                handlerInfos.RemoveAt(index);
            }
            else
            {
                Debug.LogWarning($"{nameof(NormalMessageHandler<T>)}: task not found");
            }
        }

        struct HandlerInfo
        {
            public Action<Session, T> task;
            public int priority;
        }
    }
}
