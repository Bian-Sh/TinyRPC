using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
using zFramework.TinyRPC.Exceptions;
using zFramework.TinyRPC.Settings;
using static zFramework.TinyRPC.Manager;

namespace zFramework.TinyRPC
{
    public class Session
    {
        public bool IsServerSide { get; }
        public bool IsAlive { get; private set; }
        public IPEndPoint IPEndPoint { get; private set; }

        internal Session(TcpClient client, SynchronizationContext context, bool isServerSide)
        {
            IsServerSide = isServerSide;
            this.client = client;

            // 深度拷贝 IPEndPoint 用于在任意时候描述 Session （只要这个 Session 还能被访问）
            var iPEndPoint = (IsServerSide ? client.Client.RemoteEndPoint : client.Client.LocalEndPoint) as IPEndPoint;
            var address = IPAddress.Parse(iPEndPoint.Address.ToString());
            var port = iPEndPoint.Port;
            IPEndPoint = new IPEndPoint(address, port);

            this.source = new CancellationTokenSource();
            this.context = context;
            IsAlive = true;
        }

        private void Send(MessageType type, byte[] content)
        {
            if (IsAlive)
            {
                var stream = client.GetStream();
                var body = new byte[content.Length + 1];
                body[0] = (byte)type;
                Array.Copy(content, 0, body, 1, content.Length);
                var head = BitConverter.GetBytes(body.Length);
                stream.Write(head, 0, head.Length);
                stream.Write(body, 0, body.Length);
            }
            else
            {
                Debug.LogWarning($"{nameof(Session)}: 消息发送失败，会话已失效！");
            }
        }

        internal void Send(IMessage message)
        {
            var bytes = SerializeHelper.Serialize(message);
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

        internal void Reply(IMessage message) => Send(message);

        // 写注释，特别强调2组Exception: 
        internal async Task<T> Call<T>(IRequest request) where T : class, IResponse, new()
        {
            // 校验 RPC 消息匹配
            var type = GetResponseType(request);
            if (type != typeof(T))
            {
                throw new Exception($"RPC Response 消息类型不匹配, 期望值： {type},传入值 {typeof(T)}");
            }
            // 原子操作，保证 id 永远自增 1且不会溢出,溢出就从0开始
            Interlocked.CompareExchange(ref id, 0, int.MaxValue);
            request.Rid = Interlocked.Increment(ref id);

            var bytes = SerializeHelper.Serialize(request);
            Send(MessageType.RPC, bytes);  // do not catch any exception here,jut let it throw out

            var response = await AddRpcTask(request);
            if (!string.IsNullOrEmpty(response.Error))// 如果服务器告知了错误！
            {
                throw new RpcResponseException($"Rpc Handler Error :{response.Error}");
            }
            return response as T;
        }

        internal async Task ReceiveAsync()
        {
            var stream = client.GetStream();
            while (!source.IsCancellationRequested)
            {
                // 读出消息的长度
                var head = new byte[4];
                var byteReaded = await stream.ReadAsync(head, 0, head.Length, source.Token);
                if (byteReaded == 0)
                {
                    throw new Exception("在读取消息头时网络连接意外断开！");
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
                        throw new Exception("在读取消息体时网络连接意外断开！");
                    }
                    byteReaded += readed;
                }
                if (bodySize != byteReaded) // 消息不完整，此为异常，断开连接
                {
                    throw new Exception("在读取网络消息时得到了不完整数据,会话断开！");
                }
                // 解析消息类型
                var type = body[0];
                var content = new byte[body.Length - 1];
                Array.Copy(body, 1, content, 0, content.Length);
                context.Post(_ => OnMessageReceived(type, content), null);
            }
        }

        private void OnMessageReceived(byte type, byte[] content)
        {
            var message = SerializeHelper.Deserialize(content);
            if (!TinyRpcSettings.Instance.logFilters.Contains(message.GetType().FullName)) //Settings can only be created in main thread
            {
                Debug.Log($"{nameof(Session)}:   {(IsServerSide ? "Server" : "Client")} 收到网络消息 =  {JsonUtility.ToJson(message)}");
            }
            switch (type)
            {
                case 0: //normal message
                    HandleMessage(this, message);
                    break;
                case 1: // rpc message
                    if (message is Request || (message is Ping && IsServerSide))
                    {
                        HandleRequest(this, message as IRequest);
                    }
                    else if (message is Response || (message is Ping && !IsServerSide))
                    {
                        HandleResponse(message as IResponse);
                    }
                    break;
                default:
                    break;
            }
        }
        public override string ToString() => $"Session: {IPEndPoint}  IsServer:{IsServerSide}";
        public void Close()
        {
            client?.Dispose();
            source?.Dispose();
            IsAlive = false;
        }
        private int id = 0;
        private readonly TcpClient client;
        private readonly CancellationTokenSource source;
        private readonly SynchronizationContext context;
    }
}
