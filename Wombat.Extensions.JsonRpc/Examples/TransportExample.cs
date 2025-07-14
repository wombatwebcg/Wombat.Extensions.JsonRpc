using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Examples
{
    /// <summary>
    /// 传输层使用示例
    /// </summary>
    public class TransportExample
    {
        /// <summary>
        /// 主示例方法
        /// </summary>
        public static async Task RunAsync()
        {
            Console.WriteLine("=== 传输层使用示例 ===");
            
            // 1. TCP传输示例
            await TcpTransportExample();
            
            // 2. Named Pipe传输示例
            await NamedPipeTransportExample();
            
            // 3. 传输工厂示例
            await TransportFactoryExample();
            
            // 4. 混合传输示例
            await MixedTransportExample();
            
            Console.WriteLine("=== 示例完成 ===");
        }

        /// <summary>
        ///TCP传输示例
        /// </summary>
        private static async Task TcpTransportExample()
        {
            Console.WriteLine("\n--- TCP传输示例 ---");
            
            const int port = 8080;
            
            // 启动TCP服务器
            var serverTask = Task.Run(async () =>
            {
                var factory = new TransportFactory();
                var server = factory.CreateTcpServer(port);
                
                Console.WriteLine($"TCP服务器启动，监听端口: {port}");
                
                server.ClientConnected += (sender, e) =>
                {
                    Console.WriteLine($"客户端连接: {e.Transport.RemoteEndPoint}");
                    
                    // 为每个客户端创建JsonRpc实例
                    var jsonRpc = new JsonRpc(e.Transport);
                    jsonRpc.AddLocalRpcMethod("Echo", (string message) => $"服务器回复: {message}");
                    jsonRpc.StartListening();
                };
                
                await server.StartAsync();
                
                // 运行5秒后停止
                await Task.Delay(5000);
                await server.StopAsync();
            });
            
            // 等待服务器启动
            await Task.Delay(1000);
            
            // 启动TCP客户端
            var clientTask = Task.Run(async () =>
            {
                var factory = new TransportFactory();
                var client = factory.CreateTcpClient("localhost", port);
                
                await client.ConnectAsync();
                Console.WriteLine($"TCP客户端连接到: {client.RemoteEndPoint}");
                
                var jsonRpc = new JsonRpc(client);
                jsonRpc.StartListening();
                
                try
                {
                    var response = await jsonRpc.InvokeAsync<string>("Echo", "Hello from TCP client");
                    Console.WriteLine($"收到响应: {response}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RPC调用失败: {ex.Message}");
                }
                finally
                {
                    jsonRpc.Dispose();
                    client.Dispose();
                }
            });
            
            await Task.WhenAll(serverTask, clientTask);
        }

        /// <summary>
        ///Named Pipe传输示例
        /// </summary>
        private static async Task NamedPipeTransportExample()
        {
            Console.WriteLine("\n--- Named Pipe传输示例 ---");
            
            const string pipeName = "TestPipe";
            
            // 启动Named Pipe服务器
            var serverTask = Task.Run(async () =>
            {
                var factory = new TransportFactory();
                var server = factory.CreateNamedPipeServer(pipeName);
                
                Console.WriteLine($"Named Pipe服务器启动，管道名称: {pipeName}");
                
                server.ClientConnected += (sender, e) =>
                {
                    Console.WriteLine($"客户端连接到Named Pipe");
                    
                    // 为每个客户端创建JsonRpc实例
                    var jsonRpc = new JsonRpc(e.Transport);
                    jsonRpc.AddLocalRpcMethod("GetTime", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    jsonRpc.AddLocalRpcMethod("Calculate", (int a, int b, string operation) =>
                    {
                        return operation switch
                        {
                            "add" => a + b,
                            "subtract" => a - b,
                            "multiply" => a * b,
                            "divide" => b != 0 ? a / b : throw new ArgumentException("除数不能为零"),
                            _ => throw new NotSupportedException($"不支持的操作: {operation}")
                        };
                    });
                    jsonRpc.StartListening();
                };
                
                await server.StartAsync();
                
                // 运行5秒后停止
                await Task.Delay(5000);
                await server.StopAsync();
            });
            
            // 等待服务器启动
            await Task.Delay(1000);
            
            // 启动Named Pipe客户端
            var clientTask = Task.Run(async () =>
            {
                var factory = new TransportFactory();
                var client = factory.CreateNamedPipeClient(".", pipeName);
                
                await client.ConnectAsync();
                Console.WriteLine($"Named Pipe客户端连接成功");
                
                var jsonRpc = new JsonRpc(client);
                jsonRpc.StartListening();
                
                try
                {
                    var time = await jsonRpc.InvokeAsync<string>("GetTime");
                    Console.WriteLine($"服务器时间: {time}");
                    
                    var result = await jsonRpc.InvokeAsync<int>("Calculate", 10, 5, "add");
                    Console.WriteLine($"计算结果: 10 + 5 = {result}");
                    
                    var divResult = await jsonRpc.InvokeAsync<int>("Calculate", 20, 4, "divide");
                    Console.WriteLine($"计算结果: 20 / 4 = {divResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RPC调用失败: {ex.Message}");
                }
                finally
                {
                    jsonRpc.Dispose();
                    client.Dispose();
                }
            });
            
            await Task.WhenAll(serverTask, clientTask);
        }

        /// <summary>
        /// 传输工厂示例
        /// </summary>
        private static async Task TransportFactoryExample()
        {
            Console.WriteLine("\n--- 传输工厂示例 ---");
            
            // 配置依赖注入
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<ITransportFactory, TransportFactory>();
            
            var serviceProvider = services.BuildServiceProvider();
            var factory = serviceProvider.GetRequiredService<ITransportFactory>();
            
            // 创建不同类型的传输配置
            var tcpConfig = TransportConfiguration.CreateTcp("localhost", 8081);
            var namedPipeConfig = TransportConfiguration.CreateNamedPipe(".", "TestPipe2");
            
            // 使用工厂创建传输实例
            var tcpTransport = factory.CreateTransport(tcpConfig);
            var namedPipeTransport = factory.CreateTransport(namedPipeConfig);
            
            Console.WriteLine($"TCP传输: {tcpTransport.TransportType} -> {tcpTransport.RemoteEndPoint}");
            Console.WriteLine($"Named Pipe传输: {namedPipeTransport.TransportType} -> {namedPipeTransport.RemoteEndPoint}");
            
            // 批量创建传输
            var configs = new[] { tcpConfig, namedPipeConfig };
            var transports = await factory.CreateTransportsAsync(configs);
            
            Console.WriteLine($"批量创建了 {transports.Count} 个传输实例");
            
            // 清理资源
            foreach (var transport in transports)
            {
                transport.Dispose();
            }
        }

        /// <summary>
        /// 混合传输示例
        /// </summary>
        private static async Task MixedTransportExample()
        {
            Console.WriteLine("\n--- 混合传输示例 ---");
            
            // 创建传输工厂
            var factory = new TransportFactory();
            
            // 创建多种传输的配置
            var tcpOptions = new TcpTransportOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                KeepAlive = true,
                NoDelay = true
            };
            
            var namedPipeOptions = new NamedPipeTransportOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(3),
                MaxServerInstances = 5
            };
            
            // 创建传输实例
            var tcpTransport = factory.CreateTcpClient("localhost", 8082, tcpOptions);
            var namedPipeTransport = factory.CreateNamedPipeClient(".", "TestPipe3", namedPipeOptions);
            
            // 展示传输统计信息
            Console.WriteLine($"TCP传输统计:");
            var tcpStats = tcpTransport.GetStatistics();
            Console.WriteLine($"  连接状态: {tcpTransport.IsConnected}");
            Console.WriteLine($"  错误次数: {tcpStats.ErrorCount}");
            
            Console.WriteLine($"Named Pipe传输统计:");
            var pipeStats = namedPipeTransport.GetStatistics();
            Console.WriteLine($"  连接状态: {namedPipeTransport.IsConnected}");
            Console.WriteLine($"  错误次数: {pipeStats.ErrorCount}");
            
            // 模拟连接事件处理
            tcpTransport.Connected += (sender, e) =>
            {
                Console.WriteLine($"TCP连接已建立: {e.Message}");
            };
            
            tcpTransport.Disconnected += (sender, e) =>
            {
                Console.WriteLine($"TCP连接已断开: {e.Message}");
            };
            
            tcpTransport.Error += (sender, e) =>
            {
                Console.WriteLine($"TCP连接错误: {e.Exception.Message}");
            };
            
            namedPipeTransport.Connected += (sender, e) =>
            {
                Console.WriteLine($"Named Pipe连接已建立: {e.Message}");
            };
            
            namedPipeTransport.Disconnected += (sender, e) =>
            {
                Console.WriteLine($"Named Pipe连接已断开: {e.Message}");
            };
            
            namedPipeTransport.Error += (sender, e) =>
            {
                Console.WriteLine($"Named Pipe连接错误: {e.Exception.Message}");
            };
            
            // 清理资源
            tcpTransport.Dispose();
            namedPipeTransport.Dispose();
        }
    }

    /// <summary>
    /// 传输性能测试示例
    /// </summary>
    public class TransportPerformanceTest
    {
        /// <summary>
        /// 运行性能测试
        /// </summary>
        public static async Task RunPerformanceTestAsync()
        {
            Console.WriteLine("\n=== 传输性能测试 ===");
            
            // TCP性能测试
            await TestTcpPerformance();
            
            // Named Pipe性能测试
            await TestNamedPipePerformance();
        }

        /// <summary>
        ///TCP性能测试
        /// </summary>
        private static async Task TestTcpPerformance()
        {
            Console.WriteLine("\n--- TCP性能测试 ---");
            
            const int port = 8083;
            const int messageCount = 1000;
            
            var factory = new TransportFactory();
            var server = factory.CreateTcpServer(port);
            
            // 启动服务器
            server.ClientConnected += (sender, e) =>
            {
                var jsonRpc = new JsonRpc(e.Transport);
                jsonRpc.AddLocalRpcMethod("Process", (string data) => $"Processed: {data}");
                jsonRpc.StartListening();
            };
            
            await server.StartAsync();
            
            // 性能测试
            var client = factory.CreateTcpClient("localhost", port);
            await client.ConnectAsync();
            
            var jsonRpc = new JsonRpc(client);
            jsonRpc.StartListening();
            
            var startTime = DateTime.Now;
            
            for (int i = 0; i < messageCount; i++)
            {
                await jsonRpc.InvokeAsync<string>("Process", $"Message {i}");
            }
            
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            
            Console.WriteLine($"TCP性能测试结果:");
            Console.WriteLine($"  消息数量: {messageCount}");
            Console.WriteLine($"  总时间: {duration.TotalMilliseconds:F2} ms");
            Console.WriteLine($"  平均延迟: {duration.TotalMilliseconds / messageCount:F2} ms/msg");
            Console.WriteLine($"  吞吐量: {messageCount / duration.TotalSeconds:F2} msg/s");
            
            // 获取统计信息
            var stats = client.GetStatistics();
            Console.WriteLine($"  发送字节数: {stats.BytesSent}");
            Console.WriteLine($"  接收字节数: {stats.BytesReceived}");
            Console.WriteLine($"  发送消息数: {stats.MessagesSent}");
            Console.WriteLine($"  接收消息数: {stats.MessagesReceived}");
            
            // 清理资源
            jsonRpc.Dispose();
            client.Dispose();
            await server.StopAsync();
        }

        /// <summary>
        ///Named Pipe性能测试
        /// </summary>
        private static async Task TestNamedPipePerformance()
        {
            Console.WriteLine("\n--- Named Pipe性能测试 ---");
            
            const string pipeName = "PerformanceTestPipe";
            const int messageCount = 1000;
            
            var factory = new TransportFactory();
            var server = factory.CreateNamedPipeServer(pipeName);
            
            // 启动服务器
            server.ClientConnected += (sender, e) =>
            {
                var jsonRpc = new JsonRpc(e.Transport);
                jsonRpc.AddLocalRpcMethod("Process", (string data) => $"Processed: {data}");
                jsonRpc.StartListening();
            };
            
            await server.StartAsync();
            
            // 等待服务器启动
            await Task.Delay(100);
            
            // 性能测试
            var client = factory.CreateNamedPipeClient(".", pipeName);
            await client.ConnectAsync();
            
            var jsonRpc = new JsonRpc(client);
            jsonRpc.StartListening();
            
            var startTime = DateTime.Now;
            
            for (int i = 0; i < messageCount; i++)
            {
                await jsonRpc.InvokeAsync<string>("Process", $"Message {i}");
            }
            
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            
            Console.WriteLine($"Named Pipe性能测试结果:");
            Console.WriteLine($"  消息数量: {messageCount}");
            Console.WriteLine($"  总时间: {duration.TotalMilliseconds:F2} ms");
            Console.WriteLine($"  平均延迟: {duration.TotalMilliseconds / messageCount:F2} ms/msg");
            Console.WriteLine($"  吞吐量: {messageCount / duration.TotalSeconds:F2} msg/s");
            
            // 获取统计信息
            var stats = client.GetStatistics();
            Console.WriteLine($"  发送字节数: {stats.BytesSent}");
            Console.WriteLine($"  接收字节数: {stats.BytesReceived}");
            Console.WriteLine($"  发送消息数: {stats.MessagesSent}");
            Console.WriteLine($"  接收消息数: {stats.MessagesReceived}");
            
            // 清理资源
            jsonRpc.Dispose();
            client.Dispose();
            await server.StopAsync();
        }
    }
} 