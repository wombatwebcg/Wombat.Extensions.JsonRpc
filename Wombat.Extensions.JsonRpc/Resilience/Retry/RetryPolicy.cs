using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Resilience.Retry
{
    /// <summary>
    /// 重试策略接口
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// 执行带重试的操作
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行带重试的操作（无返回值）
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取重试统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        RetryStatistics GetStatistics();
    }

    /// <summary>
    /// 重试策略实现
    /// 支持多种重试算法和配置选项
    /// </summary>
    public class RetryPolicy : IRetryPolicy
    {
        private readonly ILogger<RetryPolicy> _logger;
        private readonly RetryOptions _options;
        private long _totalAttempts = 0;
        private long _totalRetries = 0;
        private long _successfulOperations = 0;
        private long _failedOperations = 0;
        private readonly object _statsLock = new object();

        public RetryPolicy(RetryOptions options, ILogger<RetryPolicy> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RetryPolicy>.Instance;
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            var attempt = 0;
            var exceptions = new List<Exception>();
            var startTime = DateTime.UtcNow;

            while (attempt <= _options.MaxRetries)
            {
                Interlocked.Increment(ref _totalAttempts);
                
                try
                {
                    _logger.LogDebug("执行操作，尝试次数: {Attempt}/{MaxRetries}", attempt + 1, _options.MaxRetries + 1);
                    
                    var result = await operation(cancellationToken);
                    
                    if (attempt > 0)
                    {
                        Interlocked.Add(ref _totalRetries, attempt);
                        _logger.LogInformation("操作在第 {Attempt} 次尝试后成功", attempt + 1);
                    }
                    
                    Interlocked.Increment(ref _successfulOperations);
                    return result;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    attempt++;

                    // 检查是否应该重试这个异常
                    if (!ShouldRetry(ex, attempt))
                    {
                        _logger.LogWarning("异常 {ExceptionType} 不符合重试条件，停止重试", ex.GetType().Name);
                        break;
                    }

                    // 检查是否超过最大重试次数
                    if (attempt > _options.MaxRetries)
                    {
                        _logger.LogWarning("已达到最大重试次数 {MaxRetries}，停止重试", _options.MaxRetries);
                        break;
                    }

                    // 检查是否超过总超时时间
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed >= _options.TotalTimeout)
                    {
                        _logger.LogWarning("已超过总超时时间 {TotalTimeout}，停止重试", _options.TotalTimeout);
                        break;
                    }

                    // 计算延迟时间
                    var delay = CalculateDelay(attempt);
                    
                    _logger.LogWarning("操作第 {Attempt} 次尝试失败，异常: {ExceptionType}，{Delay}ms 后重试", 
                        attempt, ex.GetType().Name, delay.TotalMilliseconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("重试被取消");
                        throw;
                    }
                }
            }

            // 所有重试都失败了
            Interlocked.Increment(ref _failedOperations);
            if (attempt > 1)
            {
                Interlocked.Add(ref _totalRetries, attempt - 1);
            }

            var aggregateException = new AggregateException("所有重试尝试都失败了", exceptions);
            _logger.LogError(aggregateException, "操作在 {Attempts} 次尝试后最终失败", attempt);
            
            throw aggregateException;
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async token =>
            {
                await operation(token);
                return true; // 返回虚拟值
            }, cancellationToken);
        }

        public RetryStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                var avgRetriesPerOperation = _successfulOperations + _failedOperations > 0 
                    ? _totalRetries / (double)(_successfulOperations + _failedOperations) 
                    : 0;

                var successRate = _totalAttempts > 0 
                    ? (_successfulOperations / (double)(_successfulOperations + _failedOperations)) * 100 
                    : 0;

                return new RetryStatistics
                {
                    TotalAttempts = _totalAttempts,
                    TotalRetries = _totalRetries,
                    SuccessfulOperations = _successfulOperations,
                    FailedOperations = _failedOperations,
                    AverageRetriesPerOperation = avgRetriesPerOperation,
                    SuccessRate = successRate,
                    Configuration = new RetryConfiguration
                    {
                        MaxRetries = _options.MaxRetries,
                        BaseDelay = _options.BaseDelay,
                        MaxDelay = _options.MaxDelay,
                        Strategy = _options.Strategy.ToString(),
                        BackoffMultiplier = _options.BackoffMultiplier,
                        JitterEnabled = _options.EnableJitter
                    }
                };
            }
        }

        private bool ShouldRetry(Exception exception, int attemptNumber)
        {
            // 检查是否超过最大重试次数
            if (attemptNumber > _options.MaxRetries)
                return false;

            // 检查特定的不可重试异常
            if (_options.NonRetryableExceptions?.Any(t => t.IsAssignableFrom(exception.GetType())) == true)
                return false;

            // 检查可重试异常列表
            if (_options.RetryableExceptions?.Any() == true)
            {
                return _options.RetryableExceptions.Any(t => t.IsAssignableFrom(exception.GetType()));
            }

            // 自定义重试条件
            if (_options.RetryCondition != null)
            {
                return _options.RetryCondition(exception, attemptNumber);
            }

            // 默认重试所有异常（除了一些系统异常）
            return !(exception is ArgumentException || 
                     exception is ArgumentNullException || 
                     exception is NotSupportedException ||
                     exception is NotImplementedException);
        }

        private TimeSpan CalculateDelay(int attemptNumber)
        {
            TimeSpan delay;

            switch (_options.Strategy)
            {
                case RetryStrategy.Fixed:
                    delay = _options.BaseDelay;
                    break;

                case RetryStrategy.Linear:
                    delay = TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * attemptNumber);
                    break;

                case RetryStrategy.Exponential:
                    var exponentialDelay = _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attemptNumber - 1);
                    delay = TimeSpan.FromMilliseconds(exponentialDelay);
                    break;

                case RetryStrategy.ExponentialWithJitter:
                    var baseExponentialDelay = _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attemptNumber - 1);
                    if (_options.EnableJitter)
                    {
                        var jitter = new Random().NextDouble() * 0.1 + 0.9; // 90%-100%
                        baseExponentialDelay *= jitter;
                    }
                    delay = TimeSpan.FromMilliseconds(baseExponentialDelay);
                    break;

                case RetryStrategy.Custom:
                    delay = _options.CustomDelayCalculator?.Invoke(attemptNumber) ?? _options.BaseDelay;
                    break;

                default:
                    delay = _options.BaseDelay;
                    break;
            }

            // 应用最大延迟限制
            if (delay > _options.MaxDelay)
            {
                delay = _options.MaxDelay;
            }

            // 应用抖动（如果启用且不是ExponentialWithJitter策略）
            if (_options.EnableJitter && _options.Strategy != RetryStrategy.ExponentialWithJitter)
            {
                var jitterMs = delay.TotalMilliseconds * (new Random().NextDouble() * 0.2 - 0.1); // ±10%
                delay = TimeSpan.FromMilliseconds(Math.Max(0, delay.TotalMilliseconds + jitterMs));
            }

            return delay;
        }
    }

    /// <summary>
    /// 重试策略选项
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 基础延迟时间
        /// </summary>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 最大延迟时间
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 总超时时间
        /// </summary>
        public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 重试策略
        /// </summary>
        public RetryStrategy Strategy { get; set; } = RetryStrategy.Exponential;

        /// <summary>
        /// 退避倍数（用于指数退避）
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// 是否启用抖动
        /// </summary>
        public bool EnableJitter { get; set; } = true;

        /// <summary>
        /// 可重试的异常类型
        /// </summary>
        public Type[] RetryableExceptions { get; set; }

        /// <summary>
        /// 不可重试的异常类型
        /// </summary>
        public Type[] NonRetryableExceptions { get; set; } = 
        {
            typeof(ArgumentException),
            typeof(ArgumentNullException),
            typeof(NotSupportedException),
            typeof(NotImplementedException)
        };

        /// <summary>
        /// 自定义重试条件
        /// </summary>
        public Func<Exception, int, bool> RetryCondition { get; set; }

        /// <summary>
        /// 自定义延迟计算器
        /// </summary>
        public Func<int, TimeSpan> CustomDelayCalculator { get; set; }

        /// <summary>
        /// 重试前的回调
        /// </summary>
        public Action<Exception, int, TimeSpan> OnRetrying { get; set; }

        /// <summary>
        /// 重试成功的回调
        /// </summary>
        public Action<int> OnRetrySuccess { get; set; }

        /// <summary>
        /// 重试失败的回调
        /// </summary>
        public Action<Exception, int> OnRetryFailed { get; set; }
    }

    /// <summary>
    /// 重试策略枚举
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// 固定延迟
        /// </summary>
        Fixed,

        /// <summary>
        /// 线性递增延迟
        /// </summary>
        Linear,

        /// <summary>
        /// 指数退避
        /// </summary>
        Exponential,

        /// <summary>
        /// 带抖动的指数退避
        /// </summary>
        ExponentialWithJitter,

        /// <summary>
        /// 自定义策略
        /// </summary>
        Custom
    }

    /// <summary>
    /// 重试统计信息
    /// </summary>
    public class RetryStatistics
    {
        /// <summary>
        /// 总尝试次数
        /// </summary>
        public long TotalAttempts { get; set; }

        /// <summary>
        /// 总重试次数
        /// </summary>
        public long TotalRetries { get; set; }

        /// <summary>
        /// 成功操作数
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// 失败操作数
        /// </summary>
        public long FailedOperations { get; set; }

        /// <summary>
        /// 平均每个操作的重试次数
        /// </summary>
        public double AverageRetriesPerOperation { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 配置信息
        /// </summary>
        public RetryConfiguration Configuration { get; set; }
    }

    /// <summary>
    /// 重试配置信息
    /// </summary>
    public class RetryConfiguration
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// 基础延迟时间
        /// </summary>
        public TimeSpan BaseDelay { get; set; }

        /// <summary>
        /// 最大延迟时间
        /// </summary>
        public TimeSpan MaxDelay { get; set; }

        /// <summary>
        /// 策略名称
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// 退避倍数
        /// </summary>
        public double BackoffMultiplier { get; set; }

        /// <summary>
        /// 是否启用抖动
        /// </summary>
        public bool JitterEnabled { get; set; }
    }

    /// <summary>
    /// 重试策略工厂
    /// </summary>
    public static class RetryPolicyFactory
    {
        /// <summary>
        /// 创建固定延迟重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateFixed(int maxRetries, TimeSpan delay, ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                BaseDelay = delay,
                Strategy = RetryStrategy.Fixed,
                EnableJitter = false
            };
            return new RetryPolicy(options, logger);
        }

        /// <summary>
        /// 创建指数退避重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="baseDelay">基础延迟</param>
        /// <param name="maxDelay">最大延迟</param>
        /// <param name="backoffMultiplier">退避倍数</param>
        /// <param name="enableJitter">是否启用抖动</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateExponential(
            int maxRetries, 
            TimeSpan baseDelay, 
            TimeSpan? maxDelay = null, 
            double backoffMultiplier = 2.0,
            bool enableJitter = true, 
            ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                BaseDelay = baseDelay,
                MaxDelay = maxDelay ?? TimeSpan.FromMinutes(1),
                Strategy = enableJitter ? RetryStrategy.ExponentialWithJitter : RetryStrategy.Exponential,
                BackoffMultiplier = backoffMultiplier,
                EnableJitter = enableJitter
            };
            return new RetryPolicy(options, logger);
        }

        /// <summary>
        /// 创建线性延迟重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="baseDelay">基础延迟</param>
        /// <param name="maxDelay">最大延迟</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateLinear(
            int maxRetries, 
            TimeSpan baseDelay, 
            TimeSpan? maxDelay = null, 
            ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                BaseDelay = baseDelay,
                MaxDelay = maxDelay ?? TimeSpan.FromMinutes(1),
                Strategy = RetryStrategy.Linear,
                EnableJitter = true
            };
            return new RetryPolicy(options, logger);
        }

        /// <summary>
        /// 创建自定义重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayCalculator">延迟计算器</param>
        /// <param name="retryCondition">重试条件</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateCustom(
            int maxRetries,
            Func<int, TimeSpan> delayCalculator,
            Func<Exception, int, bool> retryCondition = null,
            ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                Strategy = RetryStrategy.Custom,
                CustomDelayCalculator = delayCalculator,
                RetryCondition = retryCondition
            };
            return new RetryPolicy(options, logger);
        }

        /// <summary>
        /// 创建HTTP重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateForHttp(int maxRetries = 3, ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                BaseDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                Strategy = RetryStrategy.ExponentialWithJitter,
                BackoffMultiplier = 2.0,
                EnableJitter = true,
                RetryableExceptions = new[]
                {
                    typeof(TimeoutException),
                    typeof(TaskCanceledException),
                    typeof(System.Net.Http.HttpRequestException)
                },
                NonRetryableExceptions = new[]
                {
                    typeof(ArgumentException),
                    typeof(ArgumentNullException),
                    typeof(NotSupportedException),
                    typeof(UnauthorizedAccessException)
                }
            };
            return new RetryPolicy(options, logger);
        }

        /// <summary>
        /// 创建数据库重试策略
        /// </summary>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="logger">日志器</param>
        /// <returns>重试策略</returns>
        public static IRetryPolicy CreateForDatabase(int maxRetries = 5, ILogger<RetryPolicy> logger = null)
        {
            var options = new RetryOptions
            {
                MaxRetries = maxRetries,
                BaseDelay = TimeSpan.FromMilliseconds(500),
                MaxDelay = TimeSpan.FromSeconds(10),
                Strategy = RetryStrategy.ExponentialWithJitter,
                BackoffMultiplier = 1.5,
                EnableJitter = true,
                RetryCondition = (exception, attempt) =>
                {
                    // 数据库特定的重试逻辑
                    var message = exception.Message?.ToLower() ?? "";
                    return message.Contains("timeout") || 
                           message.Contains("connection") || 
                           message.Contains("deadlock") ||
                           message.Contains("transient");
                }
            };
            return new RetryPolicy(options, logger);
        }
    }
} 