using System;
using zFramework.TinyRPC.DataModel;
namespace zFramework.TinyRPC
{
    public interface INormalMessageHandler
    {
        void Invoke(Session session, IMessage message);
    }
}