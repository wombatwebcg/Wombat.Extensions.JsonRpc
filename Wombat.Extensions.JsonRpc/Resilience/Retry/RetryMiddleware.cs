using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Middleware.Core;

namespace Wombat.Extensions.JsonRpc.Resilience.Retry
{
    /// <summary>
    /// 重试中间件
    /// 在RPC调用失败时自动重试
    /// </summary>
    public class RetryMiddleware : RpcMiddlewareBase,IDisposable
    {
        private readonly ILogger<RetryMiddleware> _logger;
        private readonly IRetryPolicy _retryPolicy;
        private readonly RetryMiddlewareOptions _options;

        public RetryMiddleware(
            ILogger<RetryMiddleware> logger = null,
            IRetryPolicy retryPolicy = null,
            RetryMiddlewareOptions options = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RetryMiddleware>.Instance;
            _options = options ?? new RetryMiddlewareOptions();
            
            // 如果没有提供重试策略，创建默认策略
            _retryPolicy = retryPolicy ?? CreateDefaultRetryPolicy();
        }

        public override async Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            // 检查是否应该对这个方法启用重试
            if (!ShouldRetry(context))
            {
                await next(context);
                return;
            }

            var operationName = $"{context.ServiceName}.{context.MethodName}";
            
            try
            {
                await _retryPolicy.ExecuteAsync(async cancellationToken =>
                {
                    // 重置上下文状态（如果这是重试）
                    context.Exception = null;
                    context.Result = null;

                    _logger.LogDebug("执行RPC操作: {OperationName}", operationName);
                    
                    await next(context);
                    
                    // 检查执行结果
                    if (context.Exception != null)
                    {
                        throw context.Exception;
                    }

                    return context.Result;
                }, context.CancellationToken);
            }
            catch (AggregateException aggregateEx)
            {
                // 解包聚合异常，获取最后一个异常作为主要异常
                var lastException = aggregateEx.InnerExceptions[aggregateEx.InnerExceptions.Count - 1];
                context.Exception = lastException;
                
                _logger.LogError(aggregateEx, "RPC操作 {OperationName} 在重试后最终失败", operationName);
                
                // 如果配置了重新抛出异常，则抛出
                if (_options.RethrowOnFailure)
                {
                    throw lastException;
                }
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                _logger.LogError(ex, "RPC操作 {OperationName} 重试过程中发生意外错误", operationName);
                
                if (_options.RethrowOnFailure)
                {
                    throw;
                }
            }
        }

        private bool ShouldRetry(RpcMiddlewareContext context)
        {
            // 检查全局启用标志
            if (!_options.EnableRetry)
                return false;

            // 检查方法级别的重试配置
            if (_options.MethodRetrySettings?.ContainsKey(context.MethodName) == true)
            {
                return _options.MethodRetrySettings[context.MethodName];
            }

            // 检查服务级别的重试配置
            if (_options.ServiceRetrySettings?.ContainsKey(context.ServiceName) == true)
            {
                return _options.ServiceRetrySettings[context.ServiceName];
            }

            // 检查排除列表
            if (_options.ExcludedMethods?.Contains(context.MethodName) == true)
                return false;

            if (_options.ExcludedServices?.Contains(context.ServiceName) == true)
                return false;

            // 默认根据全局设置
            return _options.EnableRetry;
        }

