using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
using zFramework.TinyRPC.Exceptions;
using zFramework.TinyRPC.Settings;
using static zFramework.TinyRPC.Manager;
using static zFramework.TinyRPC.ObjectPool;
using System.Collections.Concurrent;

namespace zFramework.TinyRPC
{
    public class Session
    {
        public bool IsServerSide { get; }
        public bool IsAlive { get; private set; }
        public IPEndPoint IPEndPoint { get; private set; }
        private readonly TinyRpcSettings settings;
        private object locker = new();

        //todo: TcpClient will be replaced by Transport, so that we can use other transport protocol such as kcp and websocket
        //todo: TcpClient 将被 Transport 替换，这样我们就可以使用其他传输协议，如 kcp 和 websocket
        internal Session(TcpClient client, SynchronizationContext context, bool isServerSide)
        {
            IsServerSide = isServerSide;
            this.client = client;
            //Settings can only be created in main thread,Preload if needed
            settings = TinyRpcSettings.Instance;

            // 深度拷贝 IPEndPoint 用于在任意时候描述 Session （只要这个 Session 还能被访问）
            var iPEndPoint = (IsServerSide ? client.Client.RemoteEndPoint : client.Client.LocalEndPoint) as IPEndPoint;
            var address = IPAddress.Parse(iPEndPoint.Address.ToString());
            var port = iPEndPoint.Port;
            IPEndPoint = new IPEndPoint(address, port);

            this.source = new CancellationTokenSource();
            this.context = context;
            IsAlive = true;
        }

