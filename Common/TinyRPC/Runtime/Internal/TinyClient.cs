using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using zFramework.TinyRPC.Messages;
using System.Threading.Tasks;
using zFramework.TinyRPC.Exceptions;
using zFramework.TinyRPC.Settings;
using static zFramework.TinyRPC.ObjectPool;

namespace zFramework.TinyRPC
{
    public class TinyClient
    {
        public bool IsConnected => Session != null && Session.IsAlive;
        public Session Session { get; private set; }
        /// <summary>
        /// 当客户端连接成功时触发
        /// </summary>
        public Action OnClientEstablished;
        /// <summary>
        /// 当客户端断开连接时触发
        /// </summary>
        public Action OnClientDisconnected;
        /// <summary>
        ///  当 Ping 值计算完成时触发
        ///  <br>参数1：服务器与客户端时间差，用于在客户端上换算服务器时间</br>
        ///  <br>参数2：ping 值</br>
        /// </summary>
        public Action<float, int> OnPingCaculated;

        public TinyClient(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
            client = new TcpClient();
            source = new CancellationTokenSource();
            context = SynchronizationContext.Current;
            // alert must in main thread, otherwise throw exception
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new Exception("TinyClient.Start must be called in main thread!");
            }
        }

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(async () =>
             {
                 try
                 {
                     await client.ConnectAsync(ip, port);
                     if (source.IsCancellationRequested) return false;
                     Session = new Session(client, context, false);
                     context.Post(v => OnClientEstablished?.Invoke(), null);
                     _ = Task.Run(ReceiveAsync);
                     context.Post(v => _ = PingAsync(), null);
                     return true;
                 }
                 catch (Exception e)
                 {
                     Debug.LogError($"连接服务器失败！{e}");
                     return false;
                 }
             }, source.Token);
        }

        public void Stop()
        {
            if (!IsConnected) return;
            context.Post(v => OnClientDisconnected?.Invoke(), null);
            source?.Cancel();
            Session?.Close();
            source = null;
            client = null;
        }

        public void Send(Message message)
        {
            try
            {
                Session?.Send(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"TinyClient Send Message Error! {e}");
                Stop();
            }
        }

        public async Task<T> Call<T>(IRequest request) where T : class, IResponse, new()
        {
            T response = null;
            try
            {
                response = await Session?.Call<T>(request);
            }
            // RpcResponseException  和 TimeoutException 不应该关断会话,同时应该上报，Ping 需要知道
            //未知异常直接关断会话
            //如果还有其他已知不应该关断会话的可以在这里插入
            catch (Exception e) when (e is not RpcResponseException && e is not TimeoutException)
            {
                Debug.LogError($"{nameof(TinyClient)}: RPC Error {e}");
                Stop();
            }
            return response;
        }

        private async Task PingAsync()
        {
            var count = 1;
            while (IsConnected)
            {
                try
                {
                    var begin = DateTime.Now;
                    var pooled = Allocate<Ping>();
                    var response = await Call<Ping>(pooled);
                    var end = DateTime.Now;
                    var ping = (end - begin).Milliseconds;
                    // 服务器与客户端的时间差，用于在客户端上换算服务器时间
                    var delta = (response.time - ClientTime) / 10000.0f + ping / 2;
                    OnPingCaculated?.Invoke(delta, ping);
                    await Task.Delay(TinyRpcSettings.Instance.pingInterval);//这个API 决定了必须在主线程调用
                }
                // 只有当收到的异常是 RpcResponseException  或者 TimeoutException 时才重试
                catch (Exception e) when (e is RpcResponseException || e is TimeoutException)
                {
                    Debug.LogError($"{nameof(TinyClient)}: Ping Error {e} ， retry count  = {count}");
                    count++;
                    //应该稍作延迟，这可能在网络状态得到缓和的情况下加大 ping 通的几率
                    await Task.Delay(TinyRpcSettings.Instance.pingInterval);
                    if (count > TinyRpcSettings.Instance.pingRetry)
                    {
                        Stop();
                        Debug.LogError($"{nameof(TinyClient)}: Ping Timeout ，Session is inactive!");
                    }
                }
                // 其他异常就没有重试的必要了，直接关断会话
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(TinyClient)}: Ping Error {e} , Session Stoped ");
                    Stop();
                }
            }
        }

        // 需要对 session ReceiveAsync 进行异常处理
        // 故而先前的调用（见 git 历史）被修正为当前你看到的这种状态
        // 同理可得，Send、Call 皆是如此
        private async void ReceiveAsync()
        {
            while (!source.Token.IsCancellationRequested)
            {
                try
                {
                    await Session.ReceiveAsync();
                }
                catch (Exception e) when (e is not RpcResponseException && e is not TimeoutException)
                {
                    Debug.LogError($"{nameof(TinyClient)}: Receive Error {e}");
                    Stop();
                    break;
                }
            }
        }

        private readonly string ip = "172.0.0.1";
        private readonly int port = 12345;
        private TcpClient client;
        private CancellationTokenSource source;
        private SynchronizationContext context;
        private static long ClientTime => DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    }
}
