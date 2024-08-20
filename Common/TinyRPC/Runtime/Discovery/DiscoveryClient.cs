using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace zFramework.TinyRPC
{
    // 网络发现客户端，发现局域网内的服务器
    // Network discovery client, used to discover server in LAN
    // 被动接受 DiscoveryServer 推送的 TinyRPC 服务器实例的端口数据
    // Receive port data of TinyRPC server instance from DiscoveryServer
    public class DiscoveryClient
    {
        private readonly int port; // remote binding port
        private readonly string scope;
        private readonly UdpClient udpClient;
        private readonly float timeout;
        private CancellationTokenSource timeout_cts;
        private CancellationTokenSource cts;
        /// <summary>
        ///  是否处于等待状态
        /// </summary>
        public bool isWaiting = false;
        private readonly SynchronizationContext context;
        /// <summary>
        ///  当 TinyRPC 服务器实例被发现时触发
        ///   第一个参数是 IP, 第二个参数是 port
        /// </summary>
        public Action<string, int> OnServerDiscovered;
        /// <summary>
        ///   当网络发现超时时触发，超时时间由 timeout 决定
        ///   每个 loop 超时都会触发一次
        /// </summary>
        public Action OnDiscoveryTimeout;

        /// <summary>
        ///  网络发现客户端
        /// </summary>
        /// <param name="port">DiscoveryServer 监听端口，请向此端口广播</param>
        /// <param name="scope">与服务器识别的简易标识符</param>
        public DiscoveryClient(int port, string scope, float timeout = 10f)
        {
            // 由于使用了 | 作为分隔符，所以需要对 scope 进行校验
            if (scope.Contains("|"))
            {
                throw new ArgumentException("scope can not contains |");
            }
            this.port = port;
            this.scope = scope;
            this.context = SynchronizationContext.Current;
            this.cts = new CancellationTokenSource();
            udpClient = new UdpClient();
            // log if instance create not at main thread
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                Debug.LogWarning($"{nameof(DiscoveryClient)} is not created at main thread, this may cause some problem!");
            }
            ReceiveAsync();
            this.timeout = timeout;
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                var bytes = Encoding.UTF8.GetBytes(scope);
                while (true)
                {
                    try
                    {
                        Debug.Log($"{nameof(DiscoveryClient)} Is{(isWaiting ? "Waiting " : $"Scanning {port} ")}!");
                        await WaitUntilAsync(() => isWaiting == false);
                        cts.Token.ThrowIfCancellationRequested();
                        // 实现一个超时机制，如果超时则触发 OnDiscoveryTimeout 事件
                        if (timeout_cts == null)
                        {
                            timeout_cts = new CancellationTokenSource();
                            timeout_cts.CancelAfter((int)(timeout * 1000));
                            timeout_cts.Token.Register(() =>
                            {
                                if (!isWaiting)
                                {
                                    Debug.Log($"{nameof(DiscoveryClient)}: Discovery Timeout !");
                                    context.Post(_ => OnDiscoveryTimeout?.Invoke(), null);
                                }
                                timeout_cts?.Dispose();
                                timeout_cts = null;
                            });
                        }
                        cts.Token.ThrowIfCancellationRequested();
                        await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
                        await Task.Delay(1500); // 1.5s 扫描间隔
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{nameof(DiscoveryClient)}: quit,  exception = {e}");
                        break;
                    }
                }
            });
        }

        private void ReceiveAsync()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        Debug.Log($"{nameof(DiscoveryClient)} Is Wait For Server Echo !");
                        cts.Token.ThrowIfCancellationRequested();
                        var result = await udpClient.ReceiveAsync();
                        if (result.Buffer.Length != 0)
                        {
                            var message = Encoding.UTF8.GetString(result.Buffer);
                            // 校验消息是否来自指定的 DiscoveryServer ， 请设定专属的 scope
                            if (message.StartsWith(scope))
                            {
                                var arr = message.Split('|');
                                if (arr.Length == 2 && int.TryParse(arr[1], out int port))
                                {
                                    timeout_cts?.Dispose();
                                    timeout_cts = null;

                                    var host = result.RemoteEndPoint.Address.ToString();
                                    var ip = host.Split(':')[0];
                                    isWaiting = true; //二话不说先卡住，保证事件只执行一次，是否解锁由调用方决定！
                                    Debug.Log($"{nameof(DiscoveryClient)}:  Server Discovered， ip = {ip}, port = {port}");
                                    context.Post(_ => OnServerDiscovered?.Invoke(ip, port), null);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{nameof(DiscoveryClient)}: quit,  exception = {e}");
                        break;
                    }
                }
            });
        }

        public async Task WaitUntilAsync(Func<bool> predicate)
        {
            while (!predicate())
            {
                while (udpClient.Available > 0)
                {
                    // 如果存在来自 DiscoveryServer 的数据报那就把它们读完
                    // 避免 isWaiting =false(不等待)时，OnServerDiscovered 事件被过时的缓存数据触发
                    // let it throw out if there is any exception
                    cts.Token.ThrowIfCancellationRequested();
                    await udpClient.ReceiveAsync();
                }
                await Task.Delay(500);
            }
        }

        public void Stop()
        {
            isWaiting = false;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            timeout_cts?.Dispose();
            timeout_cts = null;
            udpClient.Close();
        }
    }
}
