using System;
using System.Collections.Generic;

namespace Wombat.Extensions.JsonRpc.Core.Client.Proxy
{
    /// <summary>
    /// 代理生成选项配置
    /// </summary>
    public class ProxyGenerationOptions
    {
        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 是否启用异常包装
        /// </summary>
        public bool EnableExceptionWrapping { get; set; } = true;

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = false;

        /// <summary>
        /// 是否启用重试机制
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// 默认超时时间（毫秒）
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 重试策略配置
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();

        /// <summary>
        /// 缓存策略配置
        /// </summary>
        public CachePolicy CachePolicy { get; set; } = new CachePolicy();

        /// <summary>
        /// 自定义拦截器类型列表
        /// </summary>
        public List<Type> InterceptorTypes { get; set; } = new List<Type>();

        /// <summary>
        /// 服务名称前缀
        /// </summary>
        public string ServiceNamePrefix { get; set; } = string.Empty;

        /// <summary>
        /// 服务名称后缀
        /// </summary>
        public string ServiceNameSuffix { get; set; } = string.Empty;

        /// <summary>
        /// 是否使用异步代理
        /// </summary>
        public bool UseAsyncProxy { get; set; } = true;

        /// <summary>
        /// 是否严格类型检查
        /// </summary>
        public bool StrictTypeChecking { get; set; } = true;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置实例</returns>
        public static ProxyGenerationOptions Default => new ProxyGenerationOptions();

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        /// <returns>高性能配置实例</returns>
        public static ProxyGenerationOptions HighPerformance => new ProxyGenerationOptions
        {
            EnableParameterValidation = false,
            EnableExceptionWrapping = false,
            EnableLogging = false,
            EnablePerformanceMonitoring = false,
            EnableRetry = false,
            DefaultTimeoutMs = 5000,
            StrictTypeChecking = false
        };

        /// <summary>
        /// 创建开发调试配置
        /// </summary>
        /// <returns>开发调试配置实例</returns>
        public static ProxyGenerationOptions Development => new ProxyGenerationOptions
        {
            EnableParameterValidation = true,
            EnableExceptionWrapping = true,
            EnableLogging = true,
            EnablePerformanceMonitoring = true,
            EnableRetry = true,
            DefaultTimeoutMs = 60000,
            StrictTypeChecking = true
        };
    }

    /// <summary>
    /// 重试策略配置
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒）
        /// </summary>
        public int RetryIntervalMs { get; set; } = 1000;

        /// <summary>
        /// 是否使用指数退避
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// 指数退避乘数
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// 最大重试间隔（毫秒）
        /// </summary>
        public int MaxRetryIntervalMs { get; set; } = 30000;

        /// <summary>
        /// 需要重试的异常类型
        /// </summary>
        public List<Type> RetryableExceptions { get; set; } = new List<Type>();

        /// <summary>
        /// 重试条件判断委托
        /// </summary>
        public Func<Exception, bool> ShouldRetry { get; set; }
    }

    /// <summary>
    /// 缓存策略配置
    /// </summary>
    public class CachePolicy
    {
        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCache { get; set; } = false;

        /// <summary>
        /// 缓存过期时间（毫秒）
        /// </summary>
        public int CacheTtlMs { get; set; } = 60000;

        /// <summary>
        /// 最大缓存条目数
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// 缓存键生成策略
        /// </summary>
        public CacheKeyStrategy KeyStrategy { get; set; } = CacheKeyStrategy.MethodNameAndParameters;

        /// <summary>
        /// 是否缓存空结果
        /// </summary>
        public bool CacheNullResults { get; set; } = false;

        /// <summary>
        /// 自定义缓存键生成器
        /// </summary>
        public Func<string, object[], string> CustomKeyGenerator { get; set; }
    }

    /// <summary>
    /// 缓存键生成策略
    /// </summary>
    public enum CacheKeyStrategy
    {
        /// <summary>
        /// 仅使用方法名
        /// </summary>
        MethodNameOnly,

        /// <summary>
        /// 方法名和参数
        /// </summary>
        MethodNameAndParameters,

        /// <summary>
        /// 方法名、参数和类型
        /// </summary>
        MethodNameParametersAndTypes,

        /// <summary>
        /// 自定义策略
        /// </summary>
        Custom
    }
} 