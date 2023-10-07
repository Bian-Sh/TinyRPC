using System;
public class RpcException : Exception
{
    public RpcException(string message) : base(message)
    {
    }
}
