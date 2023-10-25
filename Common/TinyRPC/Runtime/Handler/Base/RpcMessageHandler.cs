using System;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    using static MessageManager;
    public class RpcMessageHandler<Request, Response> : IRpcMessageHandler where Request : IRequest where Response : IResponse
    {
        Func<Session, Request, Response, Task> task; // can only be set once
        /// <summary>
        ///  实例化时自动注册到 rpc handlers
        /// </summary>
        /// <exception cref="ArgumentException">如果已经存在相同类型的 handler</exception>
        public RpcMessageHandler() => RpcMessageHandlers.Add(typeof(Request), this);
        public Task Invoke(Session session, IRequest request, IResponse response) => task?.Invoke(session, (Request)request, (Response)response) ?? Task.CompletedTask;
        public void AddTask(Func<Session, Request, Response, Task> task)
        {
            if (this.task != null)
            {
                Debug.LogWarning($"{nameof(RpcMessageHandler<Request, Response>)}: RPC Task already exists, info = {this}");
                return;
            }
            this.task = task;
        }
        public void RemoveTask() => task = null;
        public override string ToString() => $"RpcMessageHandler<{typeof(Request)},{typeof(Response)}>";
    }
}