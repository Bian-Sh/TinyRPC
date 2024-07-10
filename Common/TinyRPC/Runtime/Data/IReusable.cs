using System;

namespace zFramework.TinyRPC
{
    public interface IReusable:IDisposable
    {
        bool RequireRecycle { get; set; }
        void OnRecycle();
    }
}