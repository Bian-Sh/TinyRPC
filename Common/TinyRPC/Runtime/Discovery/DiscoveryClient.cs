using System;
using System.Collections.Generic;
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
        private readonly UdpClient udpClient;
        private readonly float timeout;
        private CancellationTokenSource timeout_cts;
        private CancellationTokenSource cts;
        /// <summary>
        ///  是否处于等待状态
        /// </summary>
        private bool isWaiting = false;
        private readonly SynchronizationContext context;
        /// <summary>
        ///  已发现的服务器集合，用于去重
        /// </summary>
        private readonly HashSet<string> discoveredServers = new HashSet<string>();
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
            // 启用广播
            udpClient.EnableBroadcast = true;
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
                        
                        // 修复多网卡问题：向所有网络接口的广播地址发送广播包
                        await SendBroadcastToAllNetworkInterfaces(bytes);
                        
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

        public void Standby()
        {
            isWaiting = true;
        }

        /// <summary>
        /// 重新扫描服务器，清除缓存并重新开始发现流程
        /// </summary>
        public void Rescan()
        {
            Debug.Log($"{nameof(DiscoveryClient)}: Starting rescan, clearing cache and restarting discovery");
            
            // 清除已发现服务器的缓存
            lock (discoveredServers)
            {
                discoveredServers.Clear();
            }
            
            // 取消当前的超时计时器
            timeout_cts?.Dispose();
            timeout_cts = null;
            
            // 重置等待状态，允许重新发现
            isWaiting = false;
            
            Debug.Log($"{nameof(DiscoveryClient)}: Rescan initiated, cache cleared, ready for new discovery");
        }

        /// <summary>
        /// 向所有可用网络接口的广播地址发送广播包
        /// </summary>
        /// <param name="bytes">要发送的数据</param>
        private async Task SendBroadcastToAllNetworkInterfaces(byte[] bytes)
        {
            var sentBroadcastAddresses = new HashSet<IPAddress>();
            
            try
            {
                // 获取所有网络接口的广播地址，并按优先级排序
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                ni.SupportsMulticast)
                    .ToList();

                var broadcastInfos = new List<NetworkBroadcastInfo>();

                foreach (var networkInterface in networkInterfaces)
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    var unicastAddresses = ipProperties.UnicastAddresses
                        .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        .ToList();

                    foreach (var unicastAddress in unicastAddresses)
                    {
                        try
                        {
                            // 计算网络的广播地址
                            var ip = unicastAddress.Address;
                            var subnet = unicastAddress.IPv4Mask;
                            
                            if (subnet != null)
                            {
                                var broadcastAddress = GetBroadcastAddress(ip, subnet);
                                if (!IPAddress.IsLoopback(broadcastAddress) && 
                                    !broadcastAddress.Equals(IPAddress.Any))
                                {
                                    // 检查该网络接口是否有默认网关（优先级更高）
                                    var hasGateway = ipProperties.GatewayAddresses.Any(g => 
                                        g.Address.AddressFamily == AddressFamily.InterNetwork && 
                                        !IPAddress.IsLoopback(g.Address));
                                    
                                    var info = new NetworkBroadcastInfo
                                    {
                                        BroadcastAddress = broadcastAddress,
                                        NetworkInterfaceName = networkInterface.Name,
                                        HasGateway = hasGateway,
                                        LocalIP = ip
                                    };

                                    // 避免重复的广播地址
                                    if (!broadcastInfos.Any(bi => bi.BroadcastAddress.Equals(broadcastAddress)))
                                    {
                                        broadcastInfos.Add(info);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"{nameof(DiscoveryClient)}: Failed to calculate broadcast address for {networkInterface.Name}: {ex.Message}");
                        }
                    }
                }

                // 按优先级排序：有网关的接口优先
                broadcastInfos = broadcastInfos
                    .OrderByDescending(bi => bi.HasGateway)
                    .ThenBy(bi => bi.NetworkInterfaceName)
                    .ToList();

                if (broadcastInfos.Count > 0)
                {
                    // 先尝试高优先级的网络接口
                    var highPriorityInfos = broadcastInfos.Where(bi => bi.HasGateway).ToList();
                    
                    if (highPriorityInfos.Count > 0)
                    {
                        // 先向有网关的接口广播
                        foreach (var info in highPriorityInfos)
                        {
                            await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(info.BroadcastAddress, port));
                            sentBroadcastAddresses.Add(info.BroadcastAddress);
                            Debug.Log($"{nameof(DiscoveryClient)}: Sent priority broadcast to {info.BroadcastAddress} via {info.NetworkInterfaceName} (Gateway: {info.HasGateway})");
                        }
                        
                        // 等待一段时间，看是否有响应
                        await Task.Delay(800); // 等待800ms
                        
                        // 如果还没有发现服务器，再向其他接口广播
                        if (!isWaiting) // isWaiting = true 表示已经发现了服务器
                        {
                            var lowPriorityInfos = broadcastInfos.Where(bi => !bi.HasGateway).ToList();
                            foreach (var info in lowPriorityInfos)
                            {
                                if (!sentBroadcastAddresses.Contains(info.BroadcastAddress))
                                {
                                    await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(info.BroadcastAddress, port));
                                    sentBroadcastAddresses.Add(info.BroadcastAddress);
                                    Debug.Log($"{nameof(DiscoveryClient)}: Sent fallback broadcast to {info.BroadcastAddress} via {info.NetworkInterfaceName} (Gateway: {info.HasGateway})");
                                }
                            }
                        }
                    }
                    else
                    {
                        // 如果没有带网关的接口，直接向所有接口广播
                        foreach (var info in broadcastInfos)
                        {
                            await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(info.BroadcastAddress, port));
                            sentBroadcastAddresses.Add(info.BroadcastAddress);
                            Debug.Log($"{nameof(DiscoveryClient)}: Sent broadcast to {info.BroadcastAddress} via {info.NetworkInterfaceName} (Gateway: {info.HasGateway})");
                        }
                    }
                }
                else
                {
                    // 如果没有找到特定的广播地址，则使用全局广播作为后备方案
                    await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
                    sentBroadcastAddresses.Add(IPAddress.Broadcast);
                    Debug.Log($"{nameof(DiscoveryClient)}: Sent global broadcast to {IPAddress.Broadcast}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(DiscoveryClient)}: Failed to send broadcast: {e.Message}");
                
                // 如果上面的方法失败，尝试全局广播作为最后的手段
                if (sentBroadcastAddresses.Count == 0)
                {
                    try
                    {
                        await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
                        Debug.Log($"{nameof(DiscoveryClient)}: Fallback to global broadcast");
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.LogError($"{nameof(DiscoveryClient)}: Even fallback broadcast failed: {fallbackEx.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 根据IP地址和子网掩码计算广播地址
        /// </summary>
        /// <param name="address">IP地址</param>
        /// <param name="mask">子网掩码</param>
        /// <returns>广播地址</returns>
        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
        {
            var addressBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var broadcastBytes = new byte[addressBytes.Length];

            for (int i = 0; i < addressBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(addressBytes[i] | (~maskBytes[i] & 0xFF));
            }

            return new IPAddress(broadcastBytes);
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
                                
                                // 兼容旧版本（只有 scope|port）和新版本（scope|port|deviceId）
                                if (arr.Length >= 2 && int.TryParse(arr[1], out int serverPort))
                                {
                                    var host = result.RemoteEndPoint.Address.ToString();
                                    var ip = host.Split(':')[0];
                                    
                                    // 创建服务器唯一标识符
                                    string serverKey;
                                    if (arr.Length >= 3 && !string.IsNullOrEmpty(arr[2]))
                                    {
                                        // 新版本：使用设备唯一标识符
                                        var deviceId = arr[2];
                                        serverKey = deviceId;
                                        Debug.Log($"{nameof(DiscoveryClient)}: Received server info with Device ID: {deviceId}");
                                    }
                                    else
                                    {
                                        // 兼容旧版本：使用端口作为标识符
                                        serverKey = $"port_{serverPort}";
                                        Debug.Log($"{nameof(DiscoveryClient)}: Received server info without Device ID, using port as identifier");
                                    }
                                    
                                    // 检查是否已经发现过这个服务器
                                    lock (discoveredServers)
                                    {
                                        if (discoveredServers.Contains(serverKey))
                                        {
                                            Debug.Log($"{nameof(DiscoveryClient)}: Server {ip}:{serverPort} (ID: {serverKey}) already discovered, ignoring duplicate");
                                            continue;
                                        }
                                        
                                        // 记录已发现的服务器
                                        discoveredServers.Add(serverKey);
                                    }

                                    timeout_cts?.Dispose();
                                    timeout_cts = null;

                                    isWaiting = true; //二话不说先卡住，保证事件只执行一次，是否解锁由调用方决定！
                                    Debug.Log($"{nameof(DiscoveryClient)}:  Server Discovered， ip = {ip}, port = {serverPort}, server ID = {serverKey}");
                                    context.Post(_ => OnServerDiscovered?.Invoke(ip, serverPort), null);
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
            lock (discoveredServers)
            {
                discoveredServers.Clear();
            }
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            timeout_cts?.Dispose();
            timeout_cts = null;
            udpClient.Close();
        }

        /// <summary>
        /// 网络广播信息
        /// </summary>
        private class NetworkBroadcastInfo
        {
            public IPAddress BroadcastAddress { get; set; }
            public string NetworkInterfaceName { get; set; }
            public bool HasGateway { get; set; }
            public IPAddress LocalIP { get; set; }
        }
    }
}
