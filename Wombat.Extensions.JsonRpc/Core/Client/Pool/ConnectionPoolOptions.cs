using System;
using System.Collections.Generic;

namespace Wombat.Extensions.JsonRpc.Core.Client.Pool
{
    /// <summary>
    /// 连接池配置选项
    /// </summary>
    public class ConnectionPoolOptions
    {
        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections { get; set; } = 100;

        /// <summary>
        /// 最小连接数
        /// </summary>
        public int MinConnections { get; set; } = 1;

        /// <summary>
        /// 每个端点的最大连接数
        /// </summary>
        public int MaxConnectionsPerEndpoint { get; set; } = 10;

        /// <summary>
        /// 每个端点的最小连接数
        /// </summary>
        public int MinConnectionsPerEndpoint { get; set; } = 1;

        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 连接空闲超时时间（毫秒）
        /// </summary>
        public int IdleTimeoutMs { get; set; } = 300000; // 5分钟

        /// <summary>
        /// 连接最大生存时间（毫秒）
        /// </summary>
        public int MaxLifetimeMs { get; set; } = 3600000; // 1小时

        /// <summary>
        /// 获取连接等待超时时间（毫秒）
        /// </summary>
        public int AcquireTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// 连接验证间隔时间（毫秒）
        /// </summary>
        public int ValidationIntervalMs { get; set; } = 60000; // 1分钟

        /// <summary>
        /// 清理过期连接的间隔时间（毫秒）
        /// </summary>
        public int CleanupIntervalMs { get; set; } = 300000; // 5分钟

        /// <summary>
        /// 是否启用连接预热
        /// </summary>
        public bool EnablePrewarming { get; set; } = true;

        /// <summary>
        /// 是否启用连接健康检查
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// 是否启用连接统计
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// 是否启用连接池监控
        /// </summary>
        public bool EnableMonitoring { get; set; } = false;

        /// <summary>
        /// 连接重试次数
        /// </summary>
        public int ConnectionRetryCount { get; set; } = 3;

        /// <summary>
        /// 连接重试间隔时间（毫秒）
        /// </summary>
        public int ConnectionRetryIntervalMs { get; set; } = 1000;

        /// <summary>
        /// 是否使用指数退避重试
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// 指数退避乘数
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// 连接池策略
        /// </summary>
        public ConnectionPoolStrategy Strategy { get; set; } = ConnectionPoolStrategy.FIFO;

        /// <summary>
        /// 连接优先级策略
        /// </summary>
        public ConnectionPriorityStrategy PriorityStrategy { get; set; } = ConnectionPriorityStrategy.LeastUsed;

        /// <summary>
        /// 自定义连接工厂
        /// </summary>
        public Func<ConnectionEndpoint, IPooledConnection> ConnectionFactory { get; set; }

        /// <summary>
        /// 自定义连接验证器
        /// </summary>
        public Func<IPooledConnection, bool> ConnectionValidator { get; set; }

        /// <summary>
        /// 连接池事件处理器
        /// </summary>
        public Dictionary<string, EventHandler<ConnectionPoolEventArgs>> EventHandlers { get; set; } = 
            new Dictionary<string, EventHandler<ConnectionPoolEventArgs>>();

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        public static ConnectionPoolOptions Default => new ConnectionPoolOptions();

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        /// <returns>高性能配置</returns>
        public static ConnectionPoolOptions HighPerformance => new ConnectionPoolOptions
        {
            MaxConnections = 1000,
            MinConnections = 10,
            MaxConnectionsPerEndpoint = 100,
            MinConnectionsPerEndpoint = 5,
            ConnectionTimeoutMs = 5000,
            IdleTimeoutMs = 60000,
            MaxLifetimeMs = 600000,
            AcquireTimeoutMs = 1000,
            ValidationIntervalMs = 30000,
            CleanupIntervalMs = 60000,
            EnablePrewarming = true,
            EnableHealthCheck = false,
            EnableStatistics = false,
            EnableMonitoring = false,
            Strategy = ConnectionPoolStrategy.LIFO,
            PriorityStrategy = ConnectionPriorityStrategy.LeastUsed
        };

