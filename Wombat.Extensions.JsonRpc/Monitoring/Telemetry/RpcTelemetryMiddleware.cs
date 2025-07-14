using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Middleware.Core;
using Wombat.Extensions.JsonRpc.Monitoring.Core;
using System.Reflection;

namespace Wombat.Extensions.JsonRpc.Monitoring.Telemetry
{
    /// <summary>
    /// RPC遥测中间件
    /// 集成OpenTelemetry分布式追踪和性能监控
    /// </summary>
    public class RpcTelemetryMiddleware : RpcMiddlewareBase
    {
        private readonly ILogger<RpcTelemetryMiddleware> _logger;
        private readonly IRpcMetricsCollector _metricsCollector;
        private readonly RpcTelemetryOptions _options;
        private readonly ActivitySource _activitySource;

        public RpcTelemetryMiddleware(
            ILogger<RpcTelemetryMiddleware> logger = null,
            IRpcMetricsCollector metricsCollector = null,
            RpcTelemetryOptions options = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RpcTelemetryMiddleware>.Instance;
            _metricsCollector = metricsCollector ?? new RpcMetricsCollector();
            _options = options ?? new RpcTelemetryOptions();
            _activitySource = RpcActivitySource.Instance;
        }

        public override async Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            // 创建活动（Activity）用于分布式追踪
            using var activity = _activitySource.StartActivity($"RPC {context.MethodName}");
            
            // 设置活动标签
            SetActivityTags(activity, context);

            // 记录请求开始
            var requestId = _metricsCollector.RecordRequestStart(
                context.MethodName, 
                context.ServiceName, 
                context.ClientId);

            var stopwatch = Stopwatch.StartNew();
            Exception exception = null;
            object result = null;

            try
            {
                // 记录请求开始遥测
                await LogRequestStartAsync(context, requestId);

                // 调用下一个中间件
                await next();

                result = context.Result;
                
                // 记录请求成功完成
                _metricsCollector.RecordRequestComplete(
                    requestId, 
                    true, 
                    null, 
                    CalculateResponseSize(result));

                // 设置成功状态
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                exception = ex;
                
                // 记录请求失败
                _metricsCollector.RecordRequestError(requestId, ex);

                // 设置错误状态
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                throw;
            }
            finally
            {
                stopwatch.Stop();

                // 记录请求完成遥测
                await LogRequestCompleteAsync(context, requestId, stopwatch.Elapsed, exception, result);

                // 更新活动指标
                UpdateActivityMetrics(activity, stopwatch.Elapsed, exception == null);
            }
        }

        private void SetActivityTags(Activity activity, RpcMiddlewareContext context)
        {
            if (activity == null || !_options.EnableDistributedTracing) return;

            activity.SetTag("rpc.system", "jsonrpc");
            activity.SetTag("rpc.service", context.ServiceName);
            activity.SetTag("rpc.method", context.MethodName);
            activity.SetTag("rpc.client_id", context.ClientId);
            activity.SetTag("rpc.request_id", context.RequestId);
            activity.SetTag("rpc.server_name", Environment.MachineName);
            activity.SetTag("rpc.server_version", GetServerVersion());

            // 设置网络信息
            if (context.Properties.ContainsKey("transport.type"))
            {
                activity.SetTag("rpc.transport.type", context.Properties["transport.type"]);
            }

            if (context.Properties.ContainsKey("client.address"))
            {
                activity.SetTag("rpc.client.address", context.Properties["client.address"]);
            }

            if (context.Properties.ContainsKey("client.port"))
            {
                activity.SetTag("rpc.client.port", context.Properties["client.port"]);
            }

            // 设置参数信息（如果启用）
            if (_options.IncludeParameters && context.Parameters != null)
            {
                try
                {
                    var parametersJson = JsonSerializer.Serialize(context.Parameters);
                    if (parametersJson.Length <= _options.MaxParameterLength)
                    {
                        activity.SetTag("rpc.parameters", parametersJson);
                    }
                    else
                    {
                        activity.SetTag("rpc.parameters", parametersJson.Substring(0, _options.MaxParameterLength) + "...");
                    }
                }
                catch
                {
                    // 忽略序列化错误
                }
            }
        }

        private void UpdateActivityMetrics(Activity activity, TimeSpan duration, bool success)
        {
            if (activity == null || !_options.EnableDistributedTracing) return;

            activity.SetTag("rpc.response_time_ms", duration.TotalMilliseconds);
            activity.SetTag("rpc.success", success);

            // 添加性能指标
            if (duration.TotalMilliseconds > _options.SlowRequestThreshold)
            {
                activity.SetTag("rpc.slow_request", true);
            }
        }

