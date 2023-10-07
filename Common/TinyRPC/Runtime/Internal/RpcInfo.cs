using System.Threading.Tasks;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    public struct RpcInfo
    {
        public int id;
        public TaskCompletionSource<Response> task;
    }
}