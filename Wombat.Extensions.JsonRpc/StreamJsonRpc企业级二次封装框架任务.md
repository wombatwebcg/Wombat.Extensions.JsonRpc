# 上下文
文件名：StreamJsonRpc企业级二次封装框架任务.md
创建于：2025-01-27
创建者：AI助手
关联协议：RIPER-5 + Multidimensional + Agent Protocol

# 任务描述
基于StreamJsonRpc 2.22.11构建一个企业级的二次封装框架，实现高性能、高可用、可扩展的RPC通信解决方案。该框架将支持多种传输协议、代码生成、性能优化、监控诊断等企业级特性。

# 项目概述
本项目旨在创建一个完整的企业级RPC通信框架，在StreamJsonRpc基础上增加批处理优化、连接池管理、代码生成、监控集成等高级特性，目标是实现100,000+ req/s的单连接吞吐量和1,000,000+ req/s的批处理吞吐量。

---
*以下部分由 AI 在协议执行过程中维护*
---

# 分析 (由 RESEARCH 模式填充)

## 技术背景分析
- **StreamJsonRpc 2.22.11特性**：基于System.IO.Pipelines的高性能I/O，支持MessagePack和JSON序列化，完整的异步支持
- **性能瓶颈识别**：传统RPC框架单连接吞吐量限制在1000 req/s，主要瓶颈在TCP栈遍历和请求-响应模式
- **市场需求**：企业级应用需要高吞吐量、低延迟、高可用性的RPC通信解决方案

## 现有项目状态
- 项目使用.NET Standard 2.0目标框架
- 已配置StreamJsonRpc 2.22.11依赖
- 项目结构基础，缺少实际代码实现
- 现有任务文档功能规划相对简单，缺乏企业级特性考虑

## 技术挑战识别
- **性能优化**：需要实现批处理、零拷贝、连接池等优化技术
- **传输层抽象**：需要统一多种传输协议的接口设计
- **代码生成**：需要实现强类型客户端代理和服务端存根生成
- **企业级特性**：需要集成认证、监控、弹性处理等功能

# 提议的解决方案 (由 INNOVATE 模式填充)

## 核心架构创新
**1. 基于特性的RPC接口暴露系统**
- 通过[RpcMethod]特性标注要暴露的方法，避免字符串命名依赖
- 自动扫描和注册带有RPC特性的类和方法
- 支持参数验证特性：[RpcParamNotNull]、[RpcParamRange]等
- 强类型接口定义和客户端代理自动生成
- 元数据提取和服务发现机制

**2. 高性能批处理机制**
- 实现10KB批量阈值的请求聚合器
- 自适应批处理策略，根据网络延迟动态调整
- 零拷贝内存管理，基于System.IO.Pipelines优化

**3. 企业级传输层设计**
- 统一的ITwoWayChannel接口支持多种传输协议
- 多路复用和管道化请求处理
- 集成压缩、加密和心跳机制

**4. 智能代码生成系统**
- 基于元数据的强类型客户端代理生成
- OpenAPI/Swagger文档自动生成
- TypeScript定义生成支持前端集成

**5. 可观测性和弹性设计**
- OpenTelemetry分布式追踪集成
- 断路器模式和重试策略
- 实时性能监控和告警

## 性能优化策略
- **批处理优化**：将吞吐量从1000 req/s提升到1,000,000 req/s
- **连接池管理**：支持1000+并发连接，智能负载均衡
- **内存优化**：使用ArrayPool和对象池，减少GC压力
- **网络优化**：TCP_NODELAY、自适应缓冲区大小

## 企业级特性规划
- **安全性**：JWT认证、RBAC权限控制、请求签名验证
- **可靠性**：故障转移、负载均衡、健康检查
- **可维护性**：结构化日志、指标收集、自动化测试
- **扩展性**：中间件系统、插件架构、配置管理

# 实施计划 (由 PLAN 模式生成)

