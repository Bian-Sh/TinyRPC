using System.Reflection;
using System.Threading.Tasks;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    public interface IRpcMessageHandler
    {
        void AddTask(MethodInfo method);
        Task Invoke(Session session, IRequest request,IResponse response);
    }
}