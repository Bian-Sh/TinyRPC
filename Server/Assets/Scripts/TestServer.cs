using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
[MessageHandlerProvider]
public class TestServer : MonoBehaviour
{
    TCPServer server;
    private void Start()
    {
        int port = 8899;
        server = new TCPServer(port);
        server.OnClientEstablished += Server_OnClientEstablished;
        server.OnClientDisconnected += Server_OnClientDisconnected;
        server.Start();
        Debug.Log($"{nameof(Test)}:  server started ！！！");
    }

    private void Server_OnClientDisconnected(Session obj)
    {
        Debug.Log($"{nameof(Test)}: Client Disconnected {obj}");
    }

    private void Server_OnClientEstablished(Session obj)
    {
        Debug.Log($"{nameof(Test)}: Client Connected {obj}");
    }

    private void OnApplicationQuit()
    {
        server.Stop();
    }

    #region Custom Handlers
    [MessageHandler(MessageType.Normal)]
    private static void MessageHandler(Session session, TestClass message)
    {
        // todo 
    }

    [MessageHandler(MessageType.RPC)]
    private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        Debug.Log($"{nameof(Test)}: Receive {session} request {request}");
        await Task.Delay(1000);
        response.name = "response after rpc";
    }
    #endregion
}