## 项目结构设计
```
Wombat.Extensions.JsonRpc/
├── Core/                    # 核心功能模块
│   ├── Contracts/           # 接口定义
│   ├── Serialization/       # 序列化抽象
│   ├── Pipeline/           # 请求处理管道
│   └── Diagnostics/        # 诊断监控
├── Transport/              # 传输层实现
│   ├── Abstractions/       # 传输层抽象
│   ├── Tcp/               # TCP实现
│   ├── WebSocket/         # WebSocket实现
│   ├── NamedPipe/         # 命名管道实现
│   └── Http/              # HTTP实现
├── Client/                 # 客户端功能
│   ├── Proxy/             # 动态代理生成
│   ├── LoadBalancing/     # 负载均衡
│   └── Resilience/        # 弹性处理
├── Server/                 # 服务端功能
│   ├── Hosting/           # 服务托管
│   ├── Routing/           # 路由处理
│   └── Middleware/        # 中间件系统
├── CodeGen/               # 代码生成工具
│   ├── Analysis/          # 代码分析
│   ├── Templates/         # 代码模板
│   └── Generators/        # 代码生成器
└── Extensions/            # 扩展功能
    ├── Authentication/    # 认证扩展
    ├── Caching/          # 缓存扩展
    └── Monitoring/       # 监控扩展
```

## 性能目标设定
- **单连接吞吐量**：>100,000 req/s
- **批处理吞吐量**：>1,000,000 req/s
- **平均延迟**：<1ms（本地网络）
- **内存使用**：<50MB（1000并发连接）
- **连接数**：支持10,000+并发连接

## 技术实现细节
**1. 基于特性的RPC接口定义系统**
```csharp
// RPC方法特性定义
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RpcMethodAttribute : Attribute
{
    public string MethodName { get; }
    public bool IsNotification { get; set; }
    public int TimeoutMs { get; set; } = 30000;
    public bool RequireAuthentication { get; set; } = false;
    
    public RpcMethodAttribute(string methodName = null)
    {
        MethodName = methodName;
    }
}

// 参数验证特性
[AttributeUsage(AttributeTargets.Parameter)]
public class RpcParamNotNullAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public class RpcParamRangeAttribute : Attribute
{
    public object Min { get; }
    public object Max { get; }
    public RpcParamRangeAttribute(object min, object max)
    {
        Min = min;
        Max = max;
    }
}

// 服务接口定义示例
[RpcMethod]
public interface ICalculatorService
{
    [RpcMethod("Add")]
    Task<int> AddAsync([RpcParamNotNull] int a, [RpcParamRange(0, 1000)] int b);
    
    [RpcMethod("Divide", RequireAuthentication = true)]
    Task<double> DivideAsync(double numerator, [RpcParamRange(0.001, double.MaxValue)] double denominator);
    
    [RpcMethod("Log", IsNotification = true)]
    Task LogAsync([RpcParamNotNull] string message);
}

// 服务实现示例
public class CalculatorService : ICalculatorService
{
    public async Task<int> AddAsync(int a, int b)
    {
        return a + b;
    }
    
    public async Task<double> DivideAsync(double numerator, double denominator)
    {
        if (denominator == 0)
            throw new ArgumentException("Division by zero");
        return numerator / denominator;
    }
    
    public async Task LogAsync(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] {message}");
    }
}
```

