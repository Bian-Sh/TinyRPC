using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.DataModel;
using static zFramework.TinyRPC.MessageManager;

namespace zFramework.TinyRPC
{
    public class Session
    {
        public bool IsServerSide { get; }
        public DateTime lastPingSendTime;
        public DateTime lastPingReceiveTime;
        public float Ping => Mathf.Clamp((float)(lastPingReceiveTime - lastPingSendTime).TotalMilliseconds, 0, 999);
        public Session(TcpClient client, SynchronizationContext context, bool isServerSide)
        {
            IsServerSide = isServerSide;
            this.client = client;
            this.source = new CancellationTokenSource();
            this.context = context;
            this.lastPingReceiveTime = DateTime.Now; //避免第一次 ping 时显示 0 且异常断线
        }

        private void Send(MessageType type, byte[] content)
        {
            var stream = client.GetStream();
            var body = new byte[content.Length + 1];
            body[0] = (byte)type;
            Array.Copy(content, 0, body, 1, content.Length);
            var head = BitConverter.GetBytes(body.Length);
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
        }

        public void Send(Message message)
        {
            var wrapper = new MessageWrapper()
            {
                Message = message
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(wrapper));
            var messageType = message switch
            {
                Request => MessageType.RPC,
                Response => MessageType.RPC,
                TinyRPC.Ping => MessageType.Ping,
                _ => MessageType.Normal
            };
            Send(messageType, bytes);
        }

        public void Reply(Message message) => Send(message);

        // 写注释，特别强调2组Exception: 
        public async Task<T> Call<T>(Request request) where T : Response, new()
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
            //写入 request id, id 永远自增 1
            request.id = Interlocked.Increment(ref id);
            var json = JsonUtility.ToJson(wrapper);
            Debug.Log($"{nameof(Session)}: Call {json}");
            var bytes = Encoding.UTF8.GetBytes(json);
            Send(MessageType.RPC, bytes);

            try
            {
                var response = await AddRpcTask(request);
                if (!string.IsNullOrEmpty(response.error))// 如果服务器告知了错误！
                {
                    throw new RpcException($"Rpc Handler Error :{response.error}");
                }
                return response as T;
            }
            catch (TaskCanceledException)//report timeout exception when catch task cancel exception(which means timeout)
            {
                throw new TimeoutException($"RPC Call Timeout! Request: {request}");
            }
        }

        public NetworkStream GetStream() => client.GetStream();

        public async void ReceiveAsync()
        {
            var stream = client.GetStream();
            while (!source.IsCancellationRequested)
            {
                // 读出消息的长度
                var head = new byte[4];
                var byteReaded = await stream.ReadAsync(head, 0, head.Length, source.Token);
                if (byteReaded == 0)
                {
                    throw new Exception("断开连接！");
                }
                // 读出消息的内容
                var bodySize = BitConverter.ToInt32(head, 0);
                var body = new byte[bodySize];
                byteReaded = 0;
                // 当读取到 body size 后的数据读取需要加入超时检测
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                while (byteReaded < bodySize)
                {
                    var readed = await stream.ReadAsync(body, byteReaded, body.Length - byteReaded, cts.Token);
                    // 读着读着就断线了的情况，如果不处理，此处会产生死循环
                    if (readed == 0)
                    {
                        throw new Exception("断开连接！");
                    }
                    byteReaded += readed;
                }
                if (bodySize != byteReaded) // 消息不完整，此为异常，断开连接
                {
                    throw new Exception("消息不完整,会话断开！");
                }
                // 解析消息类型
                var type = body[0];
                var content = new byte[body.Length - 1];
                Array.Copy(body, 1, content, 0, content.Length);
                OnMessageReceived(type, content);
            }
        }

        private void OnMessageReceived(byte type, byte[] content)
        {
            var json = Encoding.UTF8.GetString(content);
            Debug.Log($"{nameof(Session)}:  收到网络消息 {(IsServerSide ? "Server" : "Client")} {json}");
            var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
            switch (type)
            {
                case 0: // ping 
                    lastPingReceiveTime = DateTime.Now;
                    if (!IsServerSide)
                    {
                        var ping = wrapper.Message as Ping;
                        lastPingSendTime = ping.svrTime;
                        Send(ping); // 直接把 ping 原封不动发回去
                    }
                    break;
                case 1: //normal message
                    {
                        context.Post(_ => HandleNormalMessage(this, wrapper.Message), null);
                    }
                    break;
                case 2: // rpc message
                    {
                        if (wrapper.Message is Request) 
                        {
                            context.Post(_ => HandleRpcRequest(this, wrapper.Message as Request), null);
                        }
                        else if (wrapper.Message is Response) 
                        {
                            context.Post(_ => HandleRpcResponse(this, wrapper.Message as Response), null);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public override string ToString() => $"Session: {client.Client.RemoteEndPoint} Ping: {Ping} IsServer:{IsServerSide}";
        public void Close() => client?.Close();
        private readonly TcpClient client;
        private readonly CancellationTokenSource source;
        private readonly SynchronizationContext context;
        private static int id = 0;
    }
}
