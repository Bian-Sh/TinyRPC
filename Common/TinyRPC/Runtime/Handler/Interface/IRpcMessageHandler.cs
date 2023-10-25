using System.Threading.Tasks;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC
{
    public interface IRpcMessageHandler
    {
        Task Invoke(Session session, IRequest request,IResponse response);
    }
}