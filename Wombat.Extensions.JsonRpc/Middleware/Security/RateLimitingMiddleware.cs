using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Middleware.Core;

namespace Wombat.Extensions.JsonRpc.Middleware.Security
{
    /// <summary>
    /// 请求限流中间件
    /// </summary>
    [RpcMiddleware("Rate Limiting", MiddlewareOrder.RateLimiting)]
    public class RateLimitingMiddleware : RpcMiddlewareBase
    {
        private readonly RateLimitingOptions _options;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly ConcurrentDictionary<string, RateLimitingBucket> _buckets;
        private readonly Timer _cleanupTimer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">限流选项</param>
        /// <param name="logger">日志记录器</param>
        public RateLimitingMiddleware(RateLimitingOptions options, ILogger<RateLimitingMiddleware> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _buckets = new ConcurrentDictionary<string, RateLimitingBucket>();
            
            // 启动清理定时器
            _cleanupTimer = new Timer(CleanupExpiredBuckets, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 处理请求限流
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        /// <returns>任务</returns>
        public override async Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next)
        {
            try
            {
                // 检查是否启用限流
                if (!_options.EnableRateLimiting)
                {
                    await next();
                    return;
                }

                // 获取限流键
                var rateLimitKey = GetRateLimitKey(context);
                if (string.IsNullOrEmpty(rateLimitKey))
                {
                    await next();
                    return;
                }

                // 获取或创建限流桶
                var bucket = _buckets.GetOrAdd(rateLimitKey, key => new RateLimitingBucket(key, _options));

                // 检查是否超过限制
                if (!bucket.TryConsume())
                {
                    var retryAfter = bucket.GetRetryAfter();
                    _logger?.LogWarning("请求被限流，键: {Key}, 重试时间: {RetryAfter}s, 请求: {RequestId}", 
                        rateLimitKey, retryAfter, context.RequestId);

                    // 设置重试时间
                    context.SetProperty("RetryAfter", retryAfter);
                    throw new RateLimitExceededException($"请求过于频繁，请在{retryAfter}秒后重试");
                }

                // 记录请求信息
                _logger?.LogDebug("请求通过限流检查，键: {Key}, 剩余配额: {Remaining}, 请求: {RequestId}", 
                    rateLimitKey, bucket.RemainingRequests, context.RequestId);

                await next();
            }
            catch (RateLimitExceededException)
            {
                // 重新抛出限流异常
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "请求限流处理异常，请求: {RequestId}", context.RequestId);
                context.Exception = ex;
                context.ExceptionHandled = false;
                throw;
            }
        }

