✅ 一、目标功能列表（MVP 版本）
🧩 核心功能：
功能	描述
✅ 方法特性暴露	用 [RpcMethod] 标注要暴露的类或方法
✅ 自动注册对象	自动扫描注册所有打了 [RpcMethod] 的方法
✅ 支持多种传输层	TCP、WebSocket、HTTP、NamedPipe 四种协议
✅ 客户端代理支持	根据服务端元数据动态调用 RPC
✅ 请求/响应日志	请求/响应拦截与日志功能
✅ 异常处理封装	包装异常返回统一格式

🏗️ 二、项目结构设计
1. 传输层模块（Transport 层）

Transports/
├── ITwoWayChannel.cs         # 定义统一双向通信接口
├── TcpTransport.cs           # 封装 TCP 通信
├── WebSocketTransport.cs     # 基于 WebSocket 封装
├── NamedPipeTransport.cs     # 支持本地管道
├── HttpTransport.cs          # 可选，用 SignalR 或长连接实现
统一使用接口：


public interface ITwoWayChannel : IDisposable
{
    Stream InputStream { get; }
    Stream OutputStream { get; }
}
2. RPC 框架层
RpcCore/
├── RpcServer.cs             # 启动、注册、监听
├── RpcClient.cs             # 连接、调用
├── RpcTargetBuilder.cs      # 提取目标对象中打了特性的 RPC 方法
├── RpcMethodAttribute.cs    # 自定义特性
🧠 三、运行时示例
注册服务端：

var transport = new TcpTransport(port: 12345);
var server = new RpcServer(transport);

server.RegisterService(new DeviceService()); // 自动识别 [RpcMethod]
server.Start();
客户端调用：

var client = new RpcClient(new TcpTransport("127.0.0.1", 12345));
var result = await client.InvokeAsync<int>("Add", 1, 2);
🛠️ 四、进阶功能规划（v1.1+）
功能	描述
✅ 参数校验特性	[RpcParamNotNull] 等参数验证装饰器
✅ 客户端接口生成器	自动从服务端生成调用接口（代码生成或动态代理）
✅ 加密通信	TLS、Token、身份验证
✅ JSON-RPC 拦截器	预处理、后处理、统一日志
✅ 服务发现	支持多个服务实例（可选 Zeroconf/Consul）
✅ Stream 传输	支持大文件上传（Stream 参数支持）

🔧 五、推荐技术栈
模块	技术
RPC 核心	StreamJsonRpc（官方库）
WS 支持	System.Net.WebSockets
HTTP	ASP.NET Core or HttpListener
命名管道	NamedPipeServerStream / NamedPipeClientStream
特性反射	System.Reflection, 可拓展到 Source Generator
客户端生成	System.Text.Json + ExpandoObject 或代码生成
日志	Microsoft.Extensions.Logging