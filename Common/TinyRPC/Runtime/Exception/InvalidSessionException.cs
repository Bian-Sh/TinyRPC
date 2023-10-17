using System;
namespace zFramework.TinyRPC.Exceptions
{
    public class InvalidSessionException : Exception
    {
        public InvalidSessionException(string message) : base(message)
        {
        }
    }
}