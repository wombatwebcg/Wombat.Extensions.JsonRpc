using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Core.Builder;
using Wombat.Extensions.JsonRpc.Core.Contracts;
using Wombat.Extensions.JsonRpc.Core.Validation;

namespace Wombat.Extensions.JsonRpc.Examples
{
    /// <summary>
    /// RPC目标构建器使用示例
    /// </summary>
    public class RpcTargetBuilderExample
    {
        /// <summary>
        /// 主示例方法
        /// </summary>
        public static async Task RunAsync()
        {
            Console.WriteLine("=== RPC目标构建器使用示例 ===");
            
            // 1. 基本使用示例
            await BasicUsageExample();
            
            // 2. 依赖注入示例
            await DependencyInjectionExample();
            
            // 3. 高性能配置示例
            await HighPerformanceExample();
            
            // 4. 完整的服务器客户端示例
            await ServerClientExample();
            
            Console.WriteLine("=== 示例完成 ===");
        }

        /// <summary>
        /// 基本使用示例
        /// </summary>
        private static async Task BasicUsageExample()
        {
            Console.WriteLine("\n--- 基本使用示例 ---");
            
            // 创建构建器工厂
            var factory = new RpcTargetBuilderFactory();
            
            // 创建标准构建器
            var builder = factory.CreateStandard();
            
            // 自动扫描并注册服务
            var registeredCount = await builder.ScanAndRegisterAsync();
            Console.WriteLine($"自动扫描注册了 {registeredCount} 个RPC服务");
            
            // 手动注册服务
            await builder.RegisterServiceAsync(typeof(CalculatorService));
            
            // 获取服务注册信息
            var registrations = builder.GetServiceRegistrations();
            foreach (var registration in registrations)
            {
                Console.WriteLine($"服务: {registration.ServiceMetadata.ServiceName}");
                Console.WriteLine($"  类型: {registration.ServiceType.Name}");
                Console.WriteLine($"  方法数: {registration.MethodTargets.Count}");
                Console.WriteLine($"  注册时间: {registration.RegisteredAt}");
                
                foreach (var method in registration.MethodTargets.Values)
                {
                    Console.WriteLine($"  - {method.MethodName} (验证: {method.MethodMetadata.Parameters.Count > 0})");
                }
            }
        }

        /// <summary>
        /// 依赖注入示例
        /// </summary>
        private static async Task DependencyInjectionExample()
        {
            Console.WriteLine("\n--- 依赖注入示例 ---");
            
            // 配置依赖注入
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IRpcMetadataProvider, DefaultRpcMetadataProvider>();
            services.AddSingleton<ParameterValidator>();
            services.AddSingleton<RpcMethodInterceptor>();
            services.AddSingleton<CalculatorService>();
            
            var serviceProvider = services.BuildServiceProvider();
            
            // 使用依赖注入创建构建器
            var factory = new RpcTargetBuilderFactory(serviceProvider);
            var builder = factory.CreateStandard();
            
            // 注册服务
            await builder.RegisterServiceAsync(typeof(CalculatorService));
            
            Console.WriteLine("使用依赖注入成功创建并注册了RPC服务");
        }

        /// <summary>
        /// 高性能配置示例
        /// </summary>
        private static async Task HighPerformanceExample()
        {
            Console.WriteLine("\n--- 高性能配置示例 ---");
            
            var factory = new RpcTargetBuilderFactory();
            
            // 创建高性能构建器（禁用验证）
            var highPerfBuilder = factory.CreateHighPerformance();
            
            // 创建最小化构建器
            var minimalBuilder = factory.CreateMinimal();
            
            // 创建自定义配置构建器
            var customConfig = RpcTargetBuilderConfiguration.CreateHighPerformance();
            var customBuilder = factory.CreateWithConfiguration(customConfig);
            
            await highPerfBuilder.RegisterServiceAsync(typeof(CalculatorService));
            await minimalBuilder.RegisterServiceAsync(typeof(CalculatorService));
            await customBuilder.RegisterServiceAsync(typeof(CalculatorService));
            
            Console.WriteLine("创建了多种配置的RPC构建器");
            Console.WriteLine($"  高性能构建器: {highPerfBuilder.GetServiceRegistrations().Count} 个服务");
            Console.WriteLine($"  最小化构建器: {minimalBuilder.GetServiceRegistrations().Count} 个服务");
            Console.WriteLine($"  自定义构建器: {customBuilder.GetServiceRegistrations().Count} 个服务");
        }

