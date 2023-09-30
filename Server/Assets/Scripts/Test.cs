using EasyButtons;
using System;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.DataModel;

public class Test : MonoBehaviour
{

    private void OnEnable()
    {
        this.AddMessageHandler();
    }
    private void OnDisable()
    {
        this.RemoveMessageHandler();
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

    [MessageHandler(MessageType.Normal)]
    private void MessageHandler(Session session, TestClass message)
    {


    }

    [MessageHandler(MessageType.RPC)]
    private async void RPCMessageHandler(Session session,TestRPCRequest request, TestRPCResponse response)
    {
        await Task.Yield();
    }




}
