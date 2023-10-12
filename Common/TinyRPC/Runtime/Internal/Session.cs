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
    public class Session : IDisposable
    {
        public bool IsServerSide { get; }
        public bool IsAlive { get; private set; } 

        // 服务端用于断言Session是否消亡
        public DateTime lastPingSendTime;
        public DateTime lastPingReceiveTime;
        public Session(TcpClient client, SynchronizationContext context, bool isServerSide)
        {
            IsServerSide = isServerSide;
            this.client = client;
            this.source = new CancellationTokenSource();
            this.context = context;
            IsAlive = true;
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

        public void Send(IMessage message)
        {
            var wrapper = new MessageWrapper()
            {
                Message = message
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(wrapper));
            var messageType = message switch
            {
                IRequest => MessageType.RPC,
                IResponse => MessageType.RPC,
                // 其他的均为常规消息
                // otherwise you get a normal message
                _ => MessageType.Normal
            };
            Send(messageType, bytes);
        }

        public void Reply(IMessage message) => Send(message);

        // 写注释，特别强调2组Exception: 
        public async Task<T> Call<T>(IRequest request) where T : class, IResponse, new()
        {
            // 校验 RPC 消息匹配
            var type = GetResponseType(request);
            if (type != typeof(T))
            {
                throw new Exception($"RPC Response 消息类型不匹配, 期望值： {type},传入值 {typeof(T)}");
            }
            // 原子操作，保证 id 永远自增 1且不会溢出
            if (Interlocked.CompareExchange(ref id, int.MinValue, int.MaxValue) == int.MaxValue)
            {
                Interlocked.Increment(ref id);
            }
            //写入 request id, id 永远自增 1
            request.Id = id;

            var bytes = SerializeHelper.Serialize(request);
            Send(MessageType.RPC, bytes);  // try what? dispose?

            try
            {
                var response = await AddRpcTask(request);
                if (!string.IsNullOrEmpty(response.Error))// 如果服务器告知了错误！
                {
                    throw new RpcException($"Rpc Handler Error :{response.Error}");
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
            lastPingReceiveTime = DateTime.Now;
            var json = Encoding.UTF8.GetString(content);
            Debug.Log($"{nameof(Session)}:  收到网络消息 {(IsServerSide ? "Server" : "Client")} {json}");
            var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
            switch (type)
            {
                case 0: //normal message
                    {
                        context.Post(_ => HandleNormalMessage(this, wrapper.Message), null);
                    }
                    break;
                case 1: // rpc message
                    {
                        if (wrapper.Message is Request || (wrapper.Message is Ping && IsServerSide))
                        {
                            context.Post(_ => HandleRpcRequest(this, wrapper.Message as IRequest), null);
                        }
                        else if (wrapper.Message is Response || (wrapper.Message is Ping && !IsServerSide))
                        {
                            context.Post(_ => HandleRpcResponse(this, wrapper.Message as IResponse), null);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public override string ToString() => $"Session: {client.Client.RemoteEndPoint}  IsServer:{IsServerSide}";
        public void Close() => client?.Close();

        public void Dispose()
        {
            client?.Close();
            client?.Dispose();
            source?.Dispose();
            IsAlive = false;
        }

        private readonly TcpClient client;
        private readonly CancellationTokenSource source;
        private readonly SynchronizationContext context;
        private static int id = 0;
    }
}
