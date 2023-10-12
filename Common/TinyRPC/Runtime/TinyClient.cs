using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using zFramework.TinyRPC.DataModel;
using System.Threading.Tasks;

namespace zFramework.TinyRPC
{
    public class TinyClient
    {
        public bool IsConnected => client != null && client.Connected;
        public Session Session { get; private set; }
        public Action<Session> OnClientEstablished;
        public Action<Session> OnClientDisconnected;
        public int PingInterval = 2000;
        public int PingTimeout = 6000;
        public int PingCount = 3; // retry 3 times if ping timeout

        public TinyClient(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public void Start()
        {
            client = new TcpClient();
            source = new CancellationTokenSource();
            context = SynchronizationContext.Current;
            // alert must in main thread, otherwise throw exception
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new Exception("TinyClient.Start must be called in main thread!");
            }
            Task.Run(() => ReceiveAsync(source));
            // how to ping in main thread ? use AsyncOperation ,but need implement IEnumerator for await 
        }

        private async void ReceiveAsync(CancellationTokenSource token)
        {
            await client.ConnectAsync(ip, port);
            if (token.IsCancellationRequested) return;
            Session = new Session(client, context, false);
            OnClientEstablished?.Invoke(Session);
            try
            {
                _ = Task.Run(Session.ReceiveAsync);
                //context.Post(v => _ = PingAsync(), null); // ping 在主线程上下文执行
            }
            catch (Exception e)
            {
                Stop();
                Debug.LogError(e);
            }
        }

        public void Stop()
        {
            OnClientDisconnected?.Invoke(Session);
            source?.Cancel();
            client?.Close();
            source = null;
            client = null;
        }

        public void Send(Message message) => Session?.Send(message);
        public Task<T> Call<T>(IRequest request) where T : class, IResponse, new() => Session?.Call<T>(request);

        private async Task PingAsync()
        {
            while (Session.IsAlive)
            {
                Debug.Log($"{nameof(TinyClient)}: start ping ");
                var begin = DateTime.Now;
                var ping = await Call<Ping>(new Ping());
                var end = DateTime.Now;
                var result = (end - begin).Milliseconds;
                Debug.Log($"{nameof(TinyClient)}: receive ping , ttl = {result}");
                Debug.Log($"{nameof(TinyClient)}: before delay thread id = {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(2000);
                Debug.Log($"{nameof(TinyClient)}: after delay thread id = {Thread.CurrentThread.ManagedThreadId}");
            }
        }

        private string ip = "172.0.0.1";
        private int port = 12345;
        private TcpClient client;
        private CancellationTokenSource source;
        private SynchronizationContext context;
    }
}
