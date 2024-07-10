using System;
using UnityEngine;
using zFramework.TinyRPC.Messages;

namespace zFramework.TinyRPC
{
    // Ping 比较特殊，是自我响应的 RPC 消息,内部默认注册
    [Serializable]
    public class Ping : IRequest, IResponse
    {
        public long time;// 对方 session 反馈的时间，client 用于计算与服务器时间的差值
        public string error;
        public int id;
        /// <inheritdoc/>
        public int Rid { get => id; set => id = value; }
        public int Timeout { get; set; } // 在 Ping 对传中采用默认timeout,无需序列化报告给对方
        public string Error { get => error; set => error = value; }
        bool IReusable.RequireRecycle { get; set; }
        void IDisposable.Dispose() => ObjectPool.Recycle(this);
        public void OnRecycle()
        {
            time = default;
            error = default;
            id = default;
        }
        public override string ToString() => JsonUtility.ToJson(this);
    }
}