**2. 高性能RPC服务器接口**
```csharp
public interface IAdvancedRpcServer : IDisposable
{
    // 配置优化
    IAdvancedRpcServer ConfigureBatching(int batchSize, TimeSpan timeout);
    IAdvancedRpcServer ConfigureCompression(CompressionLevel level);
    IAdvancedRpcServer ConfigureConnectionPool(int maxConnections);
    
    // 中间件和认证
    IAdvancedRpcServer UseMiddleware<T>() where T : class, IRpcMiddleware;
    IAdvancedRpcServer UseAuthentication(AuthenticationOptions options);
    IAdvancedRpcServer UseParameterValidation(bool enabled = true);
    
    // 服务注册（基于特性）
    IAdvancedRpcServer RegisterService<T>(T instance, ServiceRegistrationOptions options = null);
    IAdvancedRpcServer RegisterServiceFromAssembly(Assembly assembly, Func<Type, bool> filter = null);
    IAdvancedRpcServer RegisterService<TInterface, TImplementation>() 
        where TInterface : class 
        where TImplementation : class, TInterface, new();
    
    // 元数据和发现
    Task<ServiceMetadata[]> GetRegisteredServicesAsync();
    Task<MethodMetadata[]> GetServiceMethodsAsync(string serviceName);
    
    // 生命周期管理
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

**3. 智能客户端代理接口**
```csharp
public interface IAdvancedRpcClient : IDisposable
{
    // 性能配置
    IAdvancedRpcClient ConfigureLoadBalancing(LoadBalancingStrategy strategy);
    IAdvancedRpcClient ConfigureFailover(FailoverOptions options);
    IAdvancedRpcClient ConfigureRetry(RetryPolicy policy);
    IAdvancedRpcClient ConfigureCaching(CachingOptions options);
    
    // 强类型代理生成
    T CreateProxy<T>() where T : class;
    Task<T> CreateProxyAsync<T>() where T : class;
    
    // 动态调用（保留向后兼容）
    Task<TResult> InvokeAsync<TResult>(string methodName, params object[] args);
    Task NotifyAsync(string methodName, params object[] args);
    
    // 元数据和发现
    Task<ServiceMetadata> GetServiceMetadataAsync<T>();
    Task<ServiceMetadata> GetServiceMetadataAsync(string serviceName);
    Task<IEnumerable<ServiceEndpoint>> DiscoverServicesAsync();
}
```

**4. 代码生成系统接口**
```csharp
public interface IRpcCodeGenerator
{
    // 客户端代理生成
    Task<string> GenerateClientProxyAsync(ServiceMetadata metadata, GenerationOptions options);
    Task<string> GenerateClientProxyAsync<T>(GenerationOptions options = null) where T : class;
    
    // 服务端存根生成
    Task<string> GenerateServerStubAsync(Type serviceType, GenerationOptions options);
    
    // 文档生成
    Task<OpenApiDocument> GenerateOpenApiAsync(IEnumerable<ServiceMetadata> services);
    Task<string> GenerateMarkdownDocAsync(IEnumerable<ServiceMetadata> services);
    
    // 前端集成
    Task<string> GenerateTypeScriptDefinitionsAsync(ServiceMetadata metadata);
    Task<string> GenerateTypeScriptClientAsync(ServiceMetadata metadata, TypeScriptOptions options);
}

// 元数据模型
public class ServiceMetadata
{
    public string ServiceName { get; set; }
    public Type ServiceType { get; set; }
    public MethodMetadata[] Methods { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
}

public class MethodMetadata
{
    public string MethodName { get; set; }
    public string DisplayName { get; set; }
    public bool IsNotification { get; set; }
    public bool RequireAuthentication { get; set; }
    public int TimeoutMs { get; set; }
    public ParameterMetadata[] Parameters { get; set; }
    public Type ReturnType { get; set; }
    public string Description { get; set; }
}

public class ParameterMetadata
{
    public string Name { get; set; }
    public Type Type { get; set; }
    public bool IsRequired { get; set; }
    public ValidationAttribute[] Validations { get; set; }
    public string Description { get; set; }
}
```

实施检查清单：
1. 创建核心项目结构和依赖配置
2. 实现基于特性的RPC接口暴露系统（RpcMethodAttribute等）
3. 开发参数验证特性和自动验证机制
4. 实现ITwoWayChannel高级接口和传输层抽象
5. 开发高性能RPC服务器核心引擎
6. 实现批处理请求聚合器和响应优化
7. 开发智能客户端代理和连接池管理
8. 实现强类型代理生成和元数据提取
9. 实现中间件系统和认证授权
10. 开发代码生成系统和文档生成
11. 集成监控、日志和分布式追踪
12. 实现弹性处理和故障转移机制
13. 开发测试套件和性能基准测试
14. 创建文档和示例应用
15. 实现CI/CD流水线和自动化部署

# 当前执行步骤
> 已完成: "步骤6：构建RPC服务器和客户端"
> 等待用户确认后将继续: "步骤7：创建完整的示例和文档"

# 使用示例
## 基于特性的RPC服务定义和使用
```csharp
// 1. 定义RPC服务接口
[RpcMethod]
public interface IUserService
{
    [RpcMethod("GetUser")]
    Task<User> GetUserAsync([RpcParamNotNull] string userId);
    
