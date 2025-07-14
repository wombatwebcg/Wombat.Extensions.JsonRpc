using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Monitoring.Core
{
    /// <summary>
    /// RPC性能指标收集器实现
    /// 提供高性能、线程安全的指标收集和统计功能
    /// </summary>
    public class RpcMetricsCollector : IRpcMetricsCollector, IDisposable
    {
        private readonly ILogger<RpcMetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, RequestMetrics> _activeRequests;
        private readonly ConcurrentDictionary<string, RpcMethodMetrics> _methodMetrics;
        private readonly ConcurrentDictionary<string, RpcTransportMetrics> _transportMetrics;
        private readonly ConcurrentQueue<RpcMetricsSnapshot> _metricsHistory;
        private readonly Timer _reportingTimer;
        private readonly object _statsLock = new object();

        // 性能计数器
        private long _totalRequests;
        private long _successfulRequests;
        private long _failedRequests;
        private long _totalConnections;
        private int _activeConnections;
        private readonly ConcurrentQueue<double> _responseTimes;
        private readonly ConcurrentQueue<double> _qpsBuffer;
        private readonly ConcurrentQueue<int> _batchSizes;
        private readonly ConcurrentQueue<double> _compressionRatios;

        // 系统资源监控
        private long _memoryUsage;
        private double _cpuUsage;
        private int _threadCount;

        // 配置参数
        private TimeSpan _reportingInterval = TimeSpan.FromSeconds(30);
        private readonly int _maxHistorySize = 1000;
        private readonly int _maxBufferSize = 10000;
        private volatile bool _disposed = false;

        public event EventHandler<RpcMetricsReportEventArgs> MetricsReported;

        public RpcMetricsCollector(ILogger<RpcMetricsCollector> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RpcMetricsCollector>.Instance;
            _activeRequests = new ConcurrentDictionary<string, RequestMetrics>();
            _methodMetrics = new ConcurrentDictionary<string, RpcMethodMetrics>();
            _transportMetrics = new ConcurrentDictionary<string, RpcTransportMetrics>();
            _metricsHistory = new ConcurrentQueue<RpcMetricsSnapshot>();
            _responseTimes = new ConcurrentQueue<double>();
            _qpsBuffer = new ConcurrentQueue<double>();
            _batchSizes = new ConcurrentQueue<int>();
            _compressionRatios = new ConcurrentQueue<double>();

            // 启动定时报告
            _reportingTimer = new Timer(ReportMetrics, null, _reportingInterval, _reportingInterval);
        }

        public string RecordRequestStart(string methodName, string serviceName, string clientId = null)
        {
            if (_disposed) return null;

            var requestId = Guid.NewGuid().ToString("N");
            var metrics = new RequestMetrics
            {
                RequestId = requestId,
                MethodName = methodName,
                ServiceName = serviceName,
                ClientId = clientId,
                StartTime = DateTime.UtcNow,
                Stopwatch = Stopwatch.StartNew()
            };

            _activeRequests.TryAdd(requestId, metrics);
            Interlocked.Increment(ref _totalRequests);

            _logger?.LogDebug("记录RPC请求开始: {RequestId}, 方法: {MethodName}, 服务: {ServiceName}", 
                requestId, methodName, serviceName);

            return requestId;
        }

        public void RecordRequestComplete(string requestId, bool success, string errorCode = null, long responseSize = 0)
        {
            if (_disposed || string.IsNullOrEmpty(requestId)) return;

            if (_activeRequests.TryRemove(requestId, out var metrics))
            {
                metrics.Stopwatch.Stop();
                metrics.Success = success;
                metrics.ErrorCode = errorCode;
                metrics.ResponseSize = responseSize;
                metrics.EndTime = DateTime.UtcNow;

                // 更新统计数据
                if (success)
                {
                    Interlocked.Increment(ref _successfulRequests);
                }
                else
                {
                    Interlocked.Increment(ref _failedRequests);
                }

                // 记录响应时间
                var responseTime = metrics.Stopwatch.Elapsed.TotalMilliseconds;
                RecordResponseTime(responseTime);

                // 更新方法级别指标
                UpdateMethodMetrics(metrics, responseTime);

                _logger?.LogDebug("记录RPC请求完成: {RequestId}, 成功: {Success}, 响应时间: {ResponseTime}ms", 
                    requestId, success, responseTime);
            }
        }

        public void RecordRequestError(string requestId, Exception exception, string errorCode = null)
        {
            if (_disposed || string.IsNullOrEmpty(requestId)) return;

            if (_activeRequests.TryRemove(requestId, out var metrics))
            {
                metrics.Stopwatch.Stop();
                metrics.Success = false;
                metrics.ErrorCode = errorCode ?? exception?.GetType().Name;
                metrics.Exception = exception;
                metrics.EndTime = DateTime.UtcNow;

                Interlocked.Increment(ref _failedRequests);

                var responseTime = metrics.Stopwatch.Elapsed.TotalMilliseconds;
                RecordResponseTime(responseTime);
                UpdateMethodMetrics(metrics, responseTime);

                _logger?.LogWarning(exception, "记录RPC请求错误: {RequestId}, 错误代码: {ErrorCode}", 
                    requestId, errorCode);
            }
        }

        public void RecordConnectionMetrics(string connectionId, bool connected, string transportType)
        {
            if (_disposed) return;

            if (connected)
            {
                Interlocked.Increment(ref _totalConnections);
                Interlocked.Increment(ref _activeConnections);
            }
            else
            {
                Interlocked.Decrement(ref _activeConnections);
            }

            // 更新传输层指标
            var transportMetrics = _transportMetrics.GetOrAdd(transportType, _ => new RpcTransportMetrics
            {
                TransportType = transportType
            });

            if (connected)
            {
                lock (transportMetrics)
                {
                    transportMetrics.TotalConnections++;
                    transportMetrics.ActiveConnections++;
                }
            }
            else
            {
                lock (transportMetrics)
                {
                    transportMetrics.ActiveConnections--;
                }
            }

            _logger?.LogDebug("记录连接指标: {ConnectionId}, 连接状态: {Connected}, 传输类型: {TransportType}", 
                connectionId, connected, transportType);
        }

        public void RecordBatchMetrics(int batchSize, TimeSpan batchDuration, double compressionRatio)
        {
            if (_disposed) return;

            // 记录批处理大小
            _batchSizes.Enqueue(batchSize);
            TrimQueue(_batchSizes, _maxBufferSize);

            // 记录压缩率
            _compressionRatios.Enqueue(compressionRatio);
            TrimQueue(_compressionRatios, _maxBufferSize);

            _logger?.LogDebug("记录批处理指标: 大小: {BatchSize}, 持续时间: {Duration}ms, 压缩率: {CompressionRatio}", 
                batchSize, batchDuration.TotalMilliseconds, compressionRatio);
        }

        public void RecordResourceUsage(long memoryUsage, double cpuUsage, int threadCount)
        {
            if (_disposed) return;

            _memoryUsage = memoryUsage;
            _cpuUsage = cpuUsage;
            _threadCount = threadCount;

            _logger?.LogDebug("记录资源使用: 内存: {MemoryUsage} bytes, CPU: {CpuUsage}%, 线程: {ThreadCount}", 
                memoryUsage, cpuUsage, threadCount);
        }

        public async Task<RpcMetricsSnapshot> GetMetricsSnapshotAsync()
        {
            if (_disposed) return null;

            return await Task.Run(() =>
            {
                lock (_statsLock)
                {
                    var snapshot = new RpcMetricsSnapshot
                    {
                        TotalRequests = _totalRequests,
                        SuccessfulRequests = _successfulRequests,
                        FailedRequests = _failedRequests,
                        ActiveConnections = _activeConnections,
                        TotalConnections = _totalConnections,
                        MemoryUsage = _memoryUsage,
                        CpuUsage = _cpuUsage,
                        ThreadCount = _threadCount
                    };

                    // 计算响应时间统计
                    if (_responseTimes.Count > 0)
                    {
                        var responseTimes = _responseTimes.ToArray();
                        Array.Sort(responseTimes);
                        
                        snapshot.AverageResponseTime = responseTimes.Average();
                        snapshot.P95ResponseTime = GetPercentile(responseTimes, 0.95);
                        snapshot.P99ResponseTime = GetPercentile(responseTimes, 0.99);
                    }

                    // 计算QPS
                    if (_qpsBuffer.Count > 0)
                    {
                        snapshot.CurrentQps = _qpsBuffer.LastOrDefault();
                        snapshot.PeakQps = _qpsBuffer.Max();
                    }

                    // 计算错误率
                    if (_totalRequests > 0)
                    {
                        snapshot.ErrorRate = (_failedRequests / (double)_totalRequests) * 100;
                    }

                    // 计算批处理统计
                    if (_batchSizes.Count > 0)
                    {
                        snapshot.AverageBatchSize = _batchSizes.Average();
                    }

                    if (_compressionRatios.Count > 0)
                    {
                        snapshot.AverageCompressionRatio = _compressionRatios.Average();
                    }

                    // 复制方法级别指标
                    snapshot.MethodMetrics = _methodMetrics.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => CloneMethodMetrics(kvp.Value));

                    // 复制传输层指标
                    snapshot.TransportMetrics = _transportMetrics.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => CloneTransportMetrics(kvp.Value));

                    return snapshot;
                }
            });
        }

        public async Task<IEnumerable<RpcMetricsSnapshot>> GetMetricsHistoryAsync(DateTime startTime, DateTime endTime)
        {
            if (_disposed) return Enumerable.Empty<RpcMetricsSnapshot>();

            return await Task.Run(() =>
            {
                return _metricsHistory
                    .Where(s => s.Timestamp >= startTime && s.Timestamp <= endTime)
                    .OrderBy(s => s.Timestamp)
                    .ToList();
            });
        }

        public void ResetMetrics()
        {
            if (_disposed) return;

            lock (_statsLock)
            {
                _totalRequests = 0;
                _successfulRequests = 0;
                _failedRequests = 0;
                _totalConnections = 0;
                _activeConnections = 0;
                _memoryUsage = 0;
                _cpuUsage = 0;
                _threadCount = 0;

                _activeRequests.Clear();
                _methodMetrics.Clear();
                _transportMetrics.Clear();
                while (_responseTimes.TryDequeue(out _)) { }
                while (_qpsBuffer.TryDequeue(out _)) { }
                while (_batchSizes.TryDequeue(out _)) { }
                while (_compressionRatios.TryDequeue(out _)) { }

                while (_metricsHistory.TryDequeue(out _)) { }

                _logger?.LogInformation("RPC指标已重置");
            }
        }

        public void SetReportingInterval(TimeSpan interval)
        {
            if (_disposed) return;

            _reportingInterval = interval;
            _reportingTimer?.Change(interval, interval);
            
            _logger?.LogInformation("指标报告间隔已设置为: {Interval}", interval);
        }

        private void RecordResponseTime(double responseTime)
        {
            _responseTimes.Enqueue(responseTime);
            TrimQueue(_responseTimes, _maxBufferSize);
        }

        private void UpdateMethodMetrics(RequestMetrics request, double responseTime)
        {
            var methodMetrics = _methodMetrics.GetOrAdd(request.MethodName, _ => new RpcMethodMetrics
            {
                MethodName = request.MethodName,
                MinResponseTime = double.MaxValue
            });

            lock (methodMetrics)
            {
                methodMetrics.CallCount++;
                
                if (request.Success)
                {
                    methodMetrics.SuccessCount++;
                }
                else
                {
                    methodMetrics.FailureCount++;
                }
            }

            // 更新响应时间统计
            lock (methodMetrics)
            {
                var totalTime = methodMetrics.AverageResponseTime * (methodMetrics.CallCount - 1) + responseTime;
                methodMetrics.AverageResponseTime = totalTime / methodMetrics.CallCount;
                methodMetrics.MaxResponseTime = Math.Max(methodMetrics.MaxResponseTime, responseTime);
                methodMetrics.MinResponseTime = Math.Min(methodMetrics.MinResponseTime, responseTime);
                methodMetrics.LastCallTime = DateTime.UtcNow;
            }
        }

        private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
        {
            while (queue.Count > maxSize)
            {
                queue.TryDequeue(out _);
            }
        }

        private static double GetPercentile(double[] sortedArray, double percentile)
        {
            if (sortedArray.Length == 0) return 0;
            
            var index = (int)Math.Ceiling(sortedArray.Length * percentile) - 1;
            return sortedArray[Math.Max(0, Math.Min(index, sortedArray.Length - 1))];
        }

        private static RpcMethodMetrics CloneMethodMetrics(RpcMethodMetrics original)
        {
            return new RpcMethodMetrics
            {
                MethodName = original.MethodName,
                CallCount = original.CallCount,
                SuccessCount = original.SuccessCount,
                FailureCount = original.FailureCount,
                AverageResponseTime = original.AverageResponseTime,
                MaxResponseTime = original.MaxResponseTime,
                MinResponseTime = original.MinResponseTime,
                LastCallTime = original.LastCallTime
            };
        }

        private static RpcTransportMetrics CloneTransportMetrics(RpcTransportMetrics original)
        {
            return new RpcTransportMetrics
            {
                TransportType = original.TransportType,
                ActiveConnections = original.ActiveConnections,
                TotalConnections = original.TotalConnections,
                ConnectionFailures = original.ConnectionFailures,
                BytesSent = original.BytesSent,
                BytesReceived = original.BytesReceived,
                AverageConnectionTime = original.AverageConnectionTime,
                ConnectionTimeouts = original.ConnectionTimeouts
            };
        }

        private async void ReportMetrics(object state)
        {
            try
            {
                if (_disposed) return;

                var snapshot = await GetMetricsSnapshotAsync();
                if (snapshot == null) return;

                // 计算当前QPS
                var currentQps = CalculateCurrentQps();
                snapshot.CurrentQps = currentQps;
                _qpsBuffer.Enqueue(currentQps);
                TrimQueue(_qpsBuffer, 100);

                // 保存到历史记录
                _metricsHistory.Enqueue(snapshot);
                TrimQueue(_metricsHistory, _maxHistorySize);

                // 检测异常
                var anomalies = DetectAnomalies(snapshot);
                
                // 触发报告事件
                var eventArgs = new RpcMetricsReportEventArgs
                {
                    Snapshot = snapshot,
                    ReportType = "Scheduled",
                    HasAnomalies = anomalies.Any(),
                    Anomalies = anomalies
                };

                MetricsReported?.Invoke(this, eventArgs);

                if (anomalies.Any())
                {
                    _logger?.LogWarning("检测到指标异常: {Anomalies}", string.Join(", ", anomalies));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "指标报告过程中发生错误");
            }
        }

        private double CalculateCurrentQps()
        {
            // 简化的QPS计算，可以根据需要实现更复杂的滑动窗口计算
            var interval = _reportingInterval.TotalSeconds;
            var requestsInInterval = _totalRequests - (_metricsHistory.LastOrDefault()?.TotalRequests ?? 0);
            return requestsInInterval / interval;
        }

        private List<string> DetectAnomalies(RpcMetricsSnapshot snapshot)
        {
            var anomalies = new List<string>();

            // 检测高错误率
            if (snapshot.ErrorRate > 5)
            {
                anomalies.Add($"高错误率: {snapshot.ErrorRate:F2}%");
            }

            // 检测高响应时间
            if (snapshot.AverageResponseTime > 1000)
            {
                anomalies.Add($"高平均响应时间: {snapshot.AverageResponseTime:F2}ms");
            }

            // 检测低QPS（可能的服务异常）
            if (snapshot.CurrentQps < 1 && _totalRequests > 0)
            {
                anomalies.Add($"低QPS: {snapshot.CurrentQps:F2}");
            }

            // 检测高内存使用
            if (snapshot.MemoryUsage > 500 * 1024 * 1024) // 500MB
            {
                anomalies.Add($"高内存使用: {snapshot.MemoryUsage / (1024 * 1024):F2}MB");
            }

            // 检测高CPU使用
            if (snapshot.CpuUsage > 80)
            {
                anomalies.Add($"高CPU使用: {snapshot.CpuUsage:F2}%");
            }

            return anomalies;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _reportingTimer?.Dispose();
            
            _logger?.LogInformation("RPC指标收集器已释放");
        }

        /// <summary>
        /// 请求指标内部类
        /// </summary>
        private class RequestMetrics
        {
            public string RequestId { get; set; }
            public string MethodName { get; set; }
            public string ServiceName { get; set; }
            public string ClientId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public Stopwatch Stopwatch { get; set; }
            public bool Success { get; set; }
            public string ErrorCode { get; set; }
            public Exception Exception { get; set; }
            public long ResponseSize { get; set; }
        }
    }
} 