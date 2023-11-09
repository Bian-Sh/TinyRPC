TinyRPC

---

# TinyRPC

TinyRPC 是一个使用 Socket + JsonUtility 的没有第三方依赖的简易 RPC 框架。

它的目标是提供一个轻量级、易于使用的 RPC 解决方案。

支持优雅的 ``async await`` 异步逻辑同步写的编码方式，让你的代码更加简洁易读。

客户端和服务器都支持发送 RPC 请求。

当然，除了 RPC 消息，我还提供了普通消息的处理，这样你就可以在一个项目中同时使用 RPC 和普通消息了。

这个网络框架很多地方学习参考了 [ET](https://github.com/egametang/ET) ，在此表示感谢。


# 功能

> 消息的发送

* 使用 ``Send(message)`` 发送普通网络消息

* 使用 ``var response =  await Call(request)`` 发送 RPC 请求并等待响应

> 观察者模式注册的消息处理器

* 使用 ``UnityEngine.Component.AddNetworkSignal<Session,T>()`` 注册一个普通的网络处理器

* 使用 ``UnityEngine.Component.AddNetworkSignal<Session,TRequest,TResponse>()`` 注册一个 RPC 处理器

> 通过反射自动注册消息处理器

* 使用 ``[MessageHandlerProviderAttribute]`` 标记一个消息处理器容器（类型）
* 使用 ``[MessageHandlerAttribute(MessageType.Normal)]`` 标记一个消息处理器
* 使用 ``[MessageHandlerAttribute(MessageType.RPC)]`` 标记一个 RPC 消息处理器

> 消息类一键生成

使用基于 proto3 精简版语法的 .proto 文件，可以一键生成消息类。如果存在多个 .proto 文件则会将消息生成在 .proto 文件名命名的文件夹中。

支持将生成的消息存在 Assets、Project 同级以及 Packages 文件夹中。他们的优越性在于生成在 Packages 文件夹中不会对用户工程目录有任何侵入性；存在 Project 同级目录将最大化消息文件在多个工程中的复用（本项目架构情形）。


![](doc/editor.png)

> 运行时参数配置界面

提供了一个可以编辑器下修改运行时生效的配置界面，可以配置日志过滤器；心跳间隔和重试次数；同时也会自动记录消息处理器所在的程序集信息

![](doc/runtime.png)



## RPC 原理

新版本的 Unity 对 Task 的支持越来越完备，TinyRPC 使用 ``System.Threading.Tasks`` 命名空间下的 ``TaskCompletionSource`` 来实现 RPC 的异步等待。

使用  ``System.Threading.Tasks`` 命名空间下的 ``CancellationTokenSource.CancelAfter(delay)`` 来实现 RPC 的超时控制。


## 安装和设置

1. 克隆此仓库到你的本地机器上。
2. 在 Unity 编辑器中分别打开 **Client** 和  **Server** 项目
3. 先运行 **Server** ，此时会自动创建服务器
4. 再运行 **Client** , 点击 Play 后在 Game 窗口可以连接/断开服务器，通过 SendRPC 测试 RPC 会话。
5. 本项目使用 Unity 2021.3.11f2 开发  ，请使用此版本或者更高阶版本                      

![](./doc/TinyRPC.gif)

## 快速开始

### 登录、消息发送

在 Unity 客户端中，我们使用了以下关键 API：

- `client.ConnectAsync()`: 这个方法用于连接到服务器。
- `client.Send(message)`: 这个方法用于发送一个普通消息。
- `await client.Call<TestRPCResponse>(request)`: 这个方法用于发送一个 RPC 请求并等待响应。

> 登录逻辑

下面是 TinyRPC 连接服务器逻辑 

也模拟了网络不好情况下的登录表现，展示了如何通过 async 异步对登录流程的优雅控制。

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

      //4. 转换 connect 字样为 disconnect
      connect.GetComponentInChildren<Text>().text = "Disconnect";
      connect.interactable = true;
  }
```

 这段逻辑中，我使用 Task.WhenAll + Task.Delay 实现了一个长时间的登录效果，这样文本组件就有足够时间展示 Connect... 动画了，当登录完成，就将文本改为 Disconnect，方便下个回合的交互。

 当然，你还可以对这段逻辑 Try Catch，处理登录失败的情况，这里我就不誊写啦，更多交互细节请运行示例项目体验。

 > RPC 消息发送

下面是 Tiny RPC 发送 RPC 并等待回应的逻辑，同样，得益于 RPC 的使用，与服务器的对话再也不需要调用 监听者模式这种割裂的交互方式了（完善后的TinyRPC也支持监听模式，毕竟还有常规消息要处理嘛）

```csharp
        public async void SendRPCAsync()
        {
            if (client != null && client.IsConnected)
            {
                var request = new TestRPCRequest();
                request.name = "request from tinyrpc client";
                var response = await client.Call<TestRPCResponse>(request);
            }
        }
```

 这段逻辑中，我先构建了一个请求，并告知服务请求的信息，接着等待服务器返回的数据，然后 log 输出到屏幕，

 > 常规消息发送

下面是 Tiny RPC 发送常规消息的逻辑，构建一个 Normal 消息，调用 Send 就好啦。

```csharp
        private void SendNormalMessage()
        {
            if (client != null && client.IsConnected)
            {
                var message = new TestMessage
                {
                    message = "normal message from tinyrpc client",
                    age = 999
                };
                client.Send(message);
            }
        }
