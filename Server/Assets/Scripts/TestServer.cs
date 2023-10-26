using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

public class TestServer : MonoBehaviour
{
    public int port = 8889;
    TCPServer server;
    Session session;
    public Button button;
    public Button button2;

    private void Start()
    {
        button.onClick.AddListener(SendRPC);
        button2.onClick.AddListener(SendNormalMessage);

        server = new TCPServer(port);
        server.OnClientEstablished += Server_OnClientEstablished;
        server.OnClientDisconnected += Server_OnClientDisconnected;
        server.Start();
        Debug.Log($"{nameof(TestServer)}:  server started on port = {port} ！！！");
    }

    private void SendNormalMessage()
    {
        if (session != null)
        {
            var message = new TestMessage
            {
                message = "normal message from tinyrpc SERVER",
                age = 8888
            };
            Debug.Log($"{nameof(TestServer)}: Send Test Message ！");
            server.Send(session, message);
        }
        else
        {
            Debug.LogWarning($"{nameof(TestServer)}: 还没有客户端登录哦！");
        }
    }

    private async void SendRPC()
    {
        var request = new TestRPCRequest();
        request.name = "request from tinyrpc SERVER";
        var time = Time.realtimeSinceStartup;
        Debug.Log($"{nameof(TestServer)}: Send Test RPC Request ！");
        if (session != null)
        {
            var response = await server.Call<TestRPCResponse>(session, request);
            if (response != null)
            {
                Debug.Log($"{nameof(TestServer)}: Receive RPC Response ：{response.name}  , cost = {Time.realtimeSinceStartup - time}");
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(TestServer)}: 还没有客户端登录哦！");
        }
    }

    private void Server_OnClientDisconnected(Session obj)
    {
        Debug.Log($"{nameof(TestServer)}: Client Disconnected {obj}");
    }

    private void Server_OnClientEstablished(Session obj)
    {
        this.session = obj;
        Debug.Log($"{nameof(TestServer)}: Client Connected {obj}");
    }

    private void OnApplicationQuit()
    {
        server.Stop();
    }
}