        /// <summary>
        /// 创建可靠性配置
        /// </summary>
        /// <returns>可靠性配置</returns>
        public static ConnectionPoolOptions Reliability => new ConnectionPoolOptions
        {
            MaxConnections = 50,
            MinConnections = 5,
            MaxConnectionsPerEndpoint = 20,
            MinConnectionsPerEndpoint = 2,
            ConnectionTimeoutMs = 60000,
            IdleTimeoutMs = 600000,
            MaxLifetimeMs = 7200000,
            AcquireTimeoutMs = 30000,
            ValidationIntervalMs = 30000,
            CleanupIntervalMs = 120000,
            EnablePrewarming = true,
            EnableHealthCheck = true,
            EnableStatistics = true,
            EnableMonitoring = true,
            ConnectionRetryCount = 5,
            ConnectionRetryIntervalMs = 2000,
            UseExponentialBackoff = true,
            BackoffMultiplier = 2.0,
            Strategy = ConnectionPoolStrategy.FIFO,
            PriorityStrategy = ConnectionPriorityStrategy.HealthFirst
        };

        /// <summary>
        /// 创建开发调试配置
        /// </summary>
        /// <returns>开发调试配置</returns>
        public static ConnectionPoolOptions Development => new ConnectionPoolOptions
        {
            MaxConnections = 10,
            MinConnections = 1,
            MaxConnectionsPerEndpoint = 5,
            MinConnectionsPerEndpoint = 1,
            ConnectionTimeoutMs = 30000,
            IdleTimeoutMs = 300000,
            MaxLifetimeMs = 1800000,
            AcquireTimeoutMs = 10000,
            ValidationIntervalMs = 60000,
            CleanupIntervalMs = 300000,
            EnablePrewarming = false,
            EnableHealthCheck = true,
            EnableStatistics = true,
            EnableMonitoring = true,
            ConnectionRetryCount = 3,
            ConnectionRetryIntervalMs = 1000,
            UseExponentialBackoff = true,
            BackoffMultiplier = 2.0,
            Strategy = ConnectionPoolStrategy.FIFO,
            PriorityStrategy = ConnectionPriorityStrategy.LeastUsed
        };

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (MaxConnections <= 0)
                result.Errors.Add("MaxConnections必须大于0");

            if (MinConnections < 0)
                result.Errors.Add("MinConnections不能小于0");

            if (MinConnections > MaxConnections)
                result.Errors.Add("MinConnections不能大于MaxConnections");

            if (MaxConnectionsPerEndpoint <= 0)
                result.Errors.Add("MaxConnectionsPerEndpoint必须大于0");

            if (MinConnectionsPerEndpoint < 0)
                result.Errors.Add("MinConnectionsPerEndpoint不能小于0");

            if (MinConnectionsPerEndpoint > MaxConnectionsPerEndpoint)
                result.Errors.Add("MinConnectionsPerEndpoint不能大于MaxConnectionsPerEndpoint");

            if (ConnectionTimeoutMs <= 0)
                result.Errors.Add("ConnectionTimeoutMs必须大于0");

            if (IdleTimeoutMs <= 0)
                result.Errors.Add("IdleTimeoutMs必须大于0");

            if (MaxLifetimeMs <= 0)
                result.Errors.Add("MaxLifetimeMs必须大于0");

            if (AcquireTimeoutMs <= 0)
                result.Errors.Add("AcquireTimeoutMs必须大于0");

            if (ValidationIntervalMs <= 0)
                result.Errors.Add("ValidationIntervalMs必须大于0");

            if (CleanupIntervalMs <= 0)
                result.Errors.Add("CleanupIntervalMs必须大于0");

            if (ConnectionRetryCount < 0)
                result.Errors.Add("ConnectionRetryCount不能小于0");

            if (ConnectionRetryIntervalMs <= 0)
                result.Errors.Add("ConnectionRetryIntervalMs必须大于0");

            if (BackoffMultiplier <= 1.0)
                result.Errors.Add("BackoffMultiplier必须大于1.0");

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
    }

    /// <summary>
    /// 连接池策略
    /// </summary>
    public enum ConnectionPoolStrategy
    {
        /// <summary>
        /// 先进先出
        /// </summary>
        FIFO,

        /// <summary>
        /// 后进先出
        /// </summary>
        LIFO,

        /// <summary>
        /// 最少使用
        /// </summary>
        LeastUsed,

        /// <summary>
        /// 最近使用
        /// </summary>
        MostRecentlyUsed,

        /// <summary>
        /// 随机选择
        /// </summary>
        Random
    }

    /// <summary>
    /// 连接优先级策略
    /// </summary>
    public enum ConnectionPriorityStrategy
    {
        /// <summary>
        /// 最少使用优先
        /// </summary>
        LeastUsed,

        /// <summary>
        /// 最新创建优先
        /// </summary>
        NewestFirst,

        /// <summary>
        /// 最旧创建优先
        /// </summary>
        OldestFirst,

        /// <summary>
        /// 健康状态优先
        /// </summary>
        HealthFirst,

        /// <summary>
        /// 随机优先
        /// </summary>
        Random
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }
} 