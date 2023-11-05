namespace zFramework.TinyRPC.Messages
{
    public interface IRequest :IRpcMessage
    {
        /// <summary>
        /// 告诉服务器客户端可以等待此请求的持续时长，单位毫秒
        /// </summary>
        int Timeout { get; set; }
    }
}
