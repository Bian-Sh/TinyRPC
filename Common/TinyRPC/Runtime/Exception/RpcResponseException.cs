using System;
namespace zFramework.TinyRPC.Exceptions
{
    public class RpcResponseException : Exception
    {
        public RpcResponseException(string message) : base(message)
        {
        }
    }
}