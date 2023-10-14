TinyRPC

---

# TinyRPC

TinyRPC 是一个使用 Socket + JsonUtility 的简易 RPC 框架。它的目标是提供一个轻量级、易于使用的 RPC 解决方案。

## 项目介绍

TinyRPC 项目包含了一个使用 UPM （Unity PackageManager）管理的 RPC 插件，无第三方依赖。

同时提供了 Unity 客户端和服务器的示例。这个示例展示了如何使用 TinyRPC 构建一个 Unity 客户端和服务器，包括如何连接到服务器，如何发送 RPC 请求，以及如何处理服务器的响应。

## RPC 原理

TinyRPC 使用 `MessageManager` 来处理所有的消息。对于普通消息，`MessageManager` 会查找对应的消息处理器并调用它。对于 RPC 请求，`MessageManager` 会创建一个新的任务并等待响应。一旦收到响应，`MessageManager` 会完成任务并移除它。

```csharp
internal static async void HandleRpcRequest(Session session, IRequest request)
{
    if (rpcHandlers.TryGetValue(request.GetType(), out var info))
    {
        if (rpcMessagePairs.TryGetValue(info.Request, out var responseType))
        {
            var response = Activator.CreateInstance(responseType) as IResponse;
            response.Id = request.Id;
            var task = info.method.Invoke(null, new object[] { session, request, response });
            await (task as Task);
            session.Reply(response);
            return;
        }
    }
    var response_fallback = new Response
    {
        Id = request.Id,
        Error = $"RPC 消息 {request.GetType().Name} 没有找到对应的处理器！"
    };
    session.Reply(response_fallback);
}

internal static void HandleRpcResponse(Session session, IResponse response)
{
    if (rpcInfoPairs.TryGetValue(response.Id, out var rpcInfo))
    {
        rpcInfo.task.SetResult(response);
        rpcInfoPairs.Remove(response.Id);
    }
}
```

在 `Session` 类中处理分包粘包的问题和消息的发送与接受，使用了以下关键代码来处理封包和接收到的消息：

```csharp
public async void ReceiveAsync()
{
    var stream = client.GetStream();
    while (!source.IsCancellationRequested)
    {
        // 读出消息的长度
        var head = new byte[4];
        var byteReaded = await stream.ReadAsync(head, 0, head.Length, source.Token);
        if (byteReaded == 0)
        {
            throw new Exception("断开连接！");
        }
        // 读出消息的内容
        var bodySize = BitConverter.ToInt32(head, 0);
        var body = new byte[bodySize];
        byteReaded = 0;
        // 当读取到 body size 后的数据读取需要加入超时检测
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        while (byteReaded < bodySize)
        {
            var readed = await stream.ReadAsync(body, byteReaded, body.Length - byteReaded, cts.Token);
            // 读着读着就断线了的情况，如果不处理，此处会产生死循环
            if (readed == 0)
            {
                throw new Exception("断开连接！");
            }
            byteReaded += readed;
        }
        if (bodySize != byteReaded) // 消息不完整，此为异常，断开连接
        {
            throw new Exception("消息不完整,会话断开！");
        }
        // 解析消息类型
        var type = body[0];
        var content = new byte[body.Length - 1];
        Array.Copy(body, 1, content, 0, content.Length);
        OnMessageReceived(type, content);
    }
}
```

在 Session 中对接收到的消息做层别并使用给自的消息处理器来处理，逻辑如下

```csharp
private void OnMessageReceived(byte type, byte[] content)
{
    lastPingReceiveTime = DateTime.Now;

    var message = SerializeHelper.Deserialize(content);
    if (!TinyRpcSettings.Instance.LogFilters.Contains(message.GetType().FullName))
    {
        Debug.Log($"{nameof(Session)}:   {(IsServerSide ? "Server" : "Client")} 收到网络消息 =  {JsonUtility.ToJson(message)}");
    }
    switch (type)
    {
        case 0: //normal message
            {
                context.Post(_ => HandleNormalMessage(this, message), null);
            }
            break;
        case 1: // rpc message
            {
                if (message is Request || (message is Ping && IsServerSide))
                {
                    context.Post(_ => HandleRpcRequest(this, message as IRequest), null);
                }
                else if (message is Response || (message is Ping && !IsServerSide))
                {
                    context.Post(_ => HandleRpcResponse(this, message as IResponse), null);
                }
            }
            break;
        default:                                                                                                                                                                                                                                                            
            break;
    }
}
```

## 安装和设置

1. 克隆此仓库到你的本地机器上。
2. 在 Unity 编辑器中分别打开 **Client** 和  **Server** 项目
3. 先运行 **Server** ，此时会自动创建服务器
4. 再运行 **Client** , 点击 Play 后在 Game 窗口可以连接/断开服务器，通过 SendRPC 测试 RPC 会话。
5. 本项目使用 Unity 2021.3.11f2 开发  ，请使用此版本或者更高阶版本                      

