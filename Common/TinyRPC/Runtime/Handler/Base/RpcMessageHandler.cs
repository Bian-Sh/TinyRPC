using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    public class RpcMessageHandler<Request, Response> : IRpcMessageHandler where Request : IRequest where Response : IResponse
    {
        Func<Session, Request, Response, Task> task; // can only be set once

        public Task Invoke(Session session, IRequest request, IResponse response)
        {
            if (task == null)
            {
                Debug.LogError($"{nameof(RpcMessageHandler<Request, Response>)}: RPC Task not found, info = {this}");
                response.Error = $"RPC Task not found, info = {this}";
                return Task.CompletedTask;
            }
            return task.Invoke(session, (Request)request, (Response)response);
        }

        public void AddTask(Func<Session, Request, Response, Task> task)
        {
            if (this.task != null)
            {
                Debug.LogWarning($"{nameof(RpcMessageHandler<Request, Response>)}: RPC Task already exists, info = {this}");
                return;
            }
            this.task = task;
        }

        // this is used for interface based RPC task regist
        public void AddTask(MethodInfo method)
        {
            if (this.task != null)
            {
                Debug.LogWarning($"{nameof(RpcMessageHandler<Request, Response>)}: RPC Task already exists, info = {this}");
                return;
            }
            // 使用反射将参数 task 转为泛型 task
            var genericTask = Delegate.CreateDelegate(typeof(Func<Session, Request, Response, Task>), method);
            this.task = genericTask as Func<Session, Request, Response, Task>;
        }

        public void RemoveTask(Func<Session, Request, Response, Task> task)
        {
            if (this.task != null && this.task == task)
            {
                task = null;
            }
            else
            {
                Debug.LogWarning($"{nameof(RpcMessageHandler<Request, Response>)}: RPC Task not found or registed, info = {this}");
            }
        }

        public override string ToString() => $"RpcMessageHandler<{typeof(Request)},{typeof(Response)}>";
    }
}