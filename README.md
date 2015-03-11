# YLib

一些 C# 的库，（主要是个人Unity3d项目用）

### YLib.NetWorking.Sockets.YScoket

这是一个TCP Socket Client库，用来与服务器通信

发送和接受的包是由 `数据长度` + `数据` 组成的。

`数据长度` 占用多少字节是可以自定义的。默认是 4 bytes


发送和接收都在单独的线程进行，并且和主线程通过`Queue<byte[]>` 来交换数据。
通过 `YScoket` 这个接口注册的各种回调，都是在调用`YScoket` 的线程中被调用的。

所以这些回调函数可以安全的调用 UnityEngine 自身的功能。




实例代码:

```csharp
using System;
using System.Text;
using YLib.NetWorking.Sockets;

class Program
{
    public static void Main(string[] args)
    {
        // YScoket 是一个静态类
        YScoket.Ip = "127.0.0.1";
        YScoket.Port = 7890;
        
        // 数据长度字节数，默认是4bytes
        // YScoket.HeaderLength = 4;
        
        YScoket.OnConnect += OnConnect;
        YScoket.OnData += OnData;
        YScoket.OnDisconnect += OnDisConnect;
        
        YScoket.Start();

        // YScoket的循环
        // 在UnityEngine 中可以在一个 cs脚本中的 Update 方法中 调用 YSocket.Update();
        YScoket.Loop();
        
        // 这样YScoket便运行起来了
        // Send 方法 (Send会自动在头部加上数据长度):
        // byte[] data = blabal... ;
        // YScoket.Send(data);

        // 关闭
        // YScoket.Close();
        // 如果IP， PORT， 回调都设置过以后，那么重新链接只要调用Start即可
        // YScoket.Start();
    }
    
    
    public static void OnConnect()
    {
        Console.WriteLine("Connect OK...");
    }
    
    public static void OnData(byte[] data)
    {
        // 这里的data已经是去除 头部数据长度 后的 真是数据
        Console.WriteLine("Got Data {0}", Encoding.UTF8.GetString(data));
    }
    
    public static void OnDisConnect(string reason)
    {
        // 和服务器没有成功链接
        // 在这里可以提示用户，重新链接
        // 重新链接方法：
        // YScoket.Start();
        
        Console.WriteLine("DisConnect: {0}", reason);
    }
}

```
