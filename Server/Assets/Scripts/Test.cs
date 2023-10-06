using EasyButtons;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.DataModel;
[MessageHandlerProvider]
public class Test : MonoBehaviour
{
    TinyClient client;
    TCPServer server;
    private void Start()
    {
        int port = 8899;
        server = new TCPServer(port);
        server.OnClientEstablished += Server_OnClientEstablished;
        server.Start();
        client = new TinyClient("localhost", port);
        client.Start();
        Debug.Log($"{nameof(Test)}: finish init server and client");
    }

    private void Server_OnClientEstablished(Session obj)
    {
        Debug.Log($"{nameof(Test)}: Client Connected {obj}");
    }

    private void OnApplicationQuit()
    {
        server.Stop();
        client.Stop();
    }

    [Button]
    public async void TestRPC()
    {
        var request = new TestRPCRequest();
        request.name = "request啊";
        Debug.Log($"{nameof(Test)}:  client is null {client == null} request is null ={request == null}");
        var response = await client.Call<TestRPCResponse>(request);
        Debug.Log($"{nameof(Test)}: {response.name} ");
    }



    [Button]
    public void Test1()
    {
        Debug.Log($"{nameof(Test)}: {(byte)MessageType.RPC} ");
    }

    [Button]
    public void Test2() //test 多态
    {
        var warpper = new MessageWrapper();
        var rsp = new TestRPCResponse();
        rsp.name = "response啊";
        warpper.Message = rsp;
        var json = JsonUtility.ToJson(warpper);
        Debug.Log($"{nameof(Test)}: {json} ");

        var warpper2 = JsonUtility.FromJson<MessageWrapper>(json);
        Debug.Log($"{nameof(Test)}: {warpper2.Message.GetType()}  ");
        var rsp2 = (TestRPCResponse)warpper2.Message;
        Debug.Log($"{nameof(Test)}: {rsp2.name}  ");
    }

    [Button("Test Regist message handler")]
    public void Test3()
    {
        MessageManager.StoreRPCMessagePairs();
        //MessageManager.RegisterAllHandlers();
    }


    [MessageHandler(MessageType.Normal)]
    private static void MessageHandler(Session session, TestClass message)
    {

    }

    [MessageHandler(MessageType.RPC)]
    private static async void RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        await Task.Yield();
        response.name = "response啊xx";
    }



}
