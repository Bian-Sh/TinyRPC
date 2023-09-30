using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.DataModel;
using static zFramework.TinyRPC.MessageManager;

namespace zFramework.TinyRPC
{
    //一个简单的 TCP 服务器
    // 对外：Start 、Stop  、Send
    // 对内：AcceptAsync 、Receive
    // 事件：当握手完成，当用户断线 、当服务器断线
    // 消息结构：size + body（type+content） 4 + 1 + body , type =  0 代表 ping , 1 代表常规 message ， 2 代表 rpc message
    // 会话：Session = TcpClient + lastSendTime + lastReceiveTime 

    public class TCPServer : ICommunicate
    {
        internal TcpListener listener;
        internal readonly List<Session> sessions = new List<Session>();
        readonly SynchronizationContext context;
        public event Action<TcpClient> OnClientEstablished;
        public event Action<TcpClient> OnClientDisconnected;
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
            AcceptAsync(source.Token);
            // Send Ping Message
            Ping();
        }
        public void Stop()
        {
            timer.Dispose();
            source.Cancel();
            listener.Stop();
            foreach (var session in sessions)
            {
                session.client.Close();
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
                        session.client.Close();
                        sessions.Remove(session);
                        OnClientDisconnected?.Invoke(session.client);
                    }
                    else
                    {
                        var ping = new Ping
                        {
                            svrTime = DateTime.Now
                        };
                        var bytes = SerializeHelper.Serialize(ping);
                        session.lastPingSendTime = DateTime.Now;
                        Send(session, MessageType.Ping, bytes);
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
                    var session = new Session
                    {
                        communicate = this,
                        client = client,
                    };
                    sessions.Add(session);
                    OnClientEstablished?.Invoke(client);
                    _ = Task.Run(() => ReceiveAsync(session, token));
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        private async void ReceiveAsync(Session session, CancellationToken token)
        {
            var client = session.client;
            var stream = client.GetStream();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 读出消息的长度
                    var head = new byte[4];
                    var byteReaded = await stream.ReadAsync(head, 0, head.Length, token);
                    if (byteReaded == 0)
                    {
                        break;
                    }
                    // 读出消息的内容
                    var bodySize = BitConverter.ToInt32(head, 0);
                    var body = new byte[bodySize];

                    while (byteReaded < bodySize)
                    {
                        var readed = await stream.ReadAsync(body, 0, body.Length, token);
                        // 读着读着就断线了的情况，如果不处理，此处会产生死循环
                        if (readed == 0)
                        {
                            break;
                        }
                        byteReaded += readed;
                    }
                    if (bodySize != byteReaded) // 消息不完整，此为异常，断开连接
                    {
                        break;
                    }
                    // 解析消息类型
                    var type = body[0];
                    var content = new byte[body.Length - 1];
                    OnMessageReceived(session, type, content);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    break;
                }
            }
            context.Post(_ => OnClientDisconnected?.Invoke(client), null);
            sessions.Remove(session);
            client.Close();
        }

        public void Send(Session session, MessageType type, byte[] content)
        {
            var stream = session.client.GetStream();
            var body = new byte[content.Length + 1];
            body[0] = (byte)type;
            Array.Copy(content, 0, body, 1, content.Length);
            var head = BitConverter.GetBytes(body.Length);
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
        }

        public void Boardcast(MessageType type, byte[] content)
        {
            foreach (var session in sessions)
            {
                Send(session, type, content);
            }
        }

        private void OnMessageReceived(Session session, byte type, byte[] content)
        {
            switch (type)
            {
                case 0:
                    // ping 
                    // 服务器收到客户端的 ping 消息，更新 lastPingReceiveTime
                    session.lastPingReceiveTime = DateTime.Now;
                    break;
                case 1:
                    {
                        //normal message
                        var json = Encoding.UTF8.GetString(content);
                        var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
                        context.Post(_ => HandleNormalMessage(session, wrapper.Message), null);
                    }
                    break;
                case 2:
                    {
                        // rpc message
                        var json = Encoding.UTF8.GetString(content);
                        var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
                        // rpc 消息有2个分支：response 、request
                        if (wrapper.Message is Request) //如果是请求就直接发消息回去即可，否则就是响应
                        {
                            context.Post(_ => HandleRpcMessage(session, wrapper.Message as Request), null);
                        }
                        else if (wrapper.Message is Response) //如果是响应就找到对应的 task 并设置 task.SetResult
                        {
                            context.Post(_ => HandleRpcResponse(session, wrapper.Message as Response), null);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public void Send(Session session, Message message)
        {
            var wrapper = new MessageWrapper()
            {
                Message = message
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(wrapper));
            var messageType = message is Response ? MessageType.RPC : MessageType.Normal;
            Send(session, messageType, bytes);
        }

        public async Task<T> Call<T>(Session session, Request request) where T : Response, new()
        {
            // 校验 RPC 消息匹配
            var type = GetResponseType(request);
            if (type != typeof(T))
            {
                throw new Exception($"RPC Response 消息类型不匹配, 期望值： {type},传入值 {typeof(T)}");
            }
            var wrapper = new MessageWrapper()
            {
                Message = request
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(wrapper));
            Send(session, MessageType.RPC, bytes);
            Response response;
            try
            {
                response = await AddRpcTask(request, request.timeout);
            }
            catch (Exception e)
            {
                response = new T()
                {
                    error = e.Message
                };
            }
            return response as T;
        }
    }
}
