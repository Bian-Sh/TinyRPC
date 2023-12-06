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
        private bool isRunning = true;
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
        ///  网络发现客户端
        /// </summary>
        /// <param name="port">DiscoveryServer 监听端口，请向此端口广播</param>
        /// <param name="scope">与服务器识别的简易标识符</param>
        /// <param name="context">事件投送到主线程</param>
        public DiscoveryClient(int port, string scope)
        {
            // 由于使用了 | 作为分隔符，所以需要对 scope 进行校验
            if (scope.Contains("|"))
            {
                throw new ArgumentException("scope can not contains |");
            }
            this.port = port;
            this.scope = scope;
            this.context = SynchronizationContext.Current;
            udpClient = new UdpClient();
            // log if instance create not at main thread
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                Debug.LogWarning($"{nameof(DiscoveryClient)}:  Discovery Client is not created at main thread, this may cause some problem!");
            }
            ReceiveAsync();
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                while (isRunning)
                {
                    try
                    {
                        Debug.Log($"{nameof(DiscoveryClient)}: Discovery Client Is{(isWaiting ? "Waiting " : "Scanning")}!");
                        await WaitUntilAsync(() => !isWaiting);
                        var bytes = Encoding.UTF8.GetBytes(scope);
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
                while (isRunning)
                {
                    try
                    {
                        Debug.Log($"{nameof(DiscoveryClient)}: Discovery Client Is Wait For Server Echo !");
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
                                    var host = result.RemoteEndPoint.Address.ToString();
                                    var ip = host.Split(':')[0];
                                    isWaiting = true; //二话不说先卡住，等待外部主动解锁
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
                // 如果存在来自 DiscoveryServer 的数据报那就把它们读完
                // 避免 isWaiting =false(不等待)时，OnServerDiscovered 事件被过时的缓存数据触发
                while (udpClient.Available > 0)
                {
                    // let it throw out if there is any exception
                    await udpClient.ReceiveAsync();
                }
                await Task.Delay(500);
            }
        }

        public void Stop()
        {
            isRunning = false;
            isWaiting = false;
            udpClient.Close();
        }
    }
}