        private IRetryPolicy CreateDefaultRetryPolicy()
        {
            var retryOptions = new RetryOptions
            {
                MaxRetries = _options.DefaultMaxRetries,
                BaseDelay = _options.DefaultBaseDelay,
                MaxDelay = _options.DefaultMaxDelay,
                Strategy = _options.DefaultRetryStrategy,
                BackoffMultiplier = _options.DefaultBackoffMultiplier,
                EnableJitter = _options.DefaultEnableJitter,
                TotalTimeout = _options.DefaultTotalTimeout,
                RetryableExceptions = _options.RetryableExceptions,
                NonRetryableExceptions = _options.NonRetryableExceptions,
                RetryCondition = _options.CustomRetryCondition,
                OnRetrying = (ex, attempt, delay) =>
                {
                    _logger.LogWarning("RPC操作重试，尝试次数: {Attempt}, 延迟: {Delay}ms, 异常: {ExceptionType}", 
                        attempt, delay.TotalMilliseconds, ex.GetType().Name);
                },
                OnRetrySuccess = (attempts) =>
                {
                    if (attempts > 1)
                    {
                        _logger.LogInformation("RPC操作在第 {Attempts} 次尝试后成功", attempts);
                    }
                },
                OnRetryFailed = (ex, attempts) =>
                {
                    _logger.LogError(ex, "RPC操作在 {Attempts} 次尝试后最终失败", attempts);
                }
            };

            return new RetryPolicy(retryOptions, _logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 重试策略通常不需要显式释放，但可以在这里添加清理逻辑
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 重试中间件选项
    /// </summary>
    public class RetryMiddlewareOptions
    {
        /// <summary>
        /// 是否启用重试
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// 失败后是否重新抛出异常
        /// </summary>
        public bool RethrowOnFailure { get; set; } = true;

        /// <summary>
        /// 默认最大重试次数
        /// </summary>
        public int DefaultMaxRetries { get; set; } = 3;

        /// <summary>
        /// 默认基础延迟
        /// </summary>
        public TimeSpan DefaultBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 默认最大延迟
        /// </summary>
        public TimeSpan DefaultMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 默认总超时时间
        /// </summary>
        public TimeSpan DefaultTotalTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 默认重试策略
        /// </summary>
        public RetryStrategy DefaultRetryStrategy { get; set; } = RetryStrategy.ExponentialWithJitter;

        /// <summary>
        /// 默认退避倍数
        /// </summary>
        public double DefaultBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// 默认是否启用抖动
        /// </summary>
        public bool DefaultEnableJitter { get; set; } = true;

        /// <summary>
        /// 可重试的异常类型
        /// </summary>
        public Type[] RetryableExceptions { get; set; } = 
        {
            typeof(TimeoutException),
            typeof(TaskCanceledException),
            typeof(System.Net.Sockets.SocketException),
            typeof(System.IO.IOException)
        };

        /// <summary>
        /// 不可重试的异常类型
        /// </summary>
        public Type[] NonRetryableExceptions { get; set; } = 
        {
            typeof(ArgumentException),
            typeof(ArgumentNullException),
            typeof(NotSupportedException),
            typeof(NotImplementedException),
            typeof(UnauthorizedAccessException)
        };

        /// <summary>
        /// 自定义重试条件
        /// </summary>
        public Func<Exception, int, bool> CustomRetryCondition { get; set; }

        /// <summary>
        /// 排除的方法列表（不进行重试）
        /// </summary>
        public string[] ExcludedMethods { get; set; }

        /// <summary>
        /// 排除的服务列表（不进行重试）
        /// </summary>
        public string[] ExcludedServices { get; set; }

        /// <summary>
        /// 方法级别的重试设置
        /// 键为方法名，值为是否启用重试
        /// </summary>
        public System.Collections.Generic.Dictionary<string, bool> MethodRetrySettings { get; set; }

        /// <summary>
        /// 服务级别的重试设置
        /// 键为服务名，值为是否启用重试
        /// </summary>
        public System.Collections.Generic.Dictionary<string, bool> ServiceRetrySettings { get; set; }

        /// <summary>
        /// 方法级别的重试策略配置
        /// 键为方法名，值为重试选项
        /// </summary>
        public System.Collections.Generic.Dictionary<string, RetryOptions> MethodRetryOptions { get; set; }

        /// <summary>
        /// 服务级别的重试策略配置
        /// 键为服务名，值为重试选项
        /// </summary>
        public System.Collections.Generic.Dictionary<string, RetryOptions> ServiceRetryOptions { get; set; }
    }

    /// <summary>
    /// 重试中间件扩展方法
    /// </summary>
    public static class RetryMiddlewareExtensions
    {
        /// <summary>
        /// 添加重试中间件
        /// </summary>
        /// <param name="server">RPC服务器</param>
        /// <param name="options">重试选项</param>
        /// <returns>RPC服务器</returns>
        public static TServer UseRetry<TServer>(this TServer server, RetryMiddlewareOptions options = null)
            where TServer : class
        {
            // 这里需要根据实际的服务器接口来实现
            // 示例实现，实际需要根据RpcServer的接口调整
            
            return server;
        }

        /// <summary>
        /// 添加重试中间件（使用自定义重试策略）
        /// </summary>
        /// <param name="server">RPC服务器</param>
        /// <param name="retryPolicy">重试策略</param>
        /// <param name="options">中间件选项</param>
        /// <returns>RPC服务器</returns>
        public static TServer UseRetry<TServer>(this TServer server, IRetryPolicy retryPolicy, RetryMiddlewareOptions options = null)
            where TServer : class
        {
            // 这里需要根据实际的服务器接口来实现
            
            return server;
        }

        /// <summary>
        /// 配置方法级别的重试设置
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="methodName">方法名</param>
        /// <param name="retryOptions">重试选项</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ConfigureMethodRetry(
            this RetryMiddlewareOptions options, 
            string methodName, 
            RetryOptions retryOptions)
        {
            if (options.MethodRetryOptions == null)
                options.MethodRetryOptions = new System.Collections.Generic.Dictionary<string, RetryOptions>();
            options.MethodRetryOptions[methodName] = retryOptions;
            return options;
        }

        /// <summary>
        /// 配置服务级别的重试设置
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="retryOptions">重试选项</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ConfigureServiceRetry(
            this RetryMiddlewareOptions options, 
            string serviceName, 
            RetryOptions retryOptions)
        {
            if (options.ServiceRetryOptions == null)
                options.ServiceRetryOptions = new System.Collections.Generic.Dictionary<string, RetryOptions>();
            options.ServiceRetryOptions[serviceName] = retryOptions;
            return options;
        }

        /// <summary>
        /// 排除指定方法的重试
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="methodNames">方法名列表</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ExcludeMethods(
            this RetryMiddlewareOptions options, 
            params string[] methodNames)
        {
            var excluded = options.ExcludedMethods?.ToList();
            if (excluded == null)
                excluded = new System.Collections.Generic.List<string>();
            excluded.AddRange(methodNames);
            options.ExcludedMethods = excluded.ToArray();
            return options;
        }

        /// <summary>
        /// 排除指定服务的重试
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="serviceNames">服务名列表</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ExcludeServices(
            this RetryMiddlewareOptions options, 
            params string[] serviceNames)
        {
            var excluded = options.ExcludedServices?.ToList();
            if (excluded == null)
                excluded = new System.Collections.Generic.List<string>();
            excluded.AddRange(serviceNames);
            options.ExcludedServices = excluded.ToArray();
            return options;
        }

        /// <summary>
        /// 配置HTTP相关的重试设置
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ConfigureForHttp(
            this RetryMiddlewareOptions options,
            int maxRetries = 3)
        {
            options.DefaultMaxRetries = maxRetries;
            options.DefaultBaseDelay = TimeSpan.FromSeconds(1);
            options.DefaultMaxDelay = TimeSpan.FromSeconds(30);
            options.DefaultRetryStrategy = RetryStrategy.ExponentialWithJitter;
            options.DefaultBackoffMultiplier = 2.0;
            options.DefaultEnableJitter = true;
            
            options.RetryableExceptions = new[]
            {
                typeof(TimeoutException),
                typeof(TaskCanceledException),
                typeof(System.Net.Http.HttpRequestException),
                typeof(System.Net.Sockets.SocketException)
            };

            return options;
        }

        /// <summary>
        /// 配置数据库相关的重试设置
        /// </summary>
        /// <param name="options">中间件选项</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>中间件选项</returns>
        public static RetryMiddlewareOptions ConfigureForDatabase(
            this RetryMiddlewareOptions options,
            int maxRetries = 5)
        {
            options.DefaultMaxRetries = maxRetries;
            options.DefaultBaseDelay = TimeSpan.FromMilliseconds(500);
            options.DefaultMaxDelay = TimeSpan.FromSeconds(10);
            options.DefaultRetryStrategy = RetryStrategy.ExponentialWithJitter;
            options.DefaultBackoffMultiplier = 1.5;
            options.DefaultEnableJitter = true;

            options.CustomRetryCondition = (exception, attempt) =>
            {
                var message = exception.Message?.ToLower();
                if (message == null)
                    message = "";
                return message.Contains("timeout") ||
                       message.Contains("connection") ||
                       message.Contains("deadlock") ||
                       message.Contains("transient");
            };

            return options;
        }
    }
} 