using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace zFramework.TinyRPC
{
    // 网络发现服务器，向局域网内的网络发现客户端广播端口信息（或者你想要任何数据）
    // Network discovery server, used to broadcast port info (or any data you want) to discovery client in LAN
    public class DiscoveryServer
    {
        private readonly string scope;
        // 在 TinyRPC 中，这个数据是 TinyRPC 服务器实例的端口
        private readonly int data;
        private readonly UdpClient udpClient;
        private bool isRunning = true;
        private int port;

        /// <summary>
        /// 构造函数。创建一个DiscoveryServer实例。
        /// </summary>
        /// <param name="port">监听广播的端口</param>
        /// <param name="scope">与 DiscoveryClient 约定的身份识别码</param>
        /// <param name="data">广播的数据 </param>
        public DiscoveryServer(int port, string scope, int data)
        {
            // 由于使用了 | 作为分隔符，所以需要对 scope 进行校验
            if (scope.Contains("|"))
            {
                throw new ArgumentException("scope can not contains |");
            }
            this.scope = scope;
            this.data = data;
            this.port = port;
            udpClient = new UdpClient(port);
        }
        public void Start()
        {
            Task.Run(async () =>
            {
                Debug.Log($"{nameof(DiscoveryServer)} is Listening {port} !");
                while (isRunning)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync();
                        if (result.Buffer.Length != 0)
                        {
                            var message = Encoding.UTF8.GetString(result.Buffer);
                            // 校验消息是否来自指定的 DiscoveryServer ， 请设定专属的 scope
                            if (message.Equals(scope))
                            {
                                var report = $"{scope}|{data}";
                                var bytes = Encoding.UTF8.GetBytes(report);
                                await udpClient.SendAsync(bytes, bytes.Length, result.RemoteEndPoint);
                                Debug.Log($"{nameof(DiscoveryServer)} Reply To {result.RemoteEndPoint}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{nameof(DiscoveryServer)}: quit,  exception = {e}");
                        break;
                    }
                }
            });
        }
        public void Stop()
        {
            isRunning = false;
            udpClient.Close();
        }
    }
}
