using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Core.Builder;
using Wombat.Extensions.JsonRpc.Core.Client;
using Wombat.Extensions.JsonRpc.Core.Contracts;
using Wombat.Extensions.JsonRpc.Core.Server;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Examples
{
    /// <summary>
    /// RPC服务器客户端完整示例
    /// </summary>
    public class RpcServerClientExample
    {
        /// <summary>
        /// 主示例方法
        /// </summary>
        public static async Task RunAsync()
        {
            Console.WriteLine("=== RPC服务器客户端完整示例 ===");
            
            // 1. 基本服务器客户端示例
            await BasicServerClientExample();
            
            // 2. 多传输层示例
            await MultiTransportExample();
            
            // 3. 企业级功能示例
            await EnterpriseFeatureExample();
            
            // 4. 性能测试示例
            await PerformanceTestExample();
            
            Console.WriteLine("=== 示例完成 ===");
        }

        /// <summary>
        /// 基本服务器客户端示例
        /// </summary>
        private static async Task BasicServerClientExample()
        {
            Console.WriteLine("\n--- 基本服务器客户端示例 ---");
            
            // 配置依赖注入
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<ITransportFactory, TransportFactory>();
            services.AddSingleton<RpcTargetBuilderFactory>();
            
            var serviceProvider = services.BuildServiceProvider();
            
            // 创建服务器
            var serverOptions = RpcServerOptions.CreateDefault();
            var server = new RpcServer(serverOptions, serviceProvider);
            
            // 注册服务
            await server.RegisterServiceAsync(new EnhancedCalculatorService());
            await server.RegisterServiceAsync(new BusinessService());
            
            // 启动TCP服务器
            const int port = 8090;
            await server.StartTcpAsync(port);
            
            Console.WriteLine($"RPC服务器已启动，监听端口: {port}");
            Console.WriteLine($"已注册服务数量: {server.GetServiceRegistrations().Count}");
            
            // 等待服务器启动
            await Task.Delay(1000);
            
            // 创建客户端
            var clientOptions = RpcClientOptions.CreateDefault();
            var client = new RpcClient(clientOptions, serviceProvider.GetService<ITransportFactory>(), serviceProvider.GetService<ILogger<RpcClient>>());
            
            // 连接到服务器
            await client.ConnectTcpAsync("localhost", port);
            Console.WriteLine($"客户端已连接到: {client.RemoteEndPoint}");
            
            try
            {
                // 调用计算器服务
                var addResult = await client.InvokeAsync<int>("Add", 15, 25);
                Console.WriteLine($"Add(15, 25) = {addResult}");
                
                var divResult = await client.InvokeAsync<double>("Divide", 100.0, 4.0);
                Console.WriteLine($"Divide(100, 4) = {divResult}");
                
                // 调用业务服务
                var userInfo = await client.InvokeAsync<UserInfo>("GetUserInfo", "user123");
                Console.WriteLine($"用户信息: {userInfo.Name} ({userInfo.Email})");
                
                var orderResult = await client.InvokeAsync<OrderResult>("ProcessOrder", new Order { Id = "ORD001", Amount = 299.99m });
                Console.WriteLine($"订单处理结果: {orderResult.Status} - {orderResult.Message}");
                
                // 发送通知
                await client.NotifyAsync("Log", "客户端测试消息");
                
                // 显示统计信息
                var clientStats = client.Statistics;
                Console.WriteLine($"客户端统计: 请求数={clientStats.TotalRequests}, 成功率={clientStats.SuccessRate:F2}%");
                
                var serverStats = server.Statistics;
                Console.WriteLine($"服务器统计: 连接数={serverStats.ActiveConnections}, 注册服务数={serverStats.RegisteredServices}");
            }
            finally
            {
                // 清理资源
                await client.DisconnectAsync();
                await server.StopAsync();
                client.Dispose();
                server.Dispose();
            }
        }

        /// <summary>
        /// 多传输层示例
        /// </summary>
        private static async Task MultiTransportExample()
        {
            Console.WriteLine("\n--- 多传输层示例 ---");
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var serviceProvider = services.BuildServiceProvider();
            
            // 创建服务器，支持多种传输
            var server = new RpcServer(RpcServerOptions.CreateDefault(), serviceProvider);
            await server.RegisterServiceAsync(new EnhancedCalculatorService());
            
            // 启动多种传输服务器
            var configs = new[]
            {
                TransportServerConfig.CreateTcp(8091, name: "主TCP服务器"),
                TransportServerConfig.CreateNamedPipe("MultiTransportPipe", name: "管道服务器")
            };
            
            await server.StartMultipleAsync(configs);
            
            Console.WriteLine("多传输RPC服务器已启动:");
            Console.WriteLine("  - TCP: localhost:8091");
            Console.WriteLine("  - Named Pipe: MultiTransportPipe");
            
            await Task.Delay(1000);
            
            // 测试TCP客户端
            var tcpClient = new RpcClient();
            await tcpClient.ConnectTcpAsync("localhost", 8091);
            
            var tcpResult = await tcpClient.InvokeAsync<int>("Add", 10, 20);
            Console.WriteLine($"TCP客户端调用结果: Add(10, 20) = {tcpResult}");
            
            // 测试Named Pipe客户端
            var pipeClient = new RpcClient();
            await pipeClient.ConnectNamedPipeAsync(".", "MultiTransportPipe");
            
            var pipeResult = await pipeClient.InvokeAsync<int>("Multiply", 6, 7);
            Console.WriteLine($"Named Pipe客户端调用结果: Multiply(6, 7) = {pipeResult}");
            
            // 清理资源
            await tcpClient.DisconnectAsync();
            await pipeClient.DisconnectAsync();
            await server.StopAsync();
            
            tcpClient.Dispose();
            pipeClient.Dispose();
            server.Dispose();
        }

        /// <summary>
        /// 企业级功能示例
        /// </summary>
        private static async Task EnterpriseFeatureExample()
        {
            Console.WriteLine("\n--- 企业级功能示例 ---");
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var serviceProvider = services.BuildServiceProvider();
            
            // 创建企业级服务器配置
            var serverOptions = new RpcServerOptions
            {
                EnableParameterValidation = true,
                EnableVerboseLogging = true,
                EnablePerformanceMonitoring = true,
                EnableHealthCheck = true,
                MaxConcurrentConnections = 100,
                RequestTimeout = TimeSpan.FromSeconds(30)
            };
            
            var server = new RpcServer(serverOptions, serviceProvider);
            
            // 注册带验证的服务
            await server.RegisterServiceAsync(new ValidatedBusinessService());
            
            // 设置事件处理
            server.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"[事件] 客户端连接: {e.Connection.RemoteEndPoint} ({e.Connection.TransportType})");
            };
            
            server.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"[事件] 客户端断开: {e.Connection.RemoteEndPoint}, 原因: {e.Reason}");
            };
            
            server.ServerError += (sender, e) =>
            {
                Console.WriteLine($"[事件] 服务器错误: {e.Message}");
            };
            
            await server.StartTcpAsync(8092);
            Console.WriteLine("企业级RPC服务器已启动 (端口: 8092)");
            
            await Task.Delay(1000);
            
            // 创建可靠的客户端
            var clientOptions = RpcClientOptions.CreateReliable();
            var client = new RpcClient(clientOptions, serviceProvider.GetService<ITransportFactory>(), serviceProvider.GetService<ILogger<RpcClient>>());
            
            // 设置客户端事件
            client.Connected += (sender, e) =>
            {
                Console.WriteLine($"[事件] 客户端已连接到: {e.RemoteEndPoint}");
            };
            
            client.Disconnected += (sender, e) =>
            {
                Console.WriteLine($"[事件] 客户端已断开: {e.Reason}");
            };
            
            client.Error += (sender, e) =>
            {
                Console.WriteLine($"[事件] 客户端错误: {e.Message}");
            };
            
            await client.ConnectTcpAsync("localhost", 8092);
            
            try
            {
                // 测试参数验证
                Console.WriteLine("测试参数验证功能:");
                
                try
                {
                    // 这应该成功
                    var validResult = await client.InvokeAsync<string>("ValidateUser", "john@example.com", 25);
                    Console.WriteLine($"  有效请求结果: {validResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  有效请求异常: {ex.Message}");
                }
                
                try
                {
                    // 这应该失败（无效邮箱）
                    var invalidResult = await client.InvokeAsync<string>("ValidateUser", "invalid-email", 25);
                    Console.WriteLine($"  无效请求结果: {invalidResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  无效请求异常: {ex.Message}");
                }
                
                try
                {
                    // 这应该失败（年龄超出范围）
                    var invalidAgeResult = await client.InvokeAsync<string>("ValidateUser", "test@example.com", 150);
                    Console.WriteLine($"  无效年龄结果: {invalidAgeResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  无效年龄异常: {ex.Message}");
                }
                
                // 测试批量操作
                Console.WriteLine("测试批量操作:");
                var tasks = new Task<int>[10];
                for (int i = 0; i < 10; i++)
                {
                    int index = i;
                    tasks[i] = client.InvokeAsync<int>("GetRandomNumber", index * 10, (index + 1) * 10);
                }
                
                var results = await Task.WhenAll(tasks);
                Console.WriteLine($"  批量操作完成，结果: [{string.Join(", ", results)}]");
                
                // 显示详细统计
                var stats = client.Statistics;
                Console.WriteLine($"客户端详细统计:");
                Console.WriteLine($"  总请求数: {stats.TotalRequests}");
                Console.WriteLine($"  成功请求数: {stats.SuccessfulRequests}");
                Console.WriteLine($"  失败请求数: {stats.FailedRequests}");
                Console.WriteLine($"  平均延迟: {stats.AverageLatency:F2}ms");
                Console.WriteLine($"  成功率: {stats.SuccessRate:F2}%");
                Console.WriteLine($"  连接时长: {stats.ConnectionDuration?.TotalSeconds:F2}秒");
            }
            finally
            {
                await client.DisconnectAsync();
                await server.StopAsync();
                client.Dispose();
                server.Dispose();
            }
        }

        /// <summary>
        /// 性能测试示例
        /// </summary>
        private static async Task PerformanceTestExample()
        {
            Console.WriteLine("\n--- 性能测试示例 ---");
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var serviceProvider = services.BuildServiceProvider();
            
            // 高性能服务器配置
            var serverOptions = RpcServerOptions.CreateHighPerformance();
            var server = new RpcServer(serverOptions, serviceProvider);
            await server.RegisterServiceAsync(new HighPerformanceService());
            
            await server.StartTcpAsync(8093);
            Console.WriteLine("高性能RPC服务器已启动 (端口: 8093)");
            
            await Task.Delay(1000);
            
            // 高性能客户端配置
            var clientOptions = RpcClientOptions.CreateHighPerformance();
            var client = new RpcClient(clientOptions);
            await client.ConnectTcpAsync("localhost", 8093);
            
            // 性能测试参数
            const int requestCount = 1000;
            const int concurrency = 10;
            
            Console.WriteLine($"开始性能测试: {requestCount} 个请求, {concurrency} 个并发");
            
            var startTime = DateTime.UtcNow;
            var tasks = new Task[concurrency];
            
            for (int i = 0; i < concurrency; i++)
            {
                int taskId = i;
                tasks[i] = Task.Run(async () =>
                {
                    int requestsPerTask = requestCount / concurrency;
                    for (int j = 0; j < requestsPerTask; j++)
                    {
                        try
                        {
                            await client.InvokeAsync<int>("FastCalculation", taskId * requestsPerTask + j);
                        }
                        catch
                        {
                            // 忽略错误，专注于性能
                        }
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
            var stats = client.Statistics;
            
            Console.WriteLine($"性能测试结果:");
            Console.WriteLine($"  总时间: {duration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  总请求数: {stats.TotalRequests}");
            Console.WriteLine($"  成功请求数: {stats.SuccessfulRequests}");
            Console.WriteLine($"  失败请求数: {stats.FailedRequests}");
            Console.WriteLine($"  吞吐量: {stats.TotalRequests / duration.TotalSeconds:F2} req/s");
            Console.WriteLine($"  平均延迟: {stats.AverageLatency:F2}ms");
            Console.WriteLine($"  成功率: {stats.SuccessRate:F2}%");
            
            var serverStats = server.Statistics;
            Console.WriteLine($"服务器统计:");
            Console.WriteLine($"  处理请求数: {serverStats.TotalRequests}");
            Console.WriteLine($"  活动连接数: {serverStats.ActiveConnections}");
            Console.WriteLine($"  服务器运行时间: {serverStats.Uptime.TotalSeconds:F2}秒");
            
            await client.DisconnectAsync();
            await server.StopAsync();
            client.Dispose();
            server.Dispose();
        }
    }

    #region 示例服务类

    /// <summary>
    /// 增强版计算器服务
    /// </summary>
    [RpcService("Calculator")]
    public class EnhancedCalculatorService
    {
        [RpcMethod("Add")]
        public async Task<int> AddAsync([RpcParamNotNull] int a, [RpcParamNotNull] int b)
        {
            return await Task.FromResult(a + b);
        }

        [RpcMethod("Subtract")]
        public async Task<int> SubtractAsync(int a, int b)
        {
            return await Task.FromResult(a - b);
        }

        [RpcMethod("Multiply")]
        public async Task<int> MultiplyAsync(int a, int b)
        {
            return await Task.FromResult(a * b);
        }

        [RpcMethod("Divide")]
        public async Task<double> DivideAsync(double numerator, [RpcParamRange(0.001, double.MaxValue)] double denominator)
        {
            if (Math.Abs(denominator) < 0.001)
                throw new ArgumentException("除数不能为零");
            return await Task.FromResult(numerator / denominator);
        }

        [RpcMethod("Log", IsNotification = true)]
        public async Task LogAsync([RpcParamNotNull] string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 业务服务示例
    /// </summary>
    [RpcService("Business")]
    public class BusinessService
    {
        [RpcMethod("GetUserInfo")]
        public async Task<UserInfo> GetUserInfoAsync([RpcParamNotNull] string userId)
        {
            // 模拟数据库查询
            await Task.Delay(50);
            
            return new UserInfo
            {
                Id = userId,
                Name = $"用户_{userId}",
                Email = $"{userId}@example.com",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
        }

        [RpcMethod("ProcessOrder")]
        public async Task<OrderResult> ProcessOrderAsync([RpcParamNotNull] Order order)
        {
            // 模拟订单处理
            await Task.Delay(100);
            
            if (order.Amount <= 0)
            {
                return new OrderResult
                {
                    Success = false,
                    Status = "Failed",
                    Message = "订单金额必须大于0"
                };
            }
            
            return new OrderResult
            {
                Success = true,
                Status = "Processed",
                Message = $"订单 {order.Id} 已成功处理，金额: {order.Amount:C}",
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 带验证的业务服务
    /// </summary>
    [RpcService("ValidatedBusiness")]
    public class ValidatedBusinessService
    {
        [RpcMethod("ValidateUser")]
        public async Task<string> ValidateUserAsync(
            [RpcParamRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")] string email,
            [RpcParamRange(1, 120)] int age)
        {
            await Task.Delay(10);
            return $"用户验证成功: {email}, 年龄: {age}";
        }

        [RpcMethod("GetRandomNumber")]
        public async Task<int> GetRandomNumberAsync(
            [RpcParamRange(0, 1000)] int min,
            [RpcParamRange(0, 1000)] int max)
        {
            if (min >= max)
                throw new ArgumentException("最小值必须小于最大值");
            
            await Task.Delay(5);
            return new Random().Next(min, max);
        }
    }

    /// <summary>
    /// 高性能服务
    /// </summary>
    [RpcService("HighPerformance")]
    public class HighPerformanceService
    {
        [RpcMethod("FastCalculation")]
        public int FastCalculation(int input)
        {
            // 简单快速计算，不使用async
            return input * 2 + 1;
        }

        [RpcMethod("QuickHash")]
        public int QuickHash(string input)
        {
            return input?.GetHashCode() ?? 0;
        }
    }

    #endregion

    #region 数据模型

    public class UserInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }

    public class OrderResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    #endregion
} 