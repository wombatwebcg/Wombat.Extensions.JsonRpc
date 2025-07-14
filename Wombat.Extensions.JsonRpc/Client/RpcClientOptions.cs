using System;
using System.Collections.Generic;

namespace Wombat.Extensions.JsonRpc.Client
{
    /// <summary>
    /// RPC客户端配置选项
    /// </summary>
    public class RpcClientOptions
    {
        /// <summary>
        /// 客户端名称
        /// </summary>
        public string ClientName { get; set; } = "RpcClient";

        /// <summary>
        /// 客户端版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 请求超时时间
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连间隔
        /// </summary>
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 最大重连次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 10;

        /// <summary>
        /// 是否启用心跳
        /// </summary>
        public bool EnableHeartbeat { get; set; } = true;

        /// <summary>
        /// 心跳间隔
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 心跳超时时间
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 是否启用请求重试
        /// </summary>
        public bool EnableRequestRetry { get; set; } = false;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 重试间隔
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// 压缩级别
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Balanced;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCaching { get; set; } = false;

        /// <summary>
        /// 缓存过期时间
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 缓存大小限制
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// 是否启用SSL/TLS
        /// </summary>
        public bool EnableSsl { get; set; } = false;

        /// <summary>
        /// SSL配置
        /// </summary>
        public ClientSslConfiguration SslConfiguration { get; set; } = new ClientSslConfiguration();

        /// <summary>
        /// 是否启用身份验证
        /// </summary>
        public bool EnableAuthentication { get; set; } = false;

        /// <summary>
        /// 身份验证配置
        /// </summary>
        public ClientAuthenticationConfiguration AuthenticationConfiguration { get; set; } = new ClientAuthenticationConfiguration();

        /// <summary>
        /// 是否启用追踪
        /// </summary>
        public bool EnableTracing { get; set; } = false;

        /// <summary>
        /// 追踪配置
        /// </summary>
        public ClientTracingConfiguration TracingConfiguration { get; set; } = new ClientTracingConfiguration();

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// 性能监控配置
        /// </summary>
        public ClientPerformanceMonitoringConfiguration PerformanceMonitoringConfiguration { get; set; } = new ClientPerformanceMonitoringConfiguration();

        /// <summary>
        /// 连接池配置
        /// </summary>
        public ConnectionPoolConfiguration ConnectionPoolConfiguration { get; set; } = new ConnectionPoolConfiguration();

        /// <summary>
        /// 序列化配置
        /// </summary>
        public SerializationConfiguration SerializationConfiguration { get; set; } = new SerializationConfiguration();

        /// <summary>
        /// 错误处理配置
        /// </summary>
        public ErrorHandlingConfiguration ErrorHandlingConfiguration { get; set; } = new ErrorHandlingConfiguration();

        /// <summary>
        /// 自定义头信息
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 扩展配置
        /// </summary>
        public Dictionary<string, object> ExtendedConfiguration { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        public static RpcClientOptions CreateDefault()
        {
            return new RpcClientOptions();
        }

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        /// <returns>高性能配置</returns>
        public static RpcClientOptions CreateHighPerformance()
        {
            return new RpcClientOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(10),
                RequestTimeout = TimeSpan.FromSeconds(5),
                EnableAutoReconnect = true,
                ReconnectInterval = TimeSpan.FromSeconds(1),
                MaxReconnectAttempts = 5,
                EnableHeartbeat = true,
                HeartbeatInterval = TimeSpan.FromSeconds(10),
                EnableCompression = true,
                CompressionLevel = CompressionLevel.Fastest,
                EnableCaching = true,
                CacheExpiration = TimeSpan.FromMinutes(30),
                EnableRequestRetry = true,
                MaxRetryAttempts = 3,
                RetryInterval = TimeSpan.FromMilliseconds(500)
            };
        }

        /// <summary>
        /// 创建安全配置
        /// </summary>
        /// <returns>安全配置</returns>
        public static RpcClientOptions CreateSecure()
        {
            return new RpcClientOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(60),
                RequestTimeout = TimeSpan.FromMinutes(5),
                EnableSsl = true,
                EnableAuthentication = true,
                EnableTracing = true,
                EnableAutoReconnect = false, // 安全模式下禁用自动重连
                EnableHeartbeat = true,
                HeartbeatInterval = TimeSpan.FromMinutes(1)
            };
        }