        /// <summary>
        /// 获取限流键
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>限流键</returns>
        private string GetRateLimitKey(RpcMiddlewareContext context)
        {
            var keyParts = new List<string>();

            // 根据配置组合键
            if (_options.KeyStrategy.HasFlag(RateLimitKeyStrategy.ClientId))
            {
                keyParts.Add($"client:{context.ClientInfo?.ClientId ?? "unknown"}");
            }

            if (_options.KeyStrategy.HasFlag(RateLimitKeyStrategy.UserId))
            {
                keyParts.Add($"user:{context.GetUserId() ?? "anonymous"}");
            }

            if (_options.KeyStrategy.HasFlag(RateLimitKeyStrategy.IpAddress))
            {
                keyParts.Add($"ip:{context.ClientInfo?.IpAddress ?? "unknown"}");
            }

            if (_options.KeyStrategy.HasFlag(RateLimitKeyStrategy.Method))
            {
                keyParts.Add($"method:{context.MethodName}");
            }

            if (_options.KeyStrategy.HasFlag(RateLimitKeyStrategy.Service))
            {
                keyParts.Add($"service:{context.ServiceMetadata?.ServiceName ?? "unknown"}");
            }

            // 使用自定义键生成器
            if (_options.CustomKeyGenerator != null)
            {
                try
                {
                    var customKey = _options.CustomKeyGenerator(context);
                    if (!string.IsNullOrEmpty(customKey))
                    {
                        keyParts.Add($"custom:{customKey}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "自定义键生成器异常");
                }
            }

            return keyParts.Count > 0 ? string.Join(":", keyParts) : null;
        }

        /// <summary>
        /// 清理过期的限流桶
        /// </summary>
        /// <param name="state">状态</param>
        private void CleanupExpiredBuckets(object state)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();

            foreach (var kvp in _buckets)
            {
                if (now - kvp.Value.LastAccessTime > TimeSpan.FromMinutes(_options.BucketCleanupIntervalMinutes))
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _buckets.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger?.LogDebug("清理过期限流桶，数量: {Count}", expiredKeys.Count);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cleanupTimer?.Dispose();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 限流选项
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>
        /// 是否启用限流
        /// </summary>
        public bool EnableRateLimiting { get; set; } = true;

        /// <summary>
        /// 限流键策略
        /// </summary>
        public RateLimitKeyStrategy KeyStrategy { get; set; } = RateLimitKeyStrategy.ClientId;

        /// <summary>
        /// 每个时间窗口的最大请求数
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// 时间窗口大小（秒）
        /// </summary>
        public int WindowSizeSeconds { get; set; } = 60;

        /// <summary>
        /// 突发请求数（令牌桶容量）
        /// </summary>
        public int BurstCapacity { get; set; } = 20;

        /// <summary>
        /// 令牌补充速率（每秒）
        /// </summary>
        public double RefillRate { get; set; } = 1.0;

        /// <summary>
        /// 限流算法
        /// </summary>
        public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;

        /// <summary>
        /// 桶清理间隔（分钟）
        /// </summary>
        public int BucketCleanupIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 自定义键生成器
        /// </summary>
        public Func<RpcMiddlewareContext, string> CustomKeyGenerator { get; set; }

        /// <summary>
        /// 方法级别的限流配置
        /// </summary>
        public Dictionary<string, RateLimitingMethodConfig> MethodConfigs { get; set; } = new Dictionary<string, RateLimitingMethodConfig>();

        /// <summary>
        /// 添加方法配置
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="maxRequests">最大请求数</param>
        /// <param name="windowSizeSeconds">时间窗口大小（秒）</param>
        /// <returns>当前实例</returns>
        public RateLimitingOptions AddMethodConfig(string methodName, int maxRequests, int windowSizeSeconds = 60)
        {
            MethodConfigs[methodName] = new RateLimitingMethodConfig
            {
                MaxRequests = maxRequests,
                WindowSizeSeconds = windowSizeSeconds
            };
            return this;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (MaxRequests <= 0)
            {
                return (false, "MaxRequests必须大于0");
            }

            if (WindowSizeSeconds <= 0)
            {
                return (false, "WindowSizeSeconds必须大于0");
            }

            if (BurstCapacity <= 0)
            {
                return (false, "BurstCapacity必须大于0");
            }

            if (RefillRate <= 0)
            {
                return (false, "RefillRate必须大于0");
            }

            return (true, null);
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>配置实例</returns>
        public static RateLimitingOptions CreateDefault()
        {
            return new RateLimitingOptions
            {
                EnableRateLimiting = true,
                KeyStrategy = RateLimitKeyStrategy.ClientId,
                MaxRequests = 100,
                WindowSizeSeconds = 60,
                BurstCapacity = 20,
                RefillRate = 1.0,
                Algorithm = RateLimitAlgorithm.TokenBucket
            };
        }
    }

    /// <summary>
    /// 方法级别的限流配置
    /// </summary>
    public class RateLimitingMethodConfig
    {
        /// <summary>
        /// 最大请求数
        /// </summary>
        public int MaxRequests { get; set; }

        /// <summary>
        /// 时间窗口大小（秒）
        /// </summary>
        public int WindowSizeSeconds { get; set; }
    }

    /// <summary>
    /// 限流键策略
    /// </summary>
    [Flags]
    public enum RateLimitKeyStrategy
    {
        /// <summary>
        /// 客户端ID
        /// </summary>
        ClientId = 1,

        /// <summary>
        /// 用户ID
        /// </summary>
        UserId = 2,

        /// <summary>
        /// IP地址
        /// </summary>
        IpAddress = 4,

        /// <summary>
        /// 方法名称
        /// </summary>
        Method = 8,

        /// <summary>
        /// 服务名称
        /// </summary>
        Service = 16
    }

    /// <summary>
    /// 限流算法
    /// </summary>
    public enum RateLimitAlgorithm
    {
        /// <summary>
        /// 固定时间窗口
        /// </summary>
        FixedWindow,

        /// <summary>
        /// 滑动时间窗口
        /// </summary>
        SlidingWindow,

        /// <summary>
        /// 令牌桶
        /// </summary>
        TokenBucket,

        /// <summary>
        /// 漏桶
        /// </summary>
        LeakyBucket
    }

    /// <summary>
    /// 限流桶
    /// </summary>
    internal class RateLimitingBucket
    {
        private readonly string _key;
        private readonly RateLimitingOptions _options;
        private readonly object _lock = new object();
        private double _tokens;
        private DateTime _lastRefillTime;

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccessTime { get; private set; }

        /// <summary>
        /// 剩余请求数
        /// </summary>
        public int RemainingRequests => (int)_tokens;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="options">选项</param>
        public RateLimitingBucket(string key, RateLimitingOptions options)
        {
            _key = key;
            _options = options;
            _tokens = options.BurstCapacity;
            _lastRefillTime = DateTime.UtcNow;
            LastAccessTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 尝试消费令牌
        /// </summary>
        /// <returns>是否成功</returns>
        public bool TryConsume()
        {
            lock (_lock)
            {
                LastAccessTime = DateTime.UtcNow;
                
                // 补充令牌
                RefillTokens();

                // 检查是否有可用令牌
                if (_tokens >= 1)
                {
                    _tokens--;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 获取重试时间
        /// </summary>
        /// <returns>重试时间（秒）</returns>
        public int GetRetryAfter()
        {
            lock (_lock)
            {
                var tokensNeeded = 1 - _tokens;
                var timeToWait = tokensNeeded / _options.RefillRate;
                return (int)Math.Ceiling(timeToWait);
            }
        }

        /// <summary>
        /// 补充令牌
        /// </summary>
        private void RefillTokens()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefillTime).TotalSeconds;
            
            if (elapsed > 0)
            {
                var tokensToAdd = elapsed * _options.RefillRate;
                _tokens = Math.Min(_options.BurstCapacity, _tokens + tokensToAdd);
                _lastRefillTime = now;
            }
        }
    }

    /// <summary>
    /// 限流异常
    /// </summary>
    public class RateLimitExceededException : Exception
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        public RateLimitExceededException(string message) : base(message)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="innerException">内部异常</param>
        public RateLimitExceededException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 