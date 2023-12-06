using System;
using System.Net;
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
        private readonly UdpClient udpClient;
        private readonly int port;
        private bool isRunning = true;
        // 在 TinyRPC 中，这个数据是 TinyRPC 服务器实例的端口
        private int data; 


        /// <summary>
        /// 构造函数。创建一个DiscoveryServer实例。
        /// </summary>
        /// <param name="port">定向广播的端口，请与 DiscoveryClient 监听端口保持一致</param>
        /// <param name="scope">与 DiscoveryClient 约定的身份识别码</param>
        /// <param name="data">广播的数据 </param>
        public DiscoveryServer(int port, string scope, int data)
        {
            // 由于使用了 | 作为分隔符，所以需要对 scope 进行校验
            if (scope.Contains("|"))
            {
                throw new ArgumentException("scope can not contains |");
            }
            this.port = port;
            this.scope = scope;
            this.data = data;
            udpClient = new UdpClient();
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                Debug.Log($"{nameof(DiscoveryServer)}:  DiscoveryServer started");
                while (isRunning)
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes($"{scope}|{data}");
                        var ipendPoint = new IPEndPoint(IPAddress.Broadcast, port);
                        await udpClient.SendAsync(bytes, bytes.Length, ipendPoint);
                        await Task.Delay(2000);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{nameof(DiscoveryServer)}:  exception = {e}");
                        break;
                    }
                }
                Debug.Log($"{nameof(DiscoveryServer)}:  DiscoveryServer stopped");
            });
        }
        public void Stop()
        {
            isRunning = false;
            udpClient.Close();
        }
    }
}
