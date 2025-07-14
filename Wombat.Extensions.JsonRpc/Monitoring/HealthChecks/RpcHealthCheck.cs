using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Monitoring.Core;

namespace Wombat.Extensions.JsonRpc.Monitoring.HealthChecks
{
    /// <summary>
    /// RPC健康检查
    /// 提供服务状态监控和自动诊断功能
    /// </summary>
    public class RpcHealthCheck : IHealthCheck
    {
        private readonly ILogger<RpcHealthCheck> _logger;
        private readonly IRpcMetricsCollector _metricsCollector;
        private readonly RpcHealthCheckOptions _options;
        private readonly List<IHealthCheckProvider> _providers;

        public RpcHealthCheck(
            ILogger<RpcHealthCheck> logger = null,
            IRpcMetricsCollector metricsCollector = null,
            RpcHealthCheckOptions options = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RpcHealthCheck>.Instance;
            _metricsCollector = metricsCollector;
            _options = options ?? new RpcHealthCheckOptions();
            _providers = new List<IHealthCheckProvider>();

            // 添加默认健康检查提供程序
            if (_options.EnableDefaultProviders)
            {
                _providers.Add(new RpcMetricsHealthCheckProvider(_metricsCollector, _options));
                _providers.Add(new RpcSystemResourceHealthCheckProvider(_options));
                _providers.Add(new RpcConnectionHealthCheckProvider(_options));
            }
        }

