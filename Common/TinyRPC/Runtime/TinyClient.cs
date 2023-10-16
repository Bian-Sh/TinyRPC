using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using zFramework.TinyRPC.DataModel;
using System.Threading.Tasks;

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

        public int PingInterval = 2000;
        public int PingCount = 3; // retry 3 times if ping timeout

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
                     try
                     {
                         _ = Task.Run(Session.ReceiveAsync);
                         // ping 在主线程上下文执行 ,避免多线程导致的对 NetworkStream 资源竞争
                         context.Post(v => _ = PingAsync(), null);
                     }
                     catch (Exception e)
                     {
                         Stop();
                         Debug.LogError(e);
                     }
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
            Session?.Dispose();
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
            catch (RpcException re)
            {
                Debug.LogError($"{nameof(TinyClient)}: RPC Excption {re}");
            }
            catch (TimeoutException te)
            {
                Debug.LogError($"{nameof(TinyClient)}: RPC Timeout {te}");
            }
            //未知异常直接关断会话
            //如果还有其他已知不应该关断会话的可以在这里插入
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TinyClient)}: RPC Error {e}");
                Stop();
            }
            return response;
        }

        private async Task PingAsync()
        {
            var count = 0;
            while (IsConnected)
            {
                try
                {
                    var begin = DateTime.Now;
                    var response = await Call<Ping>(new Ping());
                    var end = DateTime.Now;
                    var ping = (end - begin).Milliseconds;
                    // 服务器与客户端的时间差，用于在客户端上换算服务器时间
                    var delta = (response.time - ClientTime) / 10000.0f + ping / 2;
                    OnPingCaculated?.Invoke(delta, ping);
                    await Task.Delay(PingInterval);
                }
                catch (Exception)
                {
                    count++;
                    if (count > PingCount)
                    {
                        Stop();
                        Debug.LogError($"{nameof(TinyClient)}: Ping Timeout!");
                    }
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