        /// <summary>
        /// 创建可靠配置
        /// </summary>
        /// <returns>可靠配置</returns>
        public static RpcClientOptions CreateReliable()
        {
            return new RpcClientOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromMinutes(2),
                EnableAutoReconnect = true,
                ReconnectInterval = TimeSpan.FromSeconds(10),
                MaxReconnectAttempts = 20,
                EnableHeartbeat = true,
                HeartbeatInterval = TimeSpan.FromSeconds(15),
                EnableRequestRetry = true,
                MaxRetryAttempts = 5,
                RetryInterval = TimeSpan.FromSeconds(2),
                EnablePerformanceMonitoring = true
            };
        }
    }

    /// <summary>
    /// 压缩级别
    /// </summary>
    public enum CompressionLevel
    {
        None,
        Fastest,
        Balanced,
        Optimal
    }

    /// <summary>
    /// 客户端SSL配置
    /// </summary>
    public class ClientSslConfiguration
    {
        /// <summary>
        /// 客户端证书文件路径
        /// </summary>
        public string ClientCertificateFilePath { get; set; }

        /// <summary>
        /// 客户端证书密码
        /// </summary>
        public string ClientCertificatePassword { get; set; }

        /// <summary>
        /// 是否验证服务器证书
        /// </summary>
        public bool ValidateServerCertificate { get; set; } = true;

        /// <summary>
        /// 受信任的证书颁发机构
        /// </summary>
        public string[] TrustedCertificateAuthorities { get; set; } = new string[0];

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
    /// 客户端身份验证配置
    /// </summary>
    public class ClientAuthenticationConfiguration
    {
        /// <summary>
        /// 身份验证类型
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.None;

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// API Key头名称
        /// </summary>
        public string ApiKeyHeaderName { get; set; } = "X-API-Key";

        /// <summary>
        /// JWT Token
        /// </summary>
        public string JwtToken { get; set; }

        /// <summary>
        /// JWT头名称
        /// </summary>
        public string JwtHeaderName { get; set; } = "Authorization";

        /// <summary>
        /// 自定义身份验证头
        /// </summary>
        public Dictionary<string, string> CustomAuthHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 自动刷新Token
        /// </summary>
        public bool AutoRefreshToken { get; set; } = false;

        /// <summary>
        /// Token刷新间隔
        /// </summary>
        public TimeSpan TokenRefreshInterval { get; set; } = TimeSpan.FromMinutes(50);
    }

    /// <summary>
    /// 身份验证类型
    /// </summary>
    public enum AuthenticationType
    {
        None,
        ApiKey,
        Jwt,
        Certificate,
        Custom
    }

    /// <summary>
    /// 客户端追踪配置
    /// </summary>
    public class ClientTracingConfiguration
    {
        /// <summary>
        /// 追踪ID头名称
        /// </summary>
        public string TraceIdHeaderName { get; set; } = "X-Trace-Id";

        /// <summary>
        /// 自动生成追踪ID
        /// </summary>
        public bool AutoGenerateTraceId { get; set; } = true;

        /// <summary>
        /// 追踪采样率
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// 追踪上下文传播
        /// </summary>
        public bool PropagateTraceContext { get; set; } = true;
    }

    /// <summary>
    /// 客户端性能监控配置
    /// </summary>
    public class ClientPerformanceMonitoringConfiguration
    {
        /// <summary>
        /// 是否收集延迟指标
        /// </summary>
        public bool CollectLatencyMetrics { get; set; } = true;

        /// <summary>
        /// 是否收集吞吐量指标
        /// </summary>
        public bool CollectThroughputMetrics { get; set; } = true;

        /// <summary>
        /// 是否收集错误率指标
        /// </summary>
        public bool CollectErrorRateMetrics { get; set; } = true;

        /// <summary>
        /// 指标收集间隔
        /// </summary>
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 指标保留时间
        /// </summary>
        public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// 连接池配置
    /// </summary>
    public class ConnectionPoolConfiguration
    {
        /// <summary>
        /// 是否启用连接池
        /// </summary>
        public bool EnableConnectionPool { get; set; } = false;

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// 最小连接数
        /// </summary>
        public int MinConnections { get; set; } = 1;

        /// <summary>
        /// 连接空闲超时
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 连接最大生存时间
        /// </summary>
        public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// 序列化配置
    /// </summary>
    public class SerializationConfiguration
    {
        /// <summary>
        /// 序列化格式
        /// </summary>
        public SerializationFormat Format { get; set; } = SerializationFormat.Json;

        /// <summary>
        /// 是否使用驼峰命名
        /// </summary>
        public bool UseCamelCase { get; set; } = true;

        /// <summary>
        /// 是否忽略空值
        /// </summary>
        public bool IgnoreNullValues { get; set; } = true;

        /// <summary>
        /// 日期时间格式
        /// </summary>
        public string DateTimeFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss.fffZ";

        /// <summary>
        /// 自定义转换器
        /// </summary>
        public Dictionary<Type, string> CustomConverters { get; set; } = new Dictionary<Type, string>();
    }

    /// <summary>
    /// 序列化格式
    /// </summary>
    public enum SerializationFormat
    {
        Json,
        MessagePack,
        Protobuf
    }

    /// <summary>
    /// 错误处理配置
    /// </summary>
    public class ErrorHandlingConfiguration
    {
        /// <summary>
        /// 是否自动重试临时错误
        /// </summary>
        public bool AutoRetryTransientErrors { get; set; } = true;

        /// <summary>
        /// 临时错误代码
        /// </summary>
        public HashSet<int> TransientErrorCodes { get; set; } = new HashSet<int>
        {
            -32603, // Internal error
            -32000  // Server error
        };

        /// <summary>
        /// 是否记录错误详情
        /// </summary>
        public bool LogErrorDetails { get; set; } = true;

        /// <summary>
        /// 错误回调
        /// </summary>
        public Action<Exception> ErrorCallback { get; set; }

        /// <summary>
        /// 重试策略
        /// </summary>
        public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.ExponentialBackoff;
    }

    /// <summary>
    /// 重试策略
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// 固定间隔
        /// </summary>
        FixedInterval,

        /// <summary>
        /// 指数退避
        /// </summary>
        ExponentialBackoff,

        /// <summary>
        /// 线性退避
        /// </summary>
        LinearBackoff
    }
} 