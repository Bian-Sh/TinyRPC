using System;
using UnityEngine;
using UnityEngine.UI;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

/*
这个类演示了如何使用 TinyRPC 服务器和 Discovery 服务器
它提供了发送普通消息和 RPC 请求的方法。它还处理客户端连接和断开连接事件。
This class, TestServerWithDiscovery, is a MonoBehaviour that sets up a TinyRPC server and a Discovery server. 
It has methods to send normal messages and RPC requests to a client. 
It also handles client connection and disconnection events.
*/

public class TestServerWithDiscovery : MonoBehaviour
{
    // TinyRPC Stuff
    TinyServer server;
    Session session;

    // Discovery Stuff
    public int discoveryPort = 8081;
    public string scope = "TinyRPC.001";
    DiscoveryServer discoveryServer;

    // UGUI Components
    public Button button;
    public Button button2;

    #region  Monobehaviour Callbacks
    private void Start()
    {
        button.onClick.AddListener(SendRPC);
        button2.onClick.AddListener(SendNormalMessage);

        var port = GetAvaliablePort();
        server = new TinyServer(port);
        server.OnClientEstablished += Server_OnClientEstablished;
        server.OnClientDisconnected += Server_OnClientDisconnected;
        server.Start();
        Debug.Log($"{nameof(TestServerWithDiscovery)}:  server started on port = {port} ！！！");

        discoveryServer = new DiscoveryServer(discoveryPort, scope, port);
        discoveryServer.Start();
        Debug.Log($"{nameof(TestServerWithDiscovery)}:  discovery server started broadcast to port {discoveryPort} ！！！");
    }



    private void OnApplicationQuit()
    {
        server.Stop();
        discoveryServer.Stop();
    }
    #endregion

    #region  Try Communicate with TinyRPC Client
    private void SendNormalMessage()
    {
        if (session != null)
        {
            var message = new TestMessage
            {
                message = "normal message from tinyrpc SERVER",
                age = 8888
            };
            Debug.Log($"{nameof(TestServerWithDiscovery)}: Send Test Message ！");
            server.Send(session, message);
        }
        else
        {
            Debug.LogWarning($"{nameof(TestServerWithDiscovery)}: 还没有客户端登录哦！");
        }
    }

    private async void SendRPC()
    {
        var request = new TestRPCRequest();
        request.name = "request from tinyrpc SERVER";
        var time = Time.realtimeSinceStartup;
        Debug.Log($"{nameof(TestServerWithDiscovery)}: Send Test RPC Request ！");
        if (session != null)
        {
            var response = await server.Call<TestRPCResponse>(session, request);
            if (response != null)
            {
                Debug.Log($"{nameof(TestServerWithDiscovery)}: Receive RPC Response ：{response.name}  , cost = {Time.realtimeSinceStartup - time}");
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(TestServerWithDiscovery)}: 还没有客户端登录哦！");
        }
    }
    #endregion

    #region TinyRPC Server Events
    private void Server_OnClientDisconnected(Session obj)
    {
        Debug.Log($"{nameof(TestServerWithDiscovery)}: Client Disconnected {obj}");
    }

    private void Server_OnClientEstablished(Session obj)
    {
        this.session = obj;
        Debug.Log($"{nameof(TestServerWithDiscovery)}: Client Connected {obj}");
    }
    #endregion

    private int GetAvaliablePort()
    {
        var port = 8088; // default port
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 0);
            listener.Start();
            port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(TestServerWithDiscovery)}:  GetAvaliablePort Error {e}");
        }
        return port;
    }
}