        /// <summary>
        /// 完整的服务器客户端示例
        /// </summary>
        private static async Task ServerClientExample()
        {
            Console.WriteLine("\n--- 服务器客户端示例 ---");
            
            const string pipeName = "RpcExample";
            
            // 启动服务器
            var serverTask = Task.Run(async () =>
            {
                using var serverPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
                Console.WriteLine("服务器等待连接...");
                
                await serverPipe.WaitForConnectionAsync();
                Console.WriteLine("客户端已连接");
                
                // 创建JsonRpc服务器
                var jsonRpc = new JsonRpc(serverPipe);
                
                // 创建RPC目标构建器
                var factory = new RpcTargetBuilderFactory();
                var builder = factory.CreateStandard();
                
                // 注册服务
                await builder.RegisterServiceAsync(typeof(CalculatorService));
                
                // 应用到JsonRpc
                builder.ApplyToJsonRpc(jsonRpc);
                
                // 启动服务器
                jsonRpc.StartListening();
                
                Console.WriteLine("RPC服务器已启动，等待调用...");
                
                // 等待5秒后关闭
                await Task.Delay(5000);
                jsonRpc.Dispose();
            });
            
            // 等待服务器启动
            await Task.Delay(1000);
            
            // 启动客户端
            var clientTask = Task.Run(async () =>
            {
                using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                await clientPipe.ConnectAsync(3000);
                
                Console.WriteLine("客户端已连接到服务器");
                
                // 创建JsonRpc客户端
                var jsonRpc = new JsonRpc(clientPipe);
                jsonRpc.StartListening();
                
                try
                {
                    // 调用RPC方法
                    var result = await jsonRpc.InvokeAsync<int>("Add", 10, 20);
                    Console.WriteLine($"Add(10, 20) = {result}");
                    
                    var divResult = await jsonRpc.InvokeAsync<double>("Divide", 100.0, 5.0);
                    Console.WriteLine($"Divide(100, 5) = {divResult}");
                    
                    // 发送通知
                    await jsonRpc.NotifyAsync("Log", "这是一个通知消息");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RPC调用异常: {ex.Message}");
                }
                finally
                {
                    jsonRpc.Dispose();
                }
            });
            
            // 等待完成
            await Task.WhenAll(serverTask, clientTask);
        }
    }

    /// <summary>
    /// 示例计算器服务（增强版）
    /// </summary>
    [RpcService("Calculator")]
    public class EnhancedCalculatorService
    {
        private readonly ILogger<EnhancedCalculatorService> _logger;

        public EnhancedCalculatorService(ILogger<EnhancedCalculatorService> logger = null)
        {
            _logger = logger;
        }

        [RpcMethod("Add")]
        public async Task<int> AddAsync(
            [RpcParamNotNull] int a, 
            [RpcParamRange(0, 1000)] int b)
        {
            _logger?.LogInformation("执行加法: {A} + {B}", a, b);
            return await Task.FromResult(a + b);
        }

        [RpcMethod("Subtract")]
        public async Task<int> SubtractAsync(
            [RpcParamNotNull] int a, 
            [RpcParamNotNull] int b)
        {
            _logger?.LogInformation("执行减法: {A} - {B}", a, b);
            return await Task.FromResult(a - b);
        }

        [RpcMethod("Multiply")]
        public async Task<int> MultiplyAsync(
            [RpcParamNotNull] int a, 
            [RpcParamNotNull] int b)
        {
            _logger?.LogInformation("执行乘法: {A} * {B}", a, b);
            return await Task.FromResult(a * b);
        }

        [RpcMethod("Divide")]
        public async Task<double> DivideAsync(
            [RpcParamNotNull] double numerator, 
            [RpcParamRange(0.001, double.MaxValue)] double denominator)
        {
            _logger?.LogInformation("执行除法: {Numerator} / {Denominator}", numerator, denominator);
            
            if (Math.Abs(denominator) < 0.001)
            {
                throw new ArgumentException("除数不能为零或接近零");
            }
            
            return await Task.FromResult(numerator / denominator);
        }

        [RpcMethod("Power")]
        public async Task<double> PowerAsync(
            [RpcParamRange(-1000, 1000)] double baseValue, 
            [RpcParamRange(0, 10)] int exponent)
        {
            _logger?.LogInformation("执行幂运算: {Base} ^ {Exponent}", baseValue, exponent);
            return await Task.FromResult(Math.Pow(baseValue, exponent));
        }

        [RpcMethod("Log", IsNotification = true)]
        public async Task LogAsync([RpcParamNotNull] string message)
        {
            _logger?.LogInformation("收到日志消息: {Message}", message);
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            await Task.CompletedTask;
        }

        [RpcMethod("GetHistory")]
        public async Task<CalculationHistory[]> GetHistoryAsync(
            [RpcParamRange(1, 100)] int count = 10)
        {
            _logger?.LogInformation("获取计算历史，数量: {Count}", count);
            
            // 模拟历史数据
            var history = new CalculationHistory[count];
            for (int i = 0; i < count; i++)
            {
                history[i] = new CalculationHistory
                {
                    Operation = $"Operation_{i + 1}",
                    Result = i * 10,
                    Timestamp = DateTime.Now.AddMinutes(-i)
                };
            }
            
            return await Task.FromResult(history);
        }
    }

    /// <summary>
    /// 计算历史记录
    /// </summary>
    public class CalculationHistory
    {
        public string Operation { get; set; }
        public double Result { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 自定义验证器示例
    /// </summary>
    public static class CustomValidators
    {
        public static bool ValidatePositiveNumber(object value)
        {
            if (value is IComparable comparable && comparable is IConvertible convertible)
            {
                var doubleValue = convertible.ToDouble(null);
                return doubleValue > 0;
            }
            return false;
        }

        public static bool ValidateEmailFormat(object value)
        {
            if (value is string email)
            {
                return email.Contains("@") && email.Contains(".");
            }
            return false;
        }
    }
} 