using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC.Messages;
using zFramework.TinyRPC.Exceptions;

namespace zFramework.TinyRPC
{
    //一个简单的 TCP 服务器
    // 对外：Start 、Stop  、Send
    // 对内：AcceptAsync 、Receive
    // 事件：当握手完成，当用户断线 、当服务器断线
    // 消息结构：size + body , type = 0 代表常规 message ， 1 代表 rpc message
    public class TinyServer
    {
        public event Action<Session> OnClientEstablished;
        public event Action<Session> OnClientDisconnected;
        public event Action<string> OnServerClosed;
        public TinyServer(int port)
        {
            context = SynchronizationContext.Current;
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            listener.Start();
            source = new CancellationTokenSource();
            Task.Run(() => AcceptAsync(source));
        }
        public void Stop()
        {
            //停服前先断开 Session
            foreach (var session in sessions)
            {
                session?.Close();
            }
            source?.Cancel();
            listener?.Stop();
            sessions.Clear();
            context.Post(v => OnServerClosed?.Invoke("服务器已关闭"), null);
        }


        private async void AcceptAsync(CancellationTokenSource token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    var session = new Session(client, context, true);
                    sessions.Add(session);
                    context.Post(v => OnClientEstablished?.Invoke(session), null);
                    _ = Task.Run(() => ReceiveAsync(session));
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }

        // 需要对 self ReceiveAsync 进行异常处理
        // 故而先前的调用（_=Task.Run(self.ReceiveAsync)被修正为当前样式
        // 同理可得，Send、Call 皆是如此
        private async void ReceiveAsync(Session session)
        {
            while (session.IsAlive)
            {
                try
                {
                    await session.ReceiveAsync();
                }
                catch (Exception e) when (e is not RpcTimeoutException && e is not RpcResponseException)
                {
                    Debug.LogError($"{nameof(TinyServer)}: Receive Error {e}");
                    HandleDisactiveSession(session);
                    break;
                }
            }
        }
        
        public void Broadcast(Message message,params Session[] exclude)
        {
            // 利用 IDisposable 的 using 语法糖，确保 message 在使用完毕后被回收
            using var _ = message;
            // InvalidOperationException: Collection was modified; enumeration operation may not execute.
            var cached = new List<Session>(sessions);
            var list = new List<Session>(exclude);
            foreach (var session in cached)
            {
                if (list.Contains(session))
                {
                    continue;
                }
                Send(session, message);
            }
            cached.Clear();
        }

        public void BroadcastOthers(Session self, Message message)
        {
            // 利用 IDisposable 的 using 语法糖，确保 message 在使用完毕后被回收
            using var _ = message;
            var cached = new List<Session>(sessions);
            cached.Remove(self);
            foreach (var s in cached)
            {
                Send(s, message);
            }
            cached.Clear();
        }

        public async Task<T> Call<T>(Session session, IRequest request) where T : class, IResponse, new()
        {
            T response = null;
            try
            {
                if (session == null)
                {
                    return response;
                }
                response = await session?.Call<T>(request);
            }
            catch (RpcResponseException re)
            {
                Debug.LogError($"{nameof(TinyServer)}: RPC Response Excption {re}");
            }
            catch (RpcTimeoutException te)
            {
                Debug.LogError($"{nameof(TinyServer)}: RPC Timeout {te}");
            }
            //未知异常直接关断会话
            //如果还有其他已知不应该关断会话的可以在这里插入
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TinyServer)}: RPC Error {e}");
                HandleDisactiveSession(session);
            }
            return response;
        }

        public void Send(Session session, Message message)
        {
            try
            {
                session.Send(message);
            }
            catch (Exception)
            {
                //如果消息发送失败，说明客户端已经断开连接，需要移除
                HandleDisactiveSession(session);
            }
        }

        #region Assistant Function
        private void HandleDisactiveSession(Session session)
        {
            Debug.Log($"{nameof(TinyServer)}:  Session is disconnected! \n{session}");
            session.Close();
            lock (sessions)
            {
                sessions.Remove(session);
            }
            context.Post(v => OnClientDisconnected?.Invoke(session), null);
        }
        #endregion

        #region Ping Message Handler
        internal static async Task OnPingReceived(Session session, Ping request, Ping response)
        {
            response.time = ServerTime;
            await Task.CompletedTask;
        }
        #endregion

        internal TcpListener listener;
        readonly SynchronizationContext context;
        CancellationTokenSource source;
        internal readonly List<Session> sessions = new List<Session>();
        private static long ServerTime => DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    }
}