```

### 消息处理

在服务器端，我们使用了消息处理器来处理接收到的各类消息，Normal 消息、RPC 消息、Ping 消息。

当然，在客户端也支持通过注册消息处理器对 Normal 消息、RPC 消息的处理 （Ping 消息除外），处理用户收到网络消息后的业务逻辑。


> Ping 消息处理器

Ping 消息是一个内置的自响应消息，交由系统自己处理，用户无需关注

```csharp
#region Ping Message Handler 
private static async Task OnPingRecevied(Session session, Ping request, Ping response) 
{ 
    response.Id = request.Id; 
    response.time = ServerTime; 
    await Task.Yield(); 
} 
#endregion 
```

> RPC 消息处理器


1. 下面的示例脚本中对使用 ``MessageHandlerProviderAttribute``、 ``MessageHandlerAttribute`` 声明 RPC 消息处理器

```csharp
[MessageHandlerProvider]
class Foo
{
    [MessageHandler(MessageType.RPC)]
    private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        Debug.Log($"{nameof(TestServer)}: Receive {session} request {request}");
        await Task.Delay(1000);
        response.name = "response  from  tinyrpc server !";
    }
}
```

这个消息处理器收到 RPC 请求后，间隔了一秒钟，然后向请求端发出响应信息： “response  from  tinyrpc server ”。

2. 下面示例脚本中演示使用 ``UnityEngine.Component.AddNetworkSignal<Session,TRequest,TResponse>()`` 注册 RPC 消息处理器

```csharp
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

public class Foo : MonoBehaviour
{
    private void OnEnable()=>this.AddNetworkSignal<TestRPCRequest, TestRPCResponse>(RPCMessageHandler);

    private void OnDisable()=>this.RemoveNetworkSignal<TestRPCRequest, TestRPCResponse>(RPCMessageHandler);
    
    private static async Task RPCMessageHandler(Session session, TestRPCRequest request, TestRPCResponse response)
    {
        await Task.Delay(500);
        response.name = $"response  from  tinyrpc {(session.IsServerSide ? "SERVER" : "CLIENT")}  !";
    }
}
```

> 普通消息处理器

1. 下面的示例脚本中对使用 ``MessageHandlerProviderAttribute``、 ``MessageHandlerAttribute`` 声明普通消息处理器

```csharp
[MessageHandlerProvider]
class Foo
{
	[MessageHandler(MessageType.Normal)]
	private static void NormalMessageHandler(Session session, TestMessage message)
	{
		Debug.Log($"{nameof(TestServer)}: Receive {session} message {message}");
	}
}
```

这个消息处理器收到普通消息后，直接 log 输出到屏幕。

2. 下面示例脚本中演示使用 ``UnityEngine.Component.AddNetworkSignal<Session,T>()`` 注册普通消息处理器

```csharp

using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

public class Foo: MonoBehaviour
{
    private void OnEnable()=>this.AddNetworkSignal<TestMessage>(OnTestMessageReceived);

    private void OnDisable()=>this.RemoveNetworkSignal<TestMessage>(OnTestMessageReceived);

    private void OnTestMessageReceived(Session session, TestMessage message)
    {
        Debug.Log($"获取到{(session.IsServerSide ? "客户端" : "服务器")}  {session}  的消息, message = {message}");
    }
}
```


# 框架架构

下面是 TinyRPC 的文件系统树，点击可以看到完整网络架构

<details>
<summary>  点我 ^_^</summary>

```
<root>
+---Editor
|   |   
|   +---Analyzer
|   |       MessageHandlerPostprocessor.cs
|   |       
|   +---CodeGen
|   |       ProtoContentProcessor.cs
|   |       TinyProtoHandler.cs
|   |       
|   +---Data
|   |       ScriptInfo.cs
|   |       ScriptType.cs
|   |       
|   +---GUI
|   |       EditorSettingsLayout.cs
|   |       RuntimeSettingsLayout.cs
|   |       TinyRpcEditorWindow.cs
|   |       
|   \---Settings
|           EditorSettingWatcher.cs
|           ScriptableSingleton.cs
|           TinyRpcEditorSettings.cs
|           
\---Runtime
    |   
    +---Data
    |       MessageType.cs
    |       MessageWrapper.cs
    |       RpcInfo.cs
    |       SerializeHelper.cs
    |       TinyRpcSettings.cs
    |       
    +---Exception
    |       InvalidSessionException.cs
    |       RpcResponseException.cs
    |       RpcTimeoutException.cs
    |       
    +---Handler
    |   |   
    |   +---Attribute
    |   |       MessageHandlerAttribute.cs
    |   |       MessageHandlerProviderAttribute.cs
    |   |       
    |   +---Base
    |   |       NormalMessageHandler.cs
    |   |       RpcMessageHandler.cs
    |   |       
    |   +---Extension
    |   |       MessageHandlerEx.cs
    |   |       
    |   \---Interface
    |           INormalMessageHandler.cs
    |           IRpcMessageHandler.cs
    |           
    +---Internal
    |       Manager.cs
    |       Session.cs
    |       TinyClient.cs
    |       TinyServer.cs
    |       
    \---Message
        |   
        +---Attribute
        |       ResponseTypeAttribute.cs
        |       
        +---Base
        |       Message.cs
        |       Ping.cs
        |       Request.cs
        |       Response.cs
        |       
        \---Interface
                IMessage.cs
                IRequest.cs
                IResponse.cs
                IRpcMessage.cs
                
```
</details>

## 贡献指南

如果你有任何问题或建议，欢迎提交 issue 或 pull request。

## License

遵循 MIT 开源协议