using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        private readonly float timeout;
        private CancellationTokenSource cts;
        private readonly SynchronizationContext context;
        public bool isWaiting = false;
        public Action<string, int> OnServerDiscovered;
        public Action OnDiscoveryTimeout;
        UdpClient activeClient;

        /// <summary>
        /// 网络发现客户端
        /// </summary>
        /// <param name="port">DiscoveryServer 监听端口，请向此端口广播</param>
        /// <param name="scope">与服务器识别的简易标识符</param>
        /// <param name="timeout">超时时间，单位为秒</param>
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
            this.timeout = timeout;

            // log if instance create not at main thread
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                Debug.LogWarning($"{nameof(DiscoveryClient)} is not created at main thread, this may cause some problem!");
            }
        }

        /// <summary>
        /// 获取本机的合理 IP 地址
        /// </summary>
        /// <returns>本机的 IP 地址</returns>
        private IPAddress GetLocalIPAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && !nic.Description.Contains("Virtual") && nic.GetIPProperties().GatewayAddresses.Any())
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(ip => ip.Address)
                .FirstOrDefault();

            if (networkInterfaces == null)
            {
                throw new InvalidOperationException("No suitable network adapters found.");
            }

            return networkInterfaces;
        }

        /// <summary>
        /// 开始网络发现
        /// </summary>
        public void Start()
        {
            Task.Run(async () =>
            {
                var localIPAddress = GetLocalIPAddress();
                var bytes = Encoding.UTF8.GetBytes(scope);
                while (true)
                {
                    using var udpClient = new UdpClient(new IPEndPoint(localIPAddress, 0));
                    activeClient = udpClient;
                    udpClient.MulticastLoopback = true;
                    //udpClient.AllowNatTraversal(true);
                    try
                    {
                        Debug.Log($"{nameof(DiscoveryClient)} Is{(isWaiting ? "Waiting " : $"Scanning {port} ")}!");
                        await WaitUntilAsync(() => isWaiting == false);
                        cts.Token.ThrowIfCancellationRequested();

                        // 设置超时机制
                        using var timeoutCts = new CancellationTokenSource();
                        timeoutCts.CancelAfter((int)(timeout * 1000));
                        await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
                        cts.Token.ThrowIfCancellationRequested();

                        // 接收响应
                        var result = await udpClient.ReceiveAsync(timeoutCts.Token);
                        cts.Token.ThrowIfCancellationRequested();

                        if (result.Buffer.Length != 0)
                        {
                            var message = Encoding.UTF8.GetString(result.Buffer);
                            // 校验消息是否来自指定的 DiscoveryServer ，请设定专属的 scope
                            if (message.StartsWith(scope))
                            {
                                var arr = message.Split('|');
                                if (arr.Length == 2 && int.TryParse(arr[1], out int port))
                                {
                                    var host = result.RemoteEndPoint.Address.ToString();
                                    var ip = host.Split(':')[0];
                                    isWaiting = true; // 二话不说先卡住，保证事件只执行一次，是否解锁由调用方决定！
                                    Debug.Log($"{nameof(DiscoveryClient)}:  Server Discovered， ip = {ip}, port = {port}");
                                    context.Post(_ => OnServerDiscovered?.Invoke(ip, port), null);
                                }
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        if (!isWaiting)
                        {
                            Debug.Log($"{nameof(DiscoveryClient)}: Discovery Timeout !");
                            context.Post(_ => OnDiscoveryTimeout?.Invoke(), null);
                        }
                    }
                    catch (Exception e) when
                    (e is OperationCanceledException ||
                    e is NullReferenceException ||
                    (e is ObjectDisposedException ex && ex.ObjectName == typeof(UdpClient).FullName))
                    {
                        Debug.Log($"{nameof(DiscoveryClient)}: Discovery Canceled !");
                        break;
                    }
                    catch (Exception other)
                    {
                        Debug.LogWarning($"{nameof(DiscoveryClient)}: quit,  exception = {other}");
                        // 如果是 Dispose Exception  break
                    }
                }
            });
        }

        /// <summary>
        /// 等待直到条件满足
        /// </summary>
        /// <param name="predicate">条件函数</param>
        /// <returns></returns>
        public async Task WaitUntilAsync(Func<bool> predicate)
        {
            while (!predicate())
            {
                await Task.Delay(500);
            }
        }

        /// <summary>
        /// 停止网络发现
        /// </summary>
        public void Stop()
        {
            isWaiting = false;
            activeClient?.Close();
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }
    }
    static class UDPClientExtension
    {
        public static async Task<UdpReceiveResult> ReceiveAsync(this UdpClient udpClient, CancellationToken token)
        {
            var receiveTask = udpClient.ReceiveAsync();
            var delayTask = Task.Delay(-1, token);
            var task = await Task.WhenAny(receiveTask, delayTask);
            if (task == delayTask)
            {
                throw new TimeoutException("Udp Receive timeout");
            }
            return await receiveTask;
        }
    }

}
