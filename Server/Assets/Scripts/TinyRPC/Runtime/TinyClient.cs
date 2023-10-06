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
        }

        private async void ReceiveAsync(CancellationTokenSource token)
        {

            Debug.Log($"{nameof(TinyClient)}: connected to server before a");
            await client.ConnectAsync(ip, port);
            Debug.Log($"{nameof(TinyClient)}: connected to server before");
            if (token.IsCancellationRequested) return;
            Debug.Log($"{nameof(TinyClient)}: connected to server");
            Session = new Session(client, context, false);
            try
            {
                _ = Task.Run(Session.ReceiveAsync);
            }
            catch (Exception e)
            {
                Stop();
                Debug.LogError(e);
            }
        }

        public void Stop()
        {
            source?.Cancel();
            client?.Close();
            source = null;
            client = null;
        }

        public void Send(Message message) => Session?.Send(message);
        public Task<T> Call<T>(Request request) where T : Response, new() => Session?.Call<T>(request);


        private string ip = "172.0.0.1";
        private int port = 12345;
        private TcpClient client;
        private CancellationTokenSource source;
        private SynchronizationContext context;
    }
}
