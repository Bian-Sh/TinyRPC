using System;
namespace zFramework.TinyRPC.Exceptions
{
    public class RpcTimeoutException : Exception
    {
        public RpcTimeoutException(string message) : base(message)
        {
        }
    }
}