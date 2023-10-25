using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    public interface INormalMessageHandler
    {
        void Invoke(Session session, IMessage message);
    }
}