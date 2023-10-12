using EasyButtons;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using Ping = zFramework.TinyRPC.Ping;

public class TestClient : MonoBehaviour
{
    TinyClient client;
    public int port = 8889;

    [Button(Mode = ButtonMode.EnabledInPlayMode)]
    private void StartConnect()
    {
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

    //[Button( Mode = ButtonMode.EnabledInPlayMode)]
    public async void TestPing()
    {
        var begin = DateTime.Now;
        var ping = await client.Call<Ping>(new Ping());
        var end = DateTime.Now;
        var result = (end - begin).Milliseconds;
        Debug.Log($"{nameof(TinyClient)}: receive ping , ttl = {result}");
        Debug.Log($"{nameof(TinyClient)}: before delay thread id = {Thread.CurrentThread.ManagedThreadId}");
        await Task.Delay(2000);
        Debug.Log($"{nameof(TinyClient)}: after delay thread id = {Thread.CurrentThread.ManagedThreadId}");
    }

}