        private async Task LogRequestStartAsync(RpcMiddlewareContext context, string requestId)
        {
            if (!_options.EnableRequestLogging) return;

            try
            {
                var logData = new Dictionary<string, object>
                {
                    ["RequestId"] = requestId,
                    ["MethodName"] = context.MethodName,
                    ["ServiceName"] = context.ServiceName,
                    ["ClientId"] = context.ClientId,
                    ["Timestamp"] = DateTime.UtcNow,
                    ["Type"] = "RequestStart"
                };

                if (_options.IncludeParameters && context.Parameters != null)
                {
                    logData["Parameters"] = context.Parameters;
                }

                if (_options.IncludeSystemInfo)
                {
                    logData["SystemInfo"] = new
                    {
                        MachineName = Environment.MachineName,
                        ProcessId = Process.GetCurrentProcess().Id,
                        ThreadId = Environment.CurrentManagedThreadId
                    };
                }

                _logger.LogInformation("RPC请求开始: {LogData}", JsonSerializer.Serialize(logData));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "记录RPC请求开始时发生错误");
            }

            await Task.CompletedTask;
        }

        private async Task LogRequestCompleteAsync(
            RpcMiddlewareContext context, 
            string requestId, 
            TimeSpan duration, 
            Exception exception, 
            object result)
        {
            if (!_options.EnableRequestLogging) return;

            try
            {
                var logData = new Dictionary<string, object>
                {
                    ["RequestId"] = requestId,
                    ["MethodName"] = context.MethodName,
                    ["ServiceName"] = context.ServiceName,
                    ["ClientId"] = context.ClientId,
                    ["Duration"] = duration.TotalMilliseconds,
                    ["Success"] = exception == null,
                    ["Timestamp"] = DateTime.UtcNow,
                    ["Type"] = "RequestComplete"
                };

                if (exception != null)
                {
                    logData["Exception"] = new
                    {
                        Type = exception.GetType().Name,
                        Message = exception.Message,
                        StackTrace = _options.IncludeStackTrace ? exception.StackTrace : null
                    };
                }

                if (_options.IncludeResponse && result != null)
                {
                    try
                    {
                        var responseJson = JsonSerializer.Serialize(result);
                        if (responseJson.Length <= _options.MaxResponseLength)
                        {
                            logData["Response"] = result;
                        }
                        else
                        {
                            logData["Response"] = responseJson.Substring(0, _options.MaxResponseLength) + "...";
                        }
                    }
                    catch
                    {
                        logData["Response"] = "[序列化失败]";
                    }
                }

                // 性能警告
                if (duration.TotalMilliseconds > _options.SlowRequestThreshold)
                {
                    logData["SlowRequest"] = true;
                    _logger.LogWarning("RPC慢请求: {LogData}", JsonSerializer.Serialize(logData));
                }
                else
                {
                    _logger.LogInformation("RPC请求完成: {LogData}", JsonSerializer.Serialize(logData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "记录RPC请求完成时发生错误");
            }

            await Task.CompletedTask;
        }

        private long CalculateResponseSize(object response)
        {
            if (response == null) return 0;

            try
            {
                var json = JsonSerializer.Serialize(response);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 0;
            }
        }

        private string GetServerVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _activitySource?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// RPC遥测配置选项
    /// </summary>
    public class RpcTelemetryOptions
    {
        /// <summary>
        /// 是否启用分布式追踪
        /// </summary>
        public bool EnableDistributedTracing { get; set; } = true;

        /// <summary>
        /// 是否启用请求日志记录
        /// </summary>
        public bool EnableRequestLogging { get; set; } = true;

        /// <summary>
        /// 是否包含请求参数
        /// </summary>
        public bool IncludeParameters { get; set; } = true;

        /// <summary>
        /// 是否包含响应数据
        /// </summary>
        public bool IncludeResponse { get; set; } = false;

        /// <summary>
        /// 是否包含系统信息
        /// </summary>
        public bool IncludeSystemInfo { get; set; } = true;

        /// <summary>
        /// 是否包含异常堆栈信息
        /// </summary>
        public bool IncludeStackTrace { get; set; } = true;

        /// <summary>
        /// 最大参数长度
        /// </summary>
        public int MaxParameterLength { get; set; } = 1000;

        /// <summary>
        /// 最大响应长度
        /// </summary>
        public int MaxResponseLength { get; set; } = 1000;

        /// <summary>
        /// 慢请求阈值（毫秒）
        /// </summary>
        public double SlowRequestThreshold { get; set; } = 1000;

        /// <summary>
        /// 采样率（0.0-1.0）
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// 自定义标签
        /// </summary>
        public Dictionary<string, object> CustomTags { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 是否启用性能计数器
        /// </summary>
        public bool EnablePerformanceCounters { get; set; } = true;

        /// <summary>
        /// 是否启用内存监控
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>
        /// 指标收集间隔（秒）
        /// </summary>
        public int MetricsCollectionInterval { get; set; } = 30;
    }
} 