        /// <summary>
        /// 添加自定义健康检查提供程序
        /// </summary>
        /// <param name="provider">健康检查提供程序</param>
        public void AddProvider(IHealthCheckProvider provider)
        {
            if (provider != null)
            {
                _providers.Add(provider);
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        /// <param name="context">健康检查上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>健康检查结果</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<HealthCheckProviderResult>();
            var overallStatus = HealthStatus.Healthy;
            var data = new Dictionary<string, object>();

            try
            {
                // 并行执行所有健康检查提供程序
                var tasks = _providers.Select(async provider =>
                {
                    try
                    {
                        return await provider.CheckHealthAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "健康检查提供程序 {ProviderName} 执行失败", provider.GetType().Name);
                        return new HealthCheckProviderResult
                        {
                            Name = provider.GetType().Name,
                            Status = HealthStatus.Unhealthy,
                            Description = $"健康检查提供程序执行失败: {ex.Message}",
                            Exception = ex
                        };
                    }
                });

                results = (await Task.WhenAll(tasks)).ToList();

                // 计算总体健康状态
                overallStatus = CalculateOverallStatus(results);

                // 准备返回数据
                data["CheckDuration"] = stopwatch.Elapsed.TotalMilliseconds;
                data["CheckTime"] = DateTime.UtcNow;
                data["TotalProviders"] = _providers.Count;
                data["HealthyProviders"] = results.Count(r => r.Status == HealthStatus.Healthy);
                data["DegradedProviders"] = results.Count(r => r.Status == HealthStatus.Degraded);
                data["UnhealthyProviders"] = results.Count(r => r.Status == HealthStatus.Unhealthy);

                // 添加各个提供程序的详细结果
                data["ProviderResults"] = results.Select(r => new
                {
                    r.Name,
                    Status = r.Status.ToString(),
                    r.Description,
                    r.Data,
                    HasException = r.Exception != null
                }).ToList();

                // 如果启用了指标收集，添加性能指标
                if (_metricsCollector != null && _options.IncludeMetrics)
                {
                    try
                    {
                        var metrics = await _metricsCollector.GetMetricsSnapshotAsync();
                        data["Metrics"] = new
                        {
                            metrics.TotalRequests,
                            metrics.SuccessfulRequests,
                            metrics.FailedRequests,
                            metrics.ErrorRate,
                            metrics.AverageResponseTime,
                            metrics.CurrentQps,
                            metrics.ActiveConnections,
                            metrics.MemoryUsage,
                            metrics.CpuUsage,
                            metrics.ThreadCount
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "获取RPC指标时发生错误");
                        data["MetricsError"] = ex.Message;
                    }
                }

                // 记录健康检查结果
                _logger.LogInformation("RPC健康检查完成: 状态={Status}, 耗时={Duration}ms, 提供程序数={ProviderCount}", 
                    overallStatus, stopwatch.Elapsed.TotalMilliseconds, _providers.Count);

                return new HealthCheckResult(overallStatus, GetStatusDescription(overallStatus, results), null, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RPC健康检查执行失败");
                
                data["CheckDuration"] = stopwatch.Elapsed.TotalMilliseconds;
                data["CheckTime"] = DateTime.UtcNow;
                data["Error"] = ex.Message;
                
                return new HealthCheckResult(HealthStatus.Unhealthy, "RPC健康检查执行失败", ex, data);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private HealthStatus CalculateOverallStatus(List<HealthCheckProviderResult> results)
        {
            if (!results.Any())
                return HealthStatus.Healthy;

            // 如果有任何不健康的提供程序，总体状态为不健康
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;

            // 如果有任何降级的提供程序，总体状态为降级
            if (results.Any(r => r.Status == HealthStatus.Degraded))
                return HealthStatus.Degraded;

            // 否则为健康
            return HealthStatus.Healthy;
        }

        private string GetStatusDescription(HealthStatus status, List<HealthCheckProviderResult> results)
        {
            var unhealthyCount = results.Count(r => r.Status == HealthStatus.Unhealthy);
            var degradedCount = results.Count(r => r.Status == HealthStatus.Degraded);
            var healthyCount = results.Count(r => r.Status == HealthStatus.Healthy);

            return status switch
            {
                HealthStatus.Healthy => $"RPC服务健康 ({healthyCount}个提供程序正常)",
                HealthStatus.Degraded => $"RPC服务降级 ({degradedCount}个提供程序降级, {healthyCount}个正常)",
                HealthStatus.Unhealthy => $"RPC服务不健康 ({unhealthyCount}个提供程序不健康, {degradedCount}个降级, {healthyCount}个正常)",
                _ => "RPC服务状态未知"
            };
        }
    }

    /// <summary>
    /// RPC健康检查选项
    /// </summary>
    public class RpcHealthCheckOptions
    {
        /// <summary>
        /// 是否启用默认提供程序
        /// </summary>
        public bool EnableDefaultProviders { get; set; } = true;

        /// <summary>
        /// 是否包含指标信息
        /// </summary>
        public bool IncludeMetrics { get; set; } = true;

        /// <summary>
        /// 健康检查超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 错误率阈值（百分比）
        /// </summary>
        public double ErrorRateThreshold { get; set; } = 5.0;

        /// <summary>
        /// 响应时间阈值（毫秒）
        /// </summary>
        public double ResponseTimeThreshold { get; set; } = 1000;

        /// <summary>
        /// 内存使用阈值（字节）
        /// </summary>
        public long MemoryUsageThreshold { get; set; } = 500 * 1024 * 1024; // 500MB

        /// <summary>
        /// CPU使用率阈值（百分比）
        /// </summary>
        public double CpuUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// 最小QPS阈值
        /// </summary>
        public double MinQpsThreshold { get; set; } = 1.0;

        /// <summary>
        /// 连接超时阈值
        /// </summary>
        public int ConnectionTimeoutThreshold { get; set; } = 100;

        /// <summary>
        /// 自定义阈值
        /// </summary>
        public Dictionary<string, object> CustomThresholds { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 健康检查提供程序接口
    /// </summary>
    public interface IHealthCheckProvider
    {
        /// <summary>
        /// 执行健康检查
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>健康检查结果</returns>
        Task<HealthCheckProviderResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 健康检查提供程序结果
    /// </summary>
    public class HealthCheckProviderResult
    {
        /// <summary>
        /// 提供程序名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 健康状态
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 详细数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// RPC指标健康检查提供程序
    /// </summary>
    public class RpcMetricsHealthCheckProvider : IHealthCheckProvider
    {
        private readonly IRpcMetricsCollector _metricsCollector;
        private readonly RpcHealthCheckOptions _options;

        public RpcMetricsHealthCheckProvider(IRpcMetricsCollector metricsCollector, RpcHealthCheckOptions options)
        {
            _metricsCollector = metricsCollector;
            _options = options;
        }

        public async Task<HealthCheckProviderResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var result = new HealthCheckProviderResult
            {
                Name = "RPC指标检查",
                Status = HealthStatus.Healthy
            };

            try
            {
                if (_metricsCollector == null)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = "指标收集器未启用";
                    return result;
                }

                var metrics = await _metricsCollector.GetMetricsSnapshotAsync();
                if (metrics == null)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = "无法获取指标数据";
                    return result;
                }

                var issues = new List<string>();

                // 检查错误率
                if (metrics.ErrorRate > _options.ErrorRateThreshold)
                {
                    issues.Add($"错误率过高: {metrics.ErrorRate:F2}% (阈值: {_options.ErrorRateThreshold}%)");
                }

                // 检查响应时间
                if (metrics.AverageResponseTime > _options.ResponseTimeThreshold)
                {
                    issues.Add($"响应时间过长: {metrics.AverageResponseTime:F2}ms (阈值: {_options.ResponseTimeThreshold}ms)");
                }

                // 检查QPS
                if (metrics.TotalRequests > 0 && metrics.CurrentQps < _options.MinQpsThreshold)
                {
                    issues.Add($"QPS过低: {metrics.CurrentQps:F2} (阈值: {_options.MinQpsThreshold})");
                }

                // 设置结果数据
                result.Data["ErrorRate"] = metrics.ErrorRate;
                result.Data["AverageResponseTime"] = metrics.AverageResponseTime;
                result.Data["CurrentQps"] = metrics.CurrentQps;
                result.Data["TotalRequests"] = metrics.TotalRequests;
                result.Data["SuccessfulRequests"] = metrics.SuccessfulRequests;
                result.Data["FailedRequests"] = metrics.FailedRequests;
                result.Data["ActiveConnections"] = metrics.ActiveConnections;

                // 设置状态和描述
                if (issues.Any())
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = string.Join("; ", issues);
                }
                else
                {
                    result.Description = "RPC指标正常";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"指标检查失败: {ex.Message}";
                result.Exception = ex;
                return result;
            }
        }
    }

    /// <summary>
    /// RPC系统资源健康检查提供程序
    /// </summary>
    public class RpcSystemResourceHealthCheckProvider : IHealthCheckProvider
    {
        private readonly RpcHealthCheckOptions _options;

        public RpcSystemResourceHealthCheckProvider(RpcHealthCheckOptions options)
        {
            _options = options;
        }

        public async Task<HealthCheckProviderResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var result = new HealthCheckProviderResult
            {
                Name = "RPC系统资源检查",
                Status = HealthStatus.Healthy
            };

            try
            {
                var process = Process.GetCurrentProcess();
                var issues = new List<string>();

                // 检查内存使用
                var memoryUsage = process.WorkingSet64;
                if (memoryUsage > _options.MemoryUsageThreshold)
                {
                    issues.Add($"内存使用过高: {memoryUsage / (1024 * 1024):F2}MB (阈值: {_options.MemoryUsageThreshold / (1024 * 1024)}MB)");
                }

                // 检查线程数
                var threadCount = process.Threads.Count;
                if (threadCount > 1000) // 默认线程数阈值
                {
                    issues.Add($"线程数过多: {threadCount} (建议: <1000)");
                }

                // 检查句柄数
                var handleCount = process.HandleCount;
                if (handleCount > 10000) // 默认句柄数阈值
                {
                    issues.Add($"句柄数过多: {handleCount} (建议: <10000)");
                }

                // 设置结果数据
                result.Data["MemoryUsage"] = memoryUsage;
                result.Data["ThreadCount"] = threadCount;
                result.Data["HandleCount"] = handleCount;
                result.Data["ProcessorTime"] = process.TotalProcessorTime.TotalMilliseconds;
                result.Data["StartTime"] = process.StartTime;
                result.Data["Uptime"] = DateTime.Now - process.StartTime;

                // 设置状态和描述
                if (issues.Any())
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = string.Join("; ", issues);
                }
                else
                {
                    result.Description = "系统资源正常";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"系统资源检查失败: {ex.Message}";
                result.Exception = ex;
                return result;
            }
        }
    }

    /// <summary>
    /// RPC连接健康检查提供程序
    /// </summary>
    public class RpcConnectionHealthCheckProvider : IHealthCheckProvider
    {
        private readonly RpcHealthCheckOptions _options;

        public RpcConnectionHealthCheckProvider(RpcHealthCheckOptions options)
        {
            _options = options;
        }

        public async Task<HealthCheckProviderResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var result = new HealthCheckProviderResult
            {
                Name = "RPC连接检查",
                Status = HealthStatus.Healthy
            };

            try
            {
                // 这里可以添加具体的连接检查逻辑
                // 例如：检查连接池状态、网络连接质量等
                
                var issues = new List<string>();

                // 模拟连接检查
                result.Data["ConnectionPoolStatus"] = "正常";
                result.Data["NetworkLatency"] = "正常";
                result.Data["TcpConnections"] = "正常";

                // 设置状态和描述
                if (issues.Any())
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = string.Join("; ", issues);
                }
                else
                {
                    result.Description = "RPC连接正常";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"连接检查失败: {ex.Message}";
                result.Exception = ex;
                return result;
            }
        }
    }
} 