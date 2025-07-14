using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Core.Server
{
    /// <summary>
    /// RPC服务器配置选项
    /// </summary>
    public class RpcServerOptions
    {
        /// <summary>
        /// 服务器名称
        /// </summary>
        public string ServerName { get; set; } = "RpcServer";

        /// <summary>
        /// 服务器版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 服务器描述
        /// </summary>
        public string Description { get; set; } = "StreamJsonRpc Enterprise Server";

        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// 最大并发连接数
        /// </summary>
        public int MaxConcurrentConnections { get; set; } = 1000;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 请求超时时间
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 心跳间隔
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用心跳检测
        /// </summary>
        public bool EnableHeartbeat { get; set; } = true;

        /// <summary>
        /// 是否启用连接池
        /// </summary>
        public bool EnableConnectionPooling { get; set; } = true;

        /// <summary>
        /// 连接池大小
        /// </summary>
        public int ConnectionPoolSize { get; set; } = 100;

        /// <summary>
        /// 是否启用请求限流
        /// </summary>
        public bool EnableRateLimiting { get; set; } = false;

        /// <summary>
        /// 每秒最大请求数
        /// </summary>
        public int MaxRequestsPerSecond { get; set; } = 1000;

        /// <summary>
        /// 是否启用请求缓存
        /// </summary>
        public bool EnableRequestCaching { get; set; } = false;

        /// <summary>
        /// 缓存过期时间
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// 压缩级别
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Balanced;

        /// <summary>
        /// 是否启用SSL/TLS
        /// </summary>
        public bool EnableSsl { get; set; } = false;

        /// <summary>
        /// SSL证书配置
        /// </summary>
        public SslConfiguration SslConfiguration { get; set; } = new SslConfiguration();

        /// <summary>
        /// 是否启用身份验证
        /// </summary>
        public bool EnableAuthentication { get; set; } = false;

        /// <summary>
        /// 身份验证配置
        /// </summary>
        public AuthenticationConfiguration AuthenticationConfiguration { get; set; } = new AuthenticationConfiguration();

        /// <summary>
        /// 是否启用授权
        /// </summary>
        public bool EnableAuthorization { get; set; } = false;

        /// <summary>
        /// 授权配置
        /// </summary>
        public AuthorizationConfiguration AuthorizationConfiguration { get; set; } = new AuthorizationConfiguration();

        /// <summary>
        /// 是否启用审计日志
        /// </summary>
        public bool EnableAuditLogging { get; set; } = false;

        /// <summary>
        /// 审计日志配置
        /// </summary>
        public AuditLoggingConfiguration AuditLoggingConfiguration { get; set; } = new AuditLoggingConfiguration();

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// 性能监控配置
        /// </summary>
        public PerformanceMonitoringConfiguration PerformanceMonitoringConfiguration { get; set; } = new PerformanceMonitoringConfiguration();

        /// <summary>
        /// 是否启用健康检查
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// 健康检查配置
        /// </summary>
        public HealthCheckConfiguration HealthCheckConfiguration { get; set; } = new HealthCheckConfiguration();

        /// <summary>
        /// 默认传输选项
        /// </summary>
        public TransportFactoryOptions DefaultTransportOptions { get; set; } = new TransportFactoryOptions();

        /// <summary>
        /// 扩展配置
        /// </summary>
        public Dictionary<string, object> ExtendedConfiguration { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        public static RpcServerOptions CreateDefault()
        {
            return new RpcServerOptions();
        }

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        /// <returns>高性能配置</returns>
        public static RpcServerOptions CreateHighPerformance()
        {
            return new RpcServerOptions
            {
                EnableParameterValidation = false,
                EnableVerboseLogging = false,
                LogLevel = LogLevel.Warning,
                MaxConcurrentConnections = 10000,
                ConnectionTimeout = TimeSpan.FromMinutes(60),
                RequestTimeout = TimeSpan.FromSeconds(10),
                EnableConnectionPooling = true,
                ConnectionPoolSize = 500,
                EnableCompression = true,
                CompressionLevel = CompressionLevel.Fastest,
                EnableRequestCaching = true,
                CacheExpiration = TimeSpan.FromMinutes(30)
            };
        }

        /// <summary>
        /// 创建安全配置
        /// </summary>
        /// <returns>安全配置</returns>
        public static RpcServerOptions CreateSecure()
        {
            return new RpcServerOptions
            {
                EnableParameterValidation = true,
                EnableVerboseLogging = true,
                LogLevel = LogLevel.Debug,
                EnableSsl = true,
                EnableAuthentication = true,
                EnableAuthorization = true,
                EnableAuditLogging = true,
                EnableRateLimiting = true,
                MaxRequestsPerSecond = 100,
                ConnectionTimeout = TimeSpan.FromMinutes(15)
            };
        }

        /// <summary>
        /// 创建开发配置
        /// </summary>
        /// <returns>开发配置</returns>
        public static RpcServerOptions CreateDevelopment()
        {
            return new RpcServerOptions
            {
                EnableParameterValidation = true,
                EnableVerboseLogging = true,
                LogLevel = LogLevel.Debug,
                MaxConcurrentConnections = 100,
                EnablePerformanceMonitoring = true,
                EnableHealthCheck = true,
                RequestTimeout = TimeSpan.FromMinutes(5)
            };
        }
    }

    /// <summary>
    /// 压缩级别
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// 无压缩
        /// </summary>
        None,

        /// <summary>
        /// 最快压缩
        /// </summary>
        Fastest,

        /// <summary>
        /// 平衡压缩
        /// </summary>
        Balanced,

        /// <summary>
        /// 最优压缩
        /// </summary>
        Optimal
    }

    /// <summary>
    /// SSL配置
    /// </summary>
    public class SslConfiguration
    {
        /// <summary>
        /// 证书文件路径
        /// </summary>
        public string CertificateFilePath { get; set; }

        /// <summary>
        /// 证书密码
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// 是否需要客户端证书
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;

        /// <summary>
        /// 支持的SSL协议
        /// </summary>
        public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;
    }

    /// <summary>
    /// SSL协议
    /// </summary>
    [Flags]
    public enum SslProtocols
    {
        None = 0,
        Tls10 = 1,
        Tls11 = 2,
        Tls12 = 4,
        Tls13 = 8
    }

    /// <summary>
    /// 身份验证配置
    /// </summary>
    public class AuthenticationConfiguration
    {
        /// <summary>
        /// 身份验证类型
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.Token;

        /// <summary>
        /// JWT配置
        /// </summary>
        public JwtConfiguration JwtConfiguration { get; set; } = new JwtConfiguration();

        /// <summary>
        /// API Key配置
        /// </summary>
        public ApiKeyConfiguration ApiKeyConfiguration { get; set; } = new ApiKeyConfiguration();

        /// <summary>
        /// 自定义验证器
        /// </summary>
        public string CustomAuthenticatorType { get; set; }
    }

    /// <summary>
    /// 身份验证类型
    /// </summary>
    public enum AuthenticationType
    {
        None,
        Token,
        ApiKey,
        Certificate,
        Custom
    }

    /// <summary>
    /// JWT配置
    /// </summary>
    public class JwtConfiguration
    {
        /// <summary>
        /// 密钥
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// 发行者
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// 受众
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// API Key配置
    /// </summary>
    public class ApiKeyConfiguration
    {
        /// <summary>
        /// API Key头名称
        /// </summary>
        public string HeaderName { get; set; } = "X-API-Key";

        /// <summary>
        /// 有效的API Keys
        /// </summary>
        public HashSet<string> ValidApiKeys { get; set; } = new HashSet<string>();
    }

    /// <summary>
    /// 授权配置
    /// </summary>
    public class AuthorizationConfiguration
    {
        /// <summary>
        /// 授权类型
        /// </summary>
        public AuthorizationType AuthorizationType { get; set; } = AuthorizationType.Role;

        /// <summary>
        /// 角色配置
        /// </summary>
        public RoleConfiguration RoleConfiguration { get; set; } = new RoleConfiguration();

        /// <summary>
        /// 权限配置
        /// </summary>
        public PermissionConfiguration PermissionConfiguration { get; set; } = new PermissionConfiguration();
    }

    /// <summary>
    /// 授权类型
    /// </summary>
    public enum AuthorizationType
    {
        None,
        Role,
        Permission,
        Custom
    }

    /// <summary>
    /// 角色配置
    /// </summary>
    public class RoleConfiguration
    {
        /// <summary>
        /// 角色定义
        /// </summary>
        public Dictionary<string, string[]> Roles { get; set; } = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// 权限配置
    /// </summary>
    public class PermissionConfiguration
    {
        /// <summary>
        /// 权限定义
        /// </summary>
        public Dictionary<string, string[]> Permissions { get; set; } = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// 审计日志配置
    /// </summary>
    public class AuditLoggingConfiguration
    {
        /// <summary>
        /// 是否记录请求
        /// </summary>
        public bool LogRequests { get; set; } = true;

        /// <summary>
        /// 是否记录响应
        /// </summary>
        public bool LogResponses { get; set; } = true;

        /// <summary>
        /// 是否记录异常
        /// </summary>
        public bool LogExceptions { get; set; } = true;

        /// <summary>
        /// 日志提供程序
        /// </summary>
        public string LogProvider { get; set; } = "Default";

        /// <summary>
        /// 日志配置
        /// </summary>
        public Dictionary<string, object> LogConfiguration { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 性能监控配置
    /// </summary>
    public class PerformanceMonitoringConfiguration
    {
        /// <summary>
        /// 是否启用指标收集
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// 指标收集间隔
        /// </summary>
        public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用分布式追踪
        /// </summary>
        public bool EnableTracing { get; set; } = false;

        /// <summary>
        /// 追踪配置
        /// </summary>
        public TracingConfiguration TracingConfiguration { get; set; } = new TracingConfiguration();
    }

    /// <summary>
    /// 追踪配置
    /// </summary>
    public class TracingConfiguration
    {
        /// <summary>
        /// 追踪提供程序
        /// </summary>
        public string TracingProvider { get; set; } = "OpenTelemetry";

        /// <summary>
        /// 采样率
        /// </summary>
        public double SamplingRate { get; set; } = 0.1;

        /// <summary>
        /// 追踪配置
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public class HealthCheckConfiguration
    {
        /// <summary>
        /// 检查间隔
        /// </summary>
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 检查超时
        /// </summary>
        public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 启用的检查项
        /// </summary>
        public HashSet<string> EnabledChecks { get; set; } = new HashSet<string>
        {
            "Memory",
            "Disk",
            "Network",
            "Database"
        };
    }
} 