using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using zFramework.TinyRPC.Generated;

namespace zFramework.TinyRPC.Samples
{
    /*
     这个脚本演示了如何使用 TinyRPC 客户端以及 TinyRPC 的网络发现功能
     构建 DiscoveryClient 实例，通过 OnServerDiscovered 事件获取服务器的 IP 和 Port
     上述事件触发时构建 TinyCient 实例，注册 Client 事件，连接到服务器
     连接成功后，发送 RPC 消息和普通消息
     通过 OnPingCaculated 事件获取 Ping 值
     this script shows how to use TinyRPC client and TinyRPC's network discovery feature
     create a DiscoveryClient instance, get the server's IP and Port through the OnServerDiscovered event
     when the above event is triggered, create a TinyCient instance, register Client events, and connect to the server
     after the connection is successful, send RPC messages and normal messages
     get the Ping value through the OnPingCaculated event
    */

    public class TestClientWithDiscovery : MonoBehaviour
    {
        #region UI Component
        public Button connect;
        public Button sendrpc;
        public Button sendNormalMessage;
        public Button sendAttributeMarkedNormalMessage;
        public Button sendAttributeMarkedRPCMessage;
        public Text ping;
        #endregion

        TinyClient client;

        public int discoveryPort = 8081;
        public string scope = "TinyRPC.001";
        DiscoveryClient discoveryClient;

        #region MonoBehaviour Func
        private void Start()
        {
            // 由于有了网络发现现在它只用于展示连接状态
            connect.interactable = false;

            sendrpc.onClick.AddListener(SendRPCAsync);
            sendNormalMessage.onClick.AddListener(SendNormalMessage);
            sendAttributeMarkedNormalMessage.onClick.AddListener(SendAttributeMarkedNormalMessage);
            sendAttributeMarkedRPCMessage.onClick.AddListener(SendAttributeMarkedRPCMessage);

            discoveryClient = new DiscoveryClient(discoveryPort, scope);
            discoveryClient.OnServerDiscovered += OnServerDiscovered;
            discoveryClient.OnDiscoveryTimeout += OnDisplayTimeout;
            discoveryClient.Start();
        }

        private void OnDisplayTimeout()
        {
            Debug.Log($"{nameof(TestClientWithDiscovery)}: Discovery Timeout");
            ping.text = "Discovery Timeout";
        }

        private void OnApplicationQuit()
        {
            discoveryClient.OnDiscoveryTimeout -= OnDisplayTimeout;
            discoveryClient.OnServerDiscovered -= OnServerDiscovered;
            discoveryClient?.Stop();
            client?.Stop();
        }

        private void OnServerDiscovered(string arg1, int arg2)
        {
            Debug.Log($"{nameof(TestClientWithDiscovery)}: 发现 TinyRPC Server ，IP = {arg1}, Port = {arg2}");
            // 1. 构建客户端
            client = new TinyClient(arg1, arg2);
            // 2. 注册 Client 事件
            client.OnClientEstablished += OnClientEstablished;
            client.OnClientDisconnected += OnClientDisconnected;
            client.OnPingCaculated += OnPingCaculated;
            // 3. 连接到服务器
            StartConnectAsync();
        }
        #endregion

        #region  TinyRPC Server  Event and Callbacks 
        private async void StartConnectAsync()
        {
            //模拟网络延迟情况下的登录
            //1. 显示登录中...
            var tcs = new CancellationTokenSource();
            _ = TextLoadingEffectAsync(tcs);

            //2. 模拟一个延迟完成的登录效果
            var delay = Task.Delay(3000).ContinueWith(v => false);
            var task = client.ConnectAsync();
            bool[] result = await Task.WhenAll(delay, task);
            //3. 取消登录中...的显示
            tcs.Cancel();
            // 将登录结果反馈给 discoveryClient , result[1] 是 ConnectAsync 的返回值
            if (result[1])
            {
                //4. 转换 connect 字样为 disconnect
                connect.GetComponentInChildren<Text>().text = "Connected";
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Client Started");
                discoveryClient.Standby();
            }
            else
            {
                connect.GetComponentInChildren<Text>().text = "Disconnect";
            }
        }

        public async void SendRPCAsync()
        {
            if (client != null && client.IsConnected)
            {
                var request = new TestRPCRequest();
                request.name = "request from tinyrpc client";
                var time = Time.realtimeSinceStartup;
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Send Test RPC Request ！");
                var response = await client.Call<TestRPCResponse>(request);
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Receive RPC Response ：{response.name}  , cost = {Time.realtimeSinceStartup - time}");
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClientWithDiscovery)}: Please Connect Server First！");
            }
        }
        private async void SendAttributeMarkedRPCMessage()
        {
            // c2s_login 's handler is registered in server by [MessageHandler(MessageType.RPC)] attribute 
            // this logic is aimed to test the attribute marked rpc message , to see whether it can be sent and received correctly
            if (client != null && client.IsConnected)
            {
                var request = new C2S_Login
                {
                    name = "request from tinyrpc client",
                    password = "123456"
                };
                var cachedtime = Time.realtimeSinceStartup;
                var response = await client.Call<S2C_Login>(request);
                var cost = Time.realtimeSinceStartup - cachedtime;
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Attrubute Marked RPC Message Test ！");
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Receive RPC Response ：cost = {cost} , response = {response}  ");
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClientWithDiscovery)}: Please Connect Server First！");
            }
        }

        private void SendAttributeMarkedNormalMessage()
        {
            if (client != null && client.IsConnected)
            {
                var message = new AttributeRegistTestMessage
                {
                    desc = "normal message from tinyrpc client",
                };
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Send Test Message ！{message}");
                client.Send(message);
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClientWithDiscovery)}: Please Connect Server First！");
            }
        }

        private void SendNormalMessage()
        {
            if (client != null && client.IsConnected)
            {
                var message = new TestMessage
                {
                    message = "normal message from tinyrpc client",
                    age = 999
                };
                Debug.Log($"{nameof(TestClientWithDiscovery)}: Send Test Message ！{message}");
                client.Send(message);
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClientWithDiscovery)}: Please Connect Server First！");
            }
        }
        #endregion

        #region Client Callbacks
        private void OnPingCaculated(float arg1, int arg2)
        {
            ping.text = $"Ping: {arg2} ms \n客户端比服务器{(arg1 > 0 ? "慢" : "快")} {Mathf.Abs(arg1):f2} ms";
        }
        private void OnClientDisconnected()
        {
            Debug.Log($"{nameof(TestClientWithDiscovery)}: Client Disconnected ");
            client = null;
            connect.GetComponentInChildren<Text>().text = "Disconnected";
            ping.text = "已断线";
            // 重新开始发现服务器
            discoveryClient.Rescan();
        }

        private void OnClientEstablished()
        {
            Debug.Log($"{nameof(TestClientWithDiscovery)}: Client Connected {client.Session}");
        }
        #endregion

        #region Assistant 
        private async Task TextLoadingEffectAsync(CancellationTokenSource source)
        {
            var text = connect.GetComponentInChildren<Text>();
            var content = text.text;
            var dot = ".";
            while (!source.IsCancellationRequested)
            {
                text.text = content + dot;
                await Task.Delay(500);
                if (source.IsCancellationRequested) break;
                dot += ".";
                if (dot.Length > 3) dot = ".";
            }
        }
        #endregion

    }
}