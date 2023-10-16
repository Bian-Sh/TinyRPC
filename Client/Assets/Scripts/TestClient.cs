using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
namespace zFramework.TinyRPC.Samples
{
    [MessageHandlerProvider]
    public class TestClient : MonoBehaviour
    {
        #region UI Component
        public Button connect;
        public Button sendrpc;
        public Text ping;
        #endregion

        TinyClient client;
        public int port = 8889;

        #region MonoBehaviour Func
        private void Start()
        {
            connect.onClick.AddListener(OnConnectedButtonClicked);
            sendrpc.onClick.AddListener(SendRPCAsync);
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
                client = new TinyClient("localhost", port);
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
            Debug.Log($"{nameof(TestClient)}: result1 {result[0]} , result2{result[1]}");
            //3. 取消登录中...的显示
            tcs.Cancel();
            Debug.Log($"{nameof(TestClient)}:  Thread id = {Thread.CurrentThread.ManagedThreadId}");
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


        [MessageHandler(MessageType.RPC)]
        private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
        {
            Debug.Log($"{nameof(TestClient)}: Receive {session} request {request}");
            await Task.Delay(500);
            response.name = "response  from  tinyrpc client !";
        }
    }
}