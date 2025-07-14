using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Core.Client.Pool;
using Wombat.Extensions.JsonRpc.Core.Client.Proxy;
using Wombat.Extensions.JsonRpc.Core.Contracts;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Examples
{
    /// <summary>
    /// 智能客户端代理示例
    /// 展示强类型代理生成、连接池管理、负载均衡等功能
    /// </summary>
    public class SmartClientProxyExample
    {
        private readonly ILogger<SmartClientProxyExample> _logger;

        public SmartClientProxyExample(ILogger<SmartClientProxyExample> logger = null)
        {
            _logger = logger ?? CreateLogger();
        }

        /// <summary>
        /// 运行示例
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("=== 智能客户端代理示例 ===");
            Console.WriteLine();

            // 1. 基本代理创建和使用
            await BasicProxyExample();
            Console.WriteLine();

            // 2. 连接池管理示例
            await ConnectionPoolExample();
            Console.WriteLine();

            // 3. 高级代理配置示例
            await AdvancedProxyExample();
            Console.WriteLine();

            // 4. 批量操作示例
            await BatchOperationExample();
            Console.WriteLine();

            // 5. 错误处理和重试示例
            await ErrorHandlingExample();
            Console.WriteLine();

            Console.WriteLine("智能客户端代理示例完成！");
        }

        /// <summary>
        /// 基本代理创建和使用示例
        /// </summary>
        private async Task BasicProxyExample()
        {
            Console.WriteLine("--- 基本代理创建和使用示例 ---");

            try
            {
                // 创建连接池
                var poolOptions = ConnectionPoolOptions.Default;
                var connectionPool = new ConnectionPool(poolOptions, _logger?.CreateLogger<ConnectionPool>());

                // 创建代理工厂
                var metadataProvider = new DefaultRpcMetadataProvider();
                var proxyGenerator = new RpcProxyGenerator(metadataProvider, _logger?.CreateLogger<RpcProxyGenerator>());
                var proxyFactory = new RpcProxyFactory(proxyGenerator, _logger?.CreateLogger<RpcProxyFactory>());

                // 创建RPC客户端
                var endpoint = ConnectionEndpoint.CreateTcp("localhost", 8080);
                var client = new SmartRpcClient(endpoint, connectionPool, _logger?.CreateLogger<SmartRpcClient>());

                // 创建强类型代理
                var calculatorProxy = proxyFactory.CreateProxy<ICalculatorService>(client);

                // 使用代理调用方法
                var result1 = await calculatorProxy.AddAsync(10, 20);
                Console.WriteLine($"10 + 20 = {result1}");

                var result2 = await calculatorProxy.MultiplyAsync(5, 6);
                Console.WriteLine($"5 * 6 = {result2}");

                var result3 = await calculatorProxy.DivideAsync(100, 4);
                Console.WriteLine($"100 / 4 = {result3}");

                // 发送通知
                await calculatorProxy.LogAsync("基本代理操作完成");

                // 清理资源
                client.Dispose();
                connectionPool.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"基本代理示例失败: {ex.Message}");
                _logger?.LogError(ex, "基本代理示例失败");
            }
        }

        /// <summary>
        /// 连接池管理示例
        /// </summary>
        private async Task ConnectionPoolExample()
        {
            Console.WriteLine("--- 连接池管理示例 ---");

            try
            {
                // 创建高性能连接池配置
                var poolOptions = ConnectionPoolOptions.HighPerformance;
                poolOptions.MaxConnections = 50;
                poolOptions.MaxConnectionsPerEndpoint = 10;
                poolOptions.EnableStatistics = true;

                var connectionPool = new ConnectionPool(poolOptions, _logger?.CreateLogger<ConnectionPool>());

                // 订阅连接池事件
                connectionPool.ConnectionCreated += (sender, e) =>
                {
                    Console.WriteLine($"连接已创建: {e.Connection.ConnectionId} -> {e.Endpoint}");
                };

                connectionPool.ConnectionDestroyed += (sender, e) =>
                {
                    Console.WriteLine($"连接已销毁: {e.Connection.ConnectionId} -> {e.Endpoint}");
                };

                // 创建多个端点
                var endpoints = new[]
                {
                    ConnectionEndpoint.CreateTcp("localhost", 8080),
                    ConnectionEndpoint.CreateTcp("localhost", 8081),
                    ConnectionEndpoint.CreateTcp("localhost", 8082)
                };

                // 创建多个客户端
                var clients = new SmartRpcClient[endpoints.Length];
                var proxies = new ICalculatorService[endpoints.Length];

                var metadataProvider = new DefaultRpcMetadataProvider();
                var proxyGenerator = new RpcProxyGenerator(metadataProvider, _logger?.CreateLogger<RpcProxyGenerator>());
                var proxyFactory = new RpcProxyFactory(proxyGenerator, _logger?.CreateLogger<RpcProxyFactory>());

                for (int i = 0; i < endpoints.Length; i++)
                {
                    clients[i] = new SmartRpcClient(endpoints[i], connectionPool, _logger?.CreateLogger<SmartRpcClient>());
                    proxies[i] = proxyFactory.CreateProxy<ICalculatorService>(clients[i]);
                }

                // 并发调用
                var tasks = new Task[endpoints.Length * 5];
                for (int i = 0; i < endpoints.Length; i++)
                {
                    var proxy = proxies[i];
                    var endpointIndex = i;
                    
                    for (int j = 0; j < 5; j++)
                    {
                        var taskIndex = i * 5 + j;
                        tasks[taskIndex] = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await proxy.AddAsync(taskIndex, endpointIndex);
                                Console.WriteLine($"端点 {endpointIndex}, 任务 {taskIndex}: {taskIndex} + {endpointIndex} = {result}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"任务失败: {ex.Message}");
                            }
                        });
                    }
                }

                await Task.WhenAll(tasks);

                // 显示连接池统计信息
                var statistics = connectionPool.Statistics.GetSummary();
                Console.WriteLine($"连接池统计:");
                Console.WriteLine($"  总连接数: {statistics.TotalConnections}");
                Console.WriteLine($"  活动连接数: {statistics.ActiveConnections}");
                Console.WriteLine($"  空闲连接数: {statistics.IdleConnections}");
                Console.WriteLine($"  已创建连接总数: {statistics.TotalConnectionsCreated}");
                Console.WriteLine($"  已关闭连接总数: {statistics.TotalConnectionsClosed}");
                Console.WriteLine($"  平均获取时间: {statistics.AverageConnectionAcquisitionTimeMs:F2}ms");

                // 清理资源
                foreach (var client in clients)
                {
                    client.Dispose();
                }
                connectionPool.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接池示例失败: {ex.Message}");
                _logger?.LogError(ex, "连接池示例失败");
            }
        }

        /// <summary>
        /// 高级代理配置示例
        /// </summary>
        private async Task AdvancedProxyExample()
        {
            Console.WriteLine("--- 高级代理配置示例 ---");

            try
            {
                // 创建开发调试配置
                var poolOptions = ConnectionPoolOptions.Development;
                var connectionPool = new ConnectionPool(poolOptions, _logger?.CreateLogger<ConnectionPool>());

                // 创建高级代理配置
                var proxyOptions = new ProxyGenerationOptions
                {
                    EnableParameterValidation = true,
                    EnableExceptionWrapping = true,
                    EnableLogging = true,
                    EnablePerformanceMonitoring = true,
                    EnableRetry = true,
                    DefaultTimeoutMs = 10000,
                    RetryPolicy = new RetryPolicy
                    {
                        MaxRetries = 3,
                        RetryIntervalMs = 1000,
                        UseExponentialBackoff = true
                    },
                    CachePolicy = new CachePolicy
                    {
                        EnableCache = true,
                        CacheTtlMs = 30000,
                        MaxCacheSize = 100
                    }
                };

                var metadataProvider = new DefaultRpcMetadataProvider();
                var proxyGenerator = new RpcProxyGenerator(metadataProvider, _logger?.CreateLogger<RpcProxyGenerator>());
                var proxyFactory = new RpcProxyFactory(proxyGenerator, _logger?.CreateLogger<RpcProxyFactory>());

                var endpoint = ConnectionEndpoint.CreateTcp("localhost", 8080);
                var client = new SmartRpcClient(endpoint, connectionPool, _logger?.CreateLogger<SmartRpcClient>());

                // 创建带高级配置的代理
                var calculatorProxy = proxyFactory.CreateProxy<ICalculatorService>(client, proxyOptions);

                // 测试参数验证
                try
                {
                    await calculatorProxy.DivideAsync(10, 0); // 会触发参数验证
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"参数验证异常: {ex.Message}");
                }

                // 测试缓存
                var start = DateTime.Now;
                var result1 = await calculatorProxy.AddAsync(100, 200);
                var time1 = DateTime.Now - start;
                Console.WriteLine($"第一次调用: {result1}, 耗时: {time1.TotalMilliseconds}ms");

                start = DateTime.Now;
                var result2 = await calculatorProxy.AddAsync(100, 200); // 应该从缓存返回
                var time2 = DateTime.Now - start;
                Console.WriteLine($"第二次调用: {result2}, 耗时: {time2.TotalMilliseconds}ms");

                // 获取服务元数据
                var metadata = await proxyFactory.GetServiceMetadataAsync<ICalculatorService>();
                Console.WriteLine($"服务元数据:");
                Console.WriteLine($"  服务名: {metadata.ServiceName}");
                Console.WriteLine($"  方法数: {metadata.Methods?.Length ?? 0}");
                foreach (var method in metadata.Methods ?? new MethodMetadata[0])
                {
                    Console.WriteLine($"  方法: {method.MethodName}, 需要认证: {method.RequireAuthentication}");
                }

                // 清理资源
                client.Dispose();
                connectionPool.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"高级代理示例失败: {ex.Message}");
                _logger?.LogError(ex, "高级代理示例失败");
            }
        }

        /// <summary>
        /// 批量操作示例
        /// </summary>
        private async Task BatchOperationExample()
        {
            Console.WriteLine("--- 批量操作示例 ---");

            try
            {
                var poolOptions = ConnectionPoolOptions.HighPerformance;
                var connectionPool = new ConnectionPool(poolOptions, _logger?.CreateLogger<ConnectionPool>());

                var metadataProvider = new DefaultRpcMetadataProvider();
                var proxyGenerator = new RpcProxyGenerator(metadataProvider, _logger?.CreateLogger<RpcProxyGenerator>());
                var proxyFactory = new RpcProxyFactory(proxyGenerator, _logger?.CreateLogger<RpcProxyFactory>());

                var endpoint = ConnectionEndpoint.CreateTcp("localhost", 8080);
                var client = new SmartRpcClient(endpoint, connectionPool, _logger?.CreateLogger<SmartRpcClient>());

                var calculatorProxy = proxyFactory.CreateProxy<ICalculatorService>(client);

                // 批量并发调用
                var batchSize = 100;
                var tasks = new Task<int>[batchSize];
                
                var startTime = DateTime.Now;
                
                for (int i = 0; i < batchSize; i++)
                {
                    var index = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        return await calculatorProxy.AddAsync(index, index * 2);
                    });
                }

                var results = await Task.WhenAll(tasks);
                var endTime = DateTime.Now;
                var totalTime = endTime - startTime;

                Console.WriteLine($"批量操作完成:");
                Console.WriteLine($"  操作数量: {batchSize}");
                Console.WriteLine($"  总耗时: {totalTime.TotalMilliseconds}ms");
                Console.WriteLine($"  平均耗时: {totalTime.TotalMilliseconds / batchSize:F2}ms");
                Console.WriteLine($"  吞吐量: {batchSize / totalTime.TotalSeconds:F2} ops/sec");

                // 显示部分结果
                for (int i = 0; i < Math.Min(10, results.Length); i++)
                {
                    Console.WriteLine($"  结果 {i}: {i} + {i * 2} = {results[i]}");
                }

                // 清理资源
                client.Dispose();
                connectionPool.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量操作示例失败: {ex.Message}");
                _logger?.LogError(ex, "批量操作示例失败");
            }
        }

        /// <summary>
        /// 错误处理和重试示例
        /// </summary>
        private async Task ErrorHandlingExample()
        {
            Console.WriteLine("--- 错误处理和重试示例 ---");

            try
            {
                var poolOptions = ConnectionPoolOptions.Reliability;
                var connectionPool = new ConnectionPool(poolOptions, _logger?.CreateLogger<ConnectionPool>());

                var proxyOptions = new ProxyGenerationOptions
                {
                    EnableRetry = true,
                    RetryPolicy = new RetryPolicy
                    {
                        MaxRetries = 5,
                        RetryIntervalMs = 500,
                        UseExponentialBackoff = true,
                        BackoffMultiplier = 2.0
                    }
                };

                var metadataProvider = new DefaultRpcMetadataProvider();
                var proxyGenerator = new RpcProxyGenerator(metadataProvider, _logger?.CreateLogger<RpcProxyGenerator>());
                var proxyFactory = new RpcProxyFactory(proxyGenerator, _logger?.CreateLogger<RpcProxyFactory>());

                // 尝试连接到不存在的端点
                var endpoint = ConnectionEndpoint.CreateTcp("localhost", 9999);
                var client = new SmartRpcClient(endpoint, connectionPool, _logger?.CreateLogger<SmartRpcClient>());

                var calculatorProxy = proxyFactory.CreateProxy<ICalculatorService>(client, proxyOptions);

                try
                {
                    // 这会失败并触发重试
                    var result = await calculatorProxy.AddAsync(1, 2);
                    Console.WriteLine($"不应该到达这里: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"预期的连接错误: {ex.Message}");
                }

                // 测试超时处理
                var validEndpoint = ConnectionEndpoint.CreateTcp("localhost", 8080);
                var validClient = new SmartRpcClient(validEndpoint, connectionPool, _logger?.CreateLogger<SmartRpcClient>());
                var validProxy = proxyFactory.CreateProxy<ICalculatorService>(validClient, proxyOptions);

                try
                {
                    // 设置很短的超时时间
                    proxyOptions.DefaultTimeoutMs = 1;
                    var timeoutProxy = proxyFactory.CreateProxy<ICalculatorService>(validClient, proxyOptions);
                    
                    var result = await timeoutProxy.AddAsync(1, 2);
                    Console.WriteLine($"超时测试结果: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"超时异常: {ex.Message}");
                }

                // 清理资源
                client.Dispose();
                validClient.Dispose();
                connectionPool.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误处理示例失败: {ex.Message}");
                _logger?.LogError(ex, "错误处理示例失败");
            }
        }

        /// <summary>
        /// 创建日志记录器
        /// </summary>
        /// <returns>日志记录器</returns>
        private ILogger<SmartClientProxyExample> CreateLogger()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Information);
            });
            
            return loggerFactory.CreateLogger<SmartClientProxyExample>();
        }
    }

    /// <summary>
    /// 智能RPC客户端（示例实现）
    /// </summary>
    public class SmartRpcClient : IRpcClient
    {
        private readonly ConnectionEndpoint _endpoint;
        private readonly IConnectionPool _connectionPool;
        private readonly ILogger<SmartRpcClient> _logger;
        private volatile bool _disposed;

        public SmartRpcClient(
            ConnectionEndpoint endpoint,
            IConnectionPool connectionPool,
            ILogger<SmartRpcClient> logger = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _logger = logger;
        }

        public bool IsConnected => !_disposed;

        public async Task<TResult> InvokeAsync<TResult>(string methodName, params object[] args)
        {
            var connection = await _connectionPool.GetConnectionAsync(_endpoint);
            try
            {
                using var lease = await connection.AcquireAsync();
                
                // 这里应该实现实际的RPC调用
                // 为了示例，我们模拟一个简单的计算
                if (methodName == "Add" && args.Length == 2)
                {
                    var a = Convert.ToInt32(args[0]);
                    var b = Convert.ToInt32(args[1]);
                    return (TResult)(object)(a + b);
                }
                else if (methodName == "Multiply" && args.Length == 2)
                {
                    var a = Convert.ToInt32(args[0]);
                    var b = Convert.ToInt32(args[1]);
                    return (TResult)(object)(a * b);
                }
                else if (methodName == "Divide" && args.Length == 2)
                {
                    var a = Convert.ToDouble(args[0]);
                    var b = Convert.ToDouble(args[1]);
                    if (b == 0)
                        throw new DivideByZeroException("除数不能为零");
                    return (TResult)(object)(a / b);
                }
                
                return default(TResult);
            }
            finally
            {
                await _connectionPool.ReleaseConnectionAsync(connection);
            }
        }

        public async Task InvokeAsync(string methodName, params object[] args)
        {
            await InvokeAsync<object>(methodName, args);
        }

        public async Task NotifyAsync(string methodName, params object[] args)
        {
            var connection = await _connectionPool.GetConnectionAsync(_endpoint);
            try
            {
                using var lease = await connection.AcquireAsync();
                
                // 这里应该实现实际的通知发送
                _logger?.LogInformation("发送通知: {MethodName}, 参数: {Args}", 
                    methodName, string.Join(", ", args));
            }
            finally
            {
                await _connectionPool.ReleaseConnectionAsync(connection);
            }
        }

        public async Task ConnectAsync()
        {
            // 连接池会自动管理连接
            await Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            // 连接池会自动管理连接
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// 计算器服务接口（示例）
    /// </summary>
    [RpcMethod]
    public interface ICalculatorService
    {
        [RpcMethod("Add")]
        Task<int> AddAsync(int a, int b);

        [RpcMethod("Multiply")]
        Task<int> MultiplyAsync(int a, int b);

        [RpcMethod("Divide")]
        Task<double> DivideAsync(double a, double b);

        [RpcMethod("Log", IsNotification = true)]
        Task LogAsync(string message);
    }
} 