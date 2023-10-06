using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    //一个简单的 TCP 服务器
    // 对外：Start 、Stop  、Send
    // 对内：AcceptAsync 、Receive
    // 事件：当握手完成，当用户断线 、当服务器断线
    // 消息结构：size + body（type+content） 4 + 1 + body , type =  0 代表 ping , 1 代表常规 message ， 2 代表 rpc message
    // 会话：Session = TcpClient + lastSendTime + lastReceiveTime 

    public class TCPServer
    {
        internal TcpListener listener;
        internal readonly List<Session> sessions = new List<Session>();
        readonly SynchronizationContext context;
        public event Action<Session> OnClientEstablished;
        public event Action<Session> OnClientDisconnected;
        public event Action<string> OnServerClosed;

        CancellationTokenSource source;

        #region Field Ping
        internal float pingInterval = 2f;
        // 如果pingTimeout秒内没有收到客户端的消息，则断开连接
        // 在 interval = 2 时代表 retry 了 5 次
        internal float pingTimeout = 10f;
        Timer timer;
        #endregion
        public TCPServer(int port)
        {
            context = SynchronizationContext.Current;
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            listener.Start();
            source = new CancellationTokenSource();
            Task.Run(() => AcceptAsync(source.Token));
            // Send Ping Message
           // Task.Run(Ping);
        }
        public void Stop()
        {
            timer.Dispose();
            source.Cancel();
            listener.Stop();
            foreach (var session in sessions)
            {
                session.Close();
            }
            sessions.Clear();
            OnServerClosed?.Invoke("服务器已关闭");
        }
        private void Ping()
        {
            timer = new(_ =>
            {
                foreach (var session in sessions)
                {
                    if (DateTime.Now - session.lastPingReceiveTime > TimeSpan.FromSeconds(pingTimeout))
                    {
                        session.Close();
                        sessions.Remove(session);
                        OnClientDisconnected?.Invoke(session);
                    }
                    else
                    {
                        var ping = new Ping
                        {
                            svrTime = DateTime.Now
                        };
                        var bytes = SerializeHelper.Serialize(ping);
                        session.lastPingSendTime = DateTime.Now;
                        session.Send(MessageType.Ping, bytes);
                    }
                }
            }, null, TimeSpan.FromSeconds(pingInterval), TimeSpan.FromSeconds(pingInterval));
        }

        private async void AcceptAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    var session = new Session(client, context, true);
                    sessions.Add(session);
                    OnClientEstablished?.Invoke(session);
                    try
                    {
                        _ = Task.Run(session.ReceiveAsync);
                    }
                    catch (Exception e)
                    {
                        session.Close();
                        sessions.Remove(session);
                        OnClientDisconnected?.Invoke(session);
                        Debug.Log($"{nameof(TCPServer)}:  Session is disconnected! \n{session}\n{e}");
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }


        public void Boardcast(MessageType type, byte[] content)
        {
            foreach (var session in sessions)
            {
                session.Send(type, content);
            }
        }
        public void Boardcast(Message message)
        {
            foreach (var session in sessions)
            {
                session.Send(message);
            }
        }
    }
}