![](./doc/TinyRPC.gif)

## 快速开始

### 客户端

在 Unity 客户端中，我们使用了以下关键 API：

- `client.ConnectAsync()`: 这个方法用于连接到服务器。
- `client.Call<TestRPCResponse>(request)`: 这个方法用于发送一个 RPC 请求并等待响应。

下面是 TinyRPC 链接服务器的测试代码,这个逻辑通过模拟网络不好情况下的登录表现，展示了如果通过 async 异步对登录流程的优雅控制。

```csharp
  private async void StartConnectAsync()
  {
      connect.interactable = false;
      //模拟网络延迟情况下的登录
      //1. 显示登录中...
      var tcs = new CancellationTokenSource();
      _ = TextLoadingEffectAsync(tcs);

      //2. 模拟一个延迟完成的登录效果
      var delay = Task.Delay(3000);
      var task = client.ConnectAsync();
      await Task.WhenAll(delay, task);

      //3. 取消登录中...的显示
      tcs.Cancel();
      Debug.Log($"{nameof(TestClient)}:  Thread id = {Thread.CurrentThread.ManagedThreadId}");
      //4. 转换 connect 字样为 disconnect
      connect.GetComponentInChildren<Text>().text = "Disconnect";
      connect.interactable = true;
      Debug.Log($"{nameof(TestClient)}: Client Started");
  }
```

> 这段逻辑中，我使用 Task.WhenAll + Task.Delay 实现了一个长时间的登录效果，这样文本组件就有足够时间展示 Connect... 动画了，当登录完成，就将文本改为 Disconnect，方便下个回合的交互。

下面是 Tiny RPC 发送 RPC 并等待回应的逻辑，同样，得益于 RPC 的使用，与服务器的对话再也不需要调用 监听者模式这种割裂的交互方式了（完善后的TinyRPC也支持监听模式，毕竟还有常规消息要处理嘛）

```csharp
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
```

> 这段逻辑中，我先构建了一个请求，并告知服务请求的信息，接着等待服务器返回的数据，然后 log 输出到屏幕，

### 服务器

在服务器端，我们使用了消息处理器来处理接收到的各类消息，下面是 Ping 消息的处理逻辑，你可以参考它实现自己的消息处理器

```csharp
#region Ping Message Handler 
[MessageHandler(MessageType.RPC)] 
private static async Task OnPingRecevied(Session session, Ping request, Ping response) 
{ 
response.Id = request.Id; 
response.time = ServerTime; 
await Task.Yield(); 
} 
#endregion 
```

 TestServer 示例脚本中对声明的消息处理器的逻辑是一个很好的参考，这个逻辑输出了请求的细节，再间隔一秒钟后发出响应。

```csharp
    [MessageHandler(MessageType.RPC)]
    private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        Debug.Log($"{nameof(TestServer)}: Receive {session} request {request}");
        await Task.Delay(1000);
        response.name = "response  from  tinyrpc server !";
    }
```

> 请注意，注册消息处理器需要给函数标记 ``MessageHandlerAttribute`` ，同时给消息处理器所在的类型加上 ``MessageHandlerProviderAttribute``

# 文件系统

下面是 TinyRPC 的文件系统树，可以看到基础架构

```
卷 数据 的文件夹 PATH 列表
卷序列号为 0E4D-4592
E:.
|   1.txt
|   FileTree.bat
|   package.json
|   temp.txt
|   
+---Editor
|   |   com.zframework.tinyrpc.editor.asmdef
|   |   Temp.cs
|   |   
|   +---Analyzer
|   |       .gitkeep
|   |       
|   +---Data
|   |       .gitkeep
|   |       
|   \---GUI
|           .gitkeep
|           
\---Runtime
    |   com.zframework.tinyrpc.runtime.asmdef
    |   TCPServer.cs
    |   TinyClient.cs
    |   
    +---Exception
    |       RpcException.cs
    |       TimeoutException.cs
    |       
    +---Internal
    |       RpcInfo.cs
    |       SerializeHelper.cs
    |       Session.cs
    |       
    +---Message
    |   |   MessageHandlerAttribute.cs
    |   |   MessageHandlerProviderAttribute.cs
    |   |   MessageManager.cs
    |   |   MessageType.cs
    |   |   MessageWrapper.cs
    |   |   
    |   +---Attribute
    |   |       BaseAttribute.cs
    |   |       MessageAttribute.cs
    |   |       ResponseTypeAttribute.cs
    |   |       
    |   +---Base
    |   |       Message.cs
    |   |       Ping.cs
    |   |       Request.cs
    |   |       Response.cs
    |   |       
    |   \---Interface
    |           IMessage.cs
    |           IRequest.cs
    |           IResponse.cs
    |           
    \---Setting
            TinyRpcSettings.cs
```

## 贡献指南

目前这个插件正在开发中，如果你有任何问题或建议，欢迎提交 issue 或 pull request。
