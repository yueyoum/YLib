# YLib

一些 C# 的库，（主要是个人Unity3d项目用）

### YLib.NetWorking.Sockets.YScoket

这是一个TCP Socket Client库，用来与服务器通信

发送和接受的包是由 `数据长度` + `数据` 组成的。

`数据长度` 占用多少字节是可以自定义的。默认是 4 bytes



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
        
        //YScoket.HeaderLength = 4;
        
        YScoket.OnConnect += OnConnect;
        YScoket.OnData += OnData;
        YScoket.OnDisconnect += OnDisConnect;
        
        YScoket.Start();
        
        // 这样YScoket便运行起来了
        // Send 方法 (Send会自动在头部加上数据长度):
        // byte[] data = blabal... ;
        // YScoket.Send(data);
        
        
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
        // YScoket.Close();
        // YScoket.Start();
        
        Console.WriteLine("DisConnect: {0}", reason);
    }
    
}

```
