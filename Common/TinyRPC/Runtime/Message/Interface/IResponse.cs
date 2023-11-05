namespace zFramework.TinyRPC.Messages
{
    public interface IResponse : IRpcMessage
    {
        /// <summary>
        /// 服务器处理请求的结果, null or empty 表示成功,否则输出失败原因
        /// </summary>
        string Error { get; set; }
    }
}
