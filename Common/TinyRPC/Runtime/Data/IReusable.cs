namespace zFramework.TinyRPC
{
    public interface IReusable
    {
        bool IsRecycled { get; set; }
        void OnRecycle();
    }
}