    [RpcMethod("CreateUser", RequireAuthentication = true)]
    Task<User> CreateUserAsync([RpcParamNotNull] User user);
    
    [RpcMethod("UpdateUserAge")]
    Task<bool> UpdateUserAgeAsync([RpcParamNotNull] string userId, [RpcParamRange(0, 150)] int age);
    
    [RpcMethod("LogUserActivity", IsNotification = true)]
    Task LogUserActivityAsync([RpcParamNotNull] string userId, [RpcParamNotNull] string activity);
}

// 2. 实现RPC服务
public class UserService : IUserService
{
    public async Task<User> GetUserAsync(string userId)
    {
        // 实现获取用户逻辑
        return await GetUserFromDatabase(userId);
    }
    
    public async Task<User> CreateUserAsync(User user)
    {
        // 实现创建用户逻辑
        return await SaveUserToDatabase(user);
    }
    
    public async Task<bool> UpdateUserAgeAsync(string userId, int age)
    {
        // 实现更新用户年龄逻辑
        return await UpdateUserInDatabase(userId, age);
    }
    
    public async Task LogUserActivityAsync(string userId, string activity)
    {
        // 实现用户活动日志
        Console.WriteLine($"User {userId} performed: {activity}");
    }
}

// 3. 服务端启动
var server = new AdvancedRpcServer();
server.ConfigureBatching(10240, TimeSpan.FromMilliseconds(50))
      .ConfigureCompression(CompressionLevel.Optimal)
      .ConfigureConnectionPool(1000)
      .UseParameterValidation(true)
      .UseAuthentication(new AuthenticationOptions 
      { 
          ValidateJwtToken = true,
          SecretKey = "your-secret-key"
      })
      .RegisterService<IUserService, UserService>()
      .RegisterServiceFromAssembly(Assembly.GetExecutingAssembly(), 
          type => type.GetCustomAttribute<RpcMethodAttribute>() != null);

await server.StartAsync();

// 4. 客户端使用（强类型代理）
var client = new AdvancedRpcClient("tcp://localhost:8080");
client.ConfigureRetry(new RetryPolicy { MaxRetries = 3, DelayMs = 1000 })
      .ConfigureCaching(new CachingOptions { EnableResponseCaching = true });

// 创建强类型代理
var userService = client.CreateProxy<IUserService>();

// 调用RPC方法，就像本地方法一样
var user = await userService.GetUserAsync("user123");
var newUser = await userService.CreateUserAsync(new User { Name = "John", Age = 30 });
await userService.UpdateUserAgeAsync("user123", 31);
await userService.LogUserActivityAsync("user123", "Login");

// 5. 元数据和服务发现
var metadata = await client.GetServiceMetadataAsync<IUserService>();
Console.WriteLine($"Service: {metadata.ServiceName}");
foreach (var method in metadata.Methods)
{
    Console.WriteLine($"  Method: {method.MethodName}, Auth Required: {method.RequireAuthentication}");
}

// 6. 代码生成示例
var codeGenerator = new RpcCodeGenerator();
var clientCode = await codeGenerator.GenerateClientProxyAsync<IUserService>();
var openApiDoc = await codeGenerator.GenerateOpenApiAsync(new[] { metadata });
var tsDefinitions = await codeGenerator.GenerateTypeScriptDefinitionsAsync(metadata);
```

## 性能优化配置示例
```csharp
// 高性能服务器配置
var server = new AdvancedRpcServer();
server.ConfigureBatching(
    batchSize: 10240,           // 10KB批处理阈值
    timeout: TimeSpan.FromMilliseconds(5)  // 5ms超时
)
.ConfigureCompression(CompressionLevel.Fastest)  // 快速压缩
.ConfigureConnectionPool(maxConnections: 10000)  // 支持10K并发连接
.UseMiddleware<PerformanceMonitoringMiddleware>()
.UseMiddleware<RequestThrottlingMiddleware>()
.UseParameterValidation(true);

// 高性能客户端配置
var client = new AdvancedRpcClient("tcp://localhost:8080");
client.ConfigureBatching(new BatchingOptions 
{
    BatchSize = 8192,
    MaxBatchDelay = TimeSpan.FromMilliseconds(10),
    EnableCompression = true
})
.ConfigureLoadBalancing(LoadBalancingStrategy.RoundRobin)
.ConfigureFailover(new FailoverOptions 
{
    MaxRetries = 3,
    FailoverTimeoutMs = 5000,
    CircuitBreakerEnabled = true
})
.ConfigureCaching(new CachingOptions 
{
    EnableResponseCaching = true,
    CacheTtlMs = 60000,
    MaxCacheSize = 1000
});
```

# 任务进度
*   2025-01-27 14:30:00
    *   步骤：1. 创建核心项目结构和依赖配置
    *   修改：
      - StreamJsonRpc企业级二次封装框架任务.md：添加基于特性的RPC接口暴露系统设计
      - Wombat.Extensions.JsonRpc.csproj：更新依赖配置，添加企业级功能所需的NuGet包
    *   更改摘要：
      - 完善了基于特性的RPC接口设计，包括RpcMethodAttribute、参数验证特性、元数据模型等
      - 添加了完整的使用示例和性能优化配置示例
      - 更新了项目文件，添加了60+个企业级依赖包，包括性能优化、安全、监控、代码生成等
    *   原因：执行计划步骤1，并根据用户反馈补充基于特性的RPC接口功能
    *   阻碍：无
    *   用户确认状态：成功

*   2025-01-27 14:45:00
    *   步骤：2. 实现基于特性的RPC接口暴露系统（RpcMethodAttribute等）
    *   修改：
      - Core/Contracts/RpcMethodAttribute.cs：创建RpcMethodAttribute和RpcServiceAttribute特性
      - Core/Contracts/RpcParameterValidationAttributes.cs：创建参数验证特性类
      - Core/Contracts/ServiceMetadata.cs：创建服务元数据模型
      - Core/Contracts/IRpcMetadataProvider.cs：创建元数据提供程序接口
      - Core/Contracts/DefaultRpcMetadataProvider.cs：实现默认元数据提供程序
      - Core/Contracts/RpcValidationException.cs：创建验证异常处理类
      - Examples/CalculatorService.cs：创建完整的使用示例
    *   更改摘要：
      - 实现了完整的基于特性的RPC接口暴露系统
      - 支持RpcMethodAttribute、RpcServiceAttribute标注
      - 实现了5种参数验证特性：NotNull、Range、StringLength、Regex、CustomValidation
      - 创建了完整的元数据模型和提取机制
      - 实现了参数验证器和异常处理
      - 提供了计算器服务的完整示例
    *   原因：执行计划步骤2，实现基于特性的RPC接口暴露核心功能
    *   阻碍：无
    *   用户确认状态：成功

*   2025-01-27 15:00:00
    *   步骤：3. 实现参数验证和自动验证机制
    *   修改：
      - Core/Validation/ParameterValidator.cs：创建参数验证器，支持所有验证特性
      - Core/Validation/RpcMethodInterceptor.cs：创建RPC方法拦截器，自动应用验证
    *   更改摘要：
      - 实现了完整的参数验证引擎，支持NotNull、Range、StringLength、Regex、CustomValidation
      - 创建了RPC方法拦截器，自动在方法调用前执行参数验证
      - 支持异步方法的参数验证和结果处理
      - 实现了详细的验证错误报告和异常处理机制
      - 提供了拦截器工厂和方法调用上下文追踪
    *   原因：执行计划步骤3，实现自动参数验证和方法拦截机制
    *   阻碍：无
    *   用户确认状态：成功

*   2025-01-27 15:15:00
    *   步骤：4. 构建RPC目标对象构建器
    *   修改：
      - Core/Builder/RpcTargetBuilder.cs：创建RPC目标构建器，支持自动发现和注册RPC服务
      - Core/Builder/RpcTargetBuilderFactory.cs：创建构建器工厂，支持多种配置模式
      - Examples/RpcTargetBuilderExample.cs：创建完整的使用示例和服务器客户端演示
    *   更改摘要：
      - 实现了完整的RPC目标构建器，支持自动扫描程序集发现RPC服务
      - 集成了参数验证和方法拦截功能
      - 支持依赖注入和服务实例管理
      - 提供了多种配置模式：标准、高性能、最小化、调试模式
      - 创建了完整的服务器客户端示例，展示Named Pipe通信
      - 实现了工厂模式，简化不同场景下的构建器创建
    *   原因：执行计划步骤4，实现RPC服务自动发现和注册机制
    *   阻碍：无
    *   用户确认状态：成功

*   2025-01-27 15:30:00
    *   步骤：5. 实现多种传输层支持
    *   修改：
      - Core/Transport/ITwoWayChannel.cs：创建统一的双向通信通道接口
      - Core/Transport/TcpTransport.cs：实现TCP传输层，支持客户端和服务器模式
      - Core/Transport/WebSocketTransport.cs：实现WebSocket传输层，支持实时双向通信
      - Core/Transport/NamedPipeTransport.cs：实现Named Pipe传输层，支持本地进程间通信
      - Core/Transport/TransportFactory.cs：创建传输层工厂，统一管理各种传输类型
    *   更改摘要：
      - 实现了完整的传输层抽象，支持TCP、WebSocket、Named Pipe三种传输方式
      - 提供了统一的ITwoWayChannel接口，抽象了底层传输细节
      - 实现了连接管理、统计信息收集、错误处理等企业级功能
      - 支持客户端和服务器模式，提供了完整的连接生命周期管理
      - 创建了传输层工厂，简化了不同传输类型的创建和配置
      - 提供了丰富的配置选项和性能优化参数
    *   原因：执行计划步骤5，实现多种传输层支持，为RPC框架提供灵活的通信基础
    *   阻碍：无
    *   用户确认状态：成功

*   2025-01-27 15:45:00
    *   步骤：6. 构建RPC服务器和客户端
    *   修改：
      - Core/Server/RpcServer.cs：创建企业级RPC服务器，集成所有组件
      - Core/Server/RpcServerOptions.cs：创建服务器配置选项，支持企业级功能
      - Core/Client/RpcClient.cs：创建高级RPC客户端，支持自动重连和代理生成
      - Core/Client/RpcClientOptions.cs：创建客户端配置选项，支持多种场景
      - Examples/RpcServerClientExample.cs：创建完整的服务器客户端示例
      - Core/Transport/TransportFactory.cs：重新创建传输工厂（之前被删除）
    *   更改摘要：
      - 实现了企业级RPC服务器，支持多传输、服务发现、参数验证、事件处理
      - 创建了功能完整的RPC客户端，支持自动重连、心跳检测、代理生成
      - 提供了丰富的配置选项，支持高性能、安全、可靠等多种场景
      - 集成了所有之前构建的组件：传输层、目标构建器、参数验证、特性系统
      - 实现了完整的连接生命周期管理和统计信息收集
      - 创建了全面的示例，展示基本使用、多传输、企业功能、性能测试等场景
    *   原因：执行计划步骤6，构建完整的RPC服务器和客户端系统
    *   阻碍：无
    *   用户确认状态：待确认

# 最终审查
*待REVIEW模式填充* 