        internal void Send(IMessage message)
        {
            //todo :need a lock ?
            var bytes = SerializeHelper.Serialize(message);
            if (IsAlive)
            {
                var stream = client.GetStream();
                var head = BitConverter.GetBytes(bytes.Length);
                lock (locker)
                {
                    stream.Write(head, 0, head.Length);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
            }
            else
            {
                Debug.LogWarning($"{nameof(Session)}: 消息发送失败，会话已失效！");
            }
        }
        #region RPC 
        internal void Reply(IMessage message) => Send(message);
        
        ///<summary>
        /// 调用RPC方法并返回响应结果。
        /// </summary>
        /// <typeparam name="T">响应结果的类型。</typeparam>
        /// <param name="request">RPC请求。</param>
        /// <returns>响应结果。</returns>
        /// <exception cref="RpcResponseException">Rpc 响应时抛出的异常</exception>
        internal async Task<T> Call<T>(IRequest request) where T : class, IResponse, new()
        {
            using var _ = request;
            // 校验 RPC 消息匹配
            var type = GetResponseType(request);
            if (type != typeof(T))
            {
                throw new Exception($"RPC Response 消息类型不匹配, 期望值： {type},传入值 {typeof(T)}");
            }
            // 原子操作，保证 id 永远自增 1且不会溢出,溢出就从0开始
            Interlocked.CompareExchange(ref id, 0, int.MaxValue);
            request.Rid = Interlocked.Increment(ref id);

            Send(request);  // do not catch any exception here,just let it throw out
            var response = await RpcWaitingTask(request);

            if (!string.IsNullOrEmpty(response.Error))// 如果服务器告知了错误！
            {
                throw new RpcResponseException($"Rpc Handler Error :{response.Error}");
            }
            return response as T;
        }
        
        internal void HandleResponse(IResponse response)
        {
            if (rpcInfoPairs.TryRemove(response.Rid, out var rpcInfo))
            {
                rpcInfo.source.Dispose();
                rpcInfo.task.SetResult(response);
            }
            else if (settings.logEnabled)
            {
                Debug.LogWarning($"{nameof(Session)}:RpcInfoPairs TryRemove id = [{response.Rid}] Error!");
            }
        }

        internal Task<IResponse> RpcWaitingTask(IRequest request)
        {
            var tcs = new TaskCompletionSource<IResponse>();
            var cts = new CancellationTokenSource();
            //等待并给定特定时长的响应机会，这在发生复杂操作时很有效
            var timeout = Mathf.Max(request.Timeout, settings.rpcTimeout);
            cts.CancelAfter(timeout);
            var exception = new TimeoutException($"RPC Call Timeout! Request: {request}");
            cts.Token.Register(() =>
            {
                if (!rpcInfoPairs.TryRemove(request.Rid, out _))
                {
                    Debug.LogError($"{nameof(Session)}:RpcInfoPairs TryRemove id:[{request.Rid}] Error!");
                }
                tcs.TrySetException(exception);
            }, useSynchronizationContext: false);
            var rpcinfo = new RpcInfo
            {
                id = request.Rid,
                task = tcs,
                source = cts
            };
            if (!rpcInfoPairs.TryAdd(request.Rid, rpcinfo))
            {
                Debug.LogError($"{nameof(Session)}:RpcInfoPairs TryAdd [{request}] Error!");
            }
            return tcs.Task;
        }

        #endregion
        internal async Task ReceiveAsync()
        {
            var stream = client.GetStream();
            while (!source.IsCancellationRequested)
            {
                // 读出消息的长度
                var head = new byte[4];
                var byteReaded = await stream.ReadAsync(head, 0, head.Length, source.Token);
                if (byteReaded == 0)
                {
                    throw new Exception("在读取消息头时网络连接意外断开！");
                }
                // 读出消息的内容
                var bodySize = BitConverter.ToInt32(head, 0);
                var body = new byte[bodySize];
                byteReaded = 0;
                // 当读取到 body size 后的数据读取需要加入超时检测
                var cts = new CancellationTokenSource();
                // 8s 超时，8秒可以读到的数据量是惊人的
                // 如果超时，说明要么断线，要么是对方恶意发来的数据，直接断开连接
                cts.CancelAfter(TimeSpan.FromSeconds(8));
                while (byteReaded < bodySize)
                {
                    var readed = await stream.ReadAsync(body, byteReaded, body.Length - byteReaded, cts.Token);
                    // 读着读着就断线了的情况，如果不处理，此处会产生死循环
                    if (readed == 0)
                    {
                        throw new Exception("在读取消息体时网络连接意外断开！");
                    }
                    byteReaded += readed;
                }
                if (bodySize != byteReaded) // 消息不完整，此为异常，断开连接
                {
                    throw new Exception("在读取网络消息时得到了不完整数据,会话断开！");
                }
                // 解析消息类型
                OnMessageReceived(body);
            }
        }

        private void OnMessageReceived(byte[] content)
        {
            var message = SerializeHelper.Deserialize(content);
            if (!settings.logFilters.Contains(message.GetType().Name))
            {
                Debug.Log($"{nameof(Session)}:   {(IsServerSide ? "Server" : "Client")} 收到网络消息 = {message.GetType().Name}  {message}");
            }
            //处理 rpc request
            if (message is Request || (message is Ping && IsServerSide))
            {
                HandleRequest(this, message as IRequest);
            }
            else if (message is Response || (message is Ping && !IsServerSide))
            {
                // 处理 rpc response
                // 提示：由于时机问题，message（Response） 无法在框架内部回收，用户需要按需回收或者交给GC
                HandleResponse(message as IResponse);
            }
            else
            {
                //处理 normal message
                HandleMessage(this, message);
            }
        }
        public override string ToString() => $"Session: {IPEndPoint}  IsServer:{IsServerSide}";
        public void Close()
        {
            client?.Dispose();
            source?.Dispose();

            foreach (var rpcInfo in rpcInfoPairs.Values)
            {
                rpcInfo.source.Dispose();
                rpcInfo.task.SetCanceled();
            }
            rpcInfoPairs.Clear();

            IsAlive = false;
        }
        private int id = 0;
        private readonly TcpClient client;
        private readonly CancellationTokenSource source;
        private readonly SynchronizationContext context;
        private readonly ConcurrentDictionary<int, RpcInfo> rpcInfoPairs = new(); // RpcId + RpcInfo

    }
}
