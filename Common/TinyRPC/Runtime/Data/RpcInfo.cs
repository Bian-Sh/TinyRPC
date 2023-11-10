using System.Threading;
using System.Threading.Tasks;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC
{
    public struct RpcInfo
    {
        public int id;
        public TaskCompletionSource<IResponse> task;
        public CancellationTokenSource source;
    }
}