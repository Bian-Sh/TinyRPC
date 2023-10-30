using System;
using System.Reflection;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    public interface INormalMessageHandler
    {
        void AddTask(MethodInfo method,int priority);
        void Invoke(Session session, IMessage message);
    }
}