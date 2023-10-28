using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using zFramework.TinyRPC.Generated;

namespace zFramework.TinyRPC.Samples
{
    public class TestClient : MonoBehaviour
    {
        #region UI Component
        public InputField ip;
        public Button connect;
        public Button sendrpc;
        public Button sendNormalMessage;
        public Text ping;
        #endregion

        TinyClient client;
        public int port = 8889;

        #region MonoBehaviour Func
        private void Start()
        {
            ip.text = PlayerPrefs.GetString("ip", "127.0.0.1");
            ip.onEndEdit.AddListener(v => PlayerPrefs.SetString("ip", v));
            connect.onClick.AddListener(OnConnectedButtonClicked);
            sendrpc.onClick.AddListener(SendRPCAsync);
            sendNormalMessage.onClick.AddListener(SendNormalMessage);
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
                Debug.Log($"{nameof(TestClient)}: Send Test Message ！{message}");
                client.Send(message);
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClient)}: Please Connect Server First！");
            }
        }

        private void OnApplicationQuit() => client?.Stop();
        #endregion

        #region  UI Callbacks

        private void OnConnectedButtonClicked()
        {
            if (client != null && client.IsConnected)
            {
                client.Stop();
                client = null;
                connect.GetComponentInChildren<Text>().text = "Connect";
            }
            else
            {
                // 1. 构建客户端
                client = new TinyClient(ip.text, port);
                // 2. 注册 Client 事件
                client.OnClientEstablished += OnClientEstablished;
                client.OnClientDisconnected += OnClientDisconnected;
                client.OnPingCaculated += OnPingCaculated;
                // 3. 连接到服务器
                StartConnectAsync();
            }
        }

        private async void StartConnectAsync()
        {
            connect.interactable = false;
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
            if (result[1])
            {
                //4. 转换 connect 字样为 disconnect
                connect.GetComponentInChildren<Text>().text = "Disconnect";
                Debug.Log($"{nameof(TestClient)}: Client Started");
            }
            else
            {
                connect.GetComponentInChildren<Text>().text = "Connect";
            }
            connect.interactable = true;
        }

        public async void SendRPCAsync()
        {
            if (client != null && client.IsConnected)
            {
                var request = new TestRPCRequest();
                request.name = "request from tinyrpc client";
                var time = Time.realtimeSinceStartup;
                Debug.Log($"{nameof(TestClient)}: Send Test RPC Request ！");
                var response = await client.Call<TestRPCResponse>(request);
                Debug.Log($"{nameof(TestClient)}: Receive RPC Response ：{response.name}  , cost = {Time.realtimeSinceStartup - time}");
            }
            else
            {
                Debug.LogWarning($"{nameof(TestClient)}: Please Connect Server First！");
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
            Debug.Log($"{nameof(TestClient)}: Client Disconnected ");
            client = null;
            connect.GetComponentInChildren<Text>().text = "Connect";
            ping.text = "已断线";
        }

        private void OnClientEstablished()
        {
            Debug.Log($"{nameof(TestClient)}: Client Connected {client.Session}");
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