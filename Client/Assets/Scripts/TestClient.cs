using EasyButtons;
using UnityEngine;
using zFramework.TinyRPC;
public class TestClient : MonoBehaviour
{
    TinyClient client;
    private void Start()
    {
        int port = 8899;
        client = new TinyClient("localhost", port);
        client.OnClientEstablished += Client_OnClientEstablished;
        client.OnClientDisconnected += Client_OnClientDisconnected;
        client.Start();
        Debug.Log($"{nameof(TestClient)}: Client Started");
    }

    private void Client_OnClientDisconnected(Session obj)
    {
        Debug.Log($"{nameof(TestClient)}: Client Disconnected {obj}");
    }

    private void Client_OnClientEstablished(Session obj)
    {
        Debug.Log($"{nameof(TestClient)}: Client Connected {obj}");
    }

    private void OnApplicationQuit()
    {
        client.Stop();
    }
    
    [Button(Mode = ButtonMode.EnabledInPlayMode)]
    public async void TestRPC()
    {
        var request = new TestRPCRequest();
        request.name = "request啊";
        var response = await client.Call<TestRPCResponse>(request);
        Debug.Log($"{nameof(TestClient)}: Receive RPC Response ：{response.name} ");
    }
}
