using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    public class Session
    {
        public bool IsServer => communicate is TCPServer;
        public ICommunicate communicate;
        public TcpClient client;
        public DateTime lastPingSendTime;
        public DateTime lastPingReceiveTime;
        public float Ping => (float)(lastPingReceiveTime - lastPingSendTime).TotalMilliseconds;

        public void Send(Message message)=> communicate.Send(this,message);
        
        public async Task<T> Call<T>(Request request) where T : Response, new() => await communicate.Call<T>(this,request);
    }
}
