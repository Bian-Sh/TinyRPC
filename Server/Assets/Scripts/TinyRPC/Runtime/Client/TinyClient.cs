using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using zFramework.TinyRPC.DataModel;
using System.Threading.Tasks;

namespace zFramework.TinyRPC
{
    public interface ICommunicate
    {
        void Send(Session session, Message message);
        Task<T> Call<T>(Session session, Request request) where T : Response, new();
    }
    // TODO: Client
    // 链接 TCPServer
    // API 无限对标 TCPServer
    public class TinyClient : ICommunicate
    {
        public string ip = "172.0.0.1";
        public int port = 12345;

        public void Start()
        {
            client = new TcpClient(ip, port);
            source = new CancellationTokenSource();
            ReceiveAsync(source.Token);
        }

        private async void ReceiveAsync(CancellationToken token)
        {
            await client.ConnectAsync(ip, port);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[1024];
                    var size = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, token);
                    if (size == 0)
                    {
                        Debug.Log("服务器断开连接");
                        Stop();
                        return;
                    }
                    //todo parse message package
                  
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    Stop();
                    return;
                }
            }
        }

        public void Stop()
        {
            source?.Cancel();
            client?.Close();
            source = null;
            client = null;
        }

        public void Send(Session session, Message message)
        {
            throw new NotImplementedException();
        }

        Task<T> ICommunicate.Call<T>(Session session, Request request)
        {
            throw new NotImplementedException();
        }

        TcpClient client;
        CancellationTokenSource source;
    }
}
