using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Monitoring.Core;
using System.Runtime;

namespace Wombat.Extensions.JsonRpc.Monitoring.Diagnostics
{
    /// <summary>
    /// RPC诊断观察者
    /// 提供性能诊断和系统监控功能
    /// </summary>
    public class RpcDiagnosticObserver : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly ILogger<RpcDiagnosticObserver> _logger;
        private readonly IRpcMetricsCollector _metricsCollector;
        private readonly RpcDiagnosticOptions _options;
        private readonly ConcurrentDictionary<string, IDisposable> _subscriptions;
        private readonly ConcurrentDictionary<string, RpcDiagnosticData> _diagnosticData;
        private readonly Timer _reportTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private volatile bool _disposed = false;

        public RpcDiagnosticObserver(
            ILogger<RpcDiagnosticObserver> logger = null,
            IRpcMetricsCollector metricsCollector = null,
            RpcDiagnosticOptions options = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RpcDiagnosticObserver>.Instance;
            _metricsCollector = metricsCollector;
            _options = options ?? new RpcDiagnosticOptions();
            _subscriptions = new ConcurrentDictionary<string, IDisposable>();
            _diagnosticData = new ConcurrentDictionary<string, RpcDiagnosticData>();

            // 初始化性能计数器
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法初始化性能计数器");
            }

            // 启动诊断监听器
            DiagnosticListener.AllListeners.Subscribe(this);

            // 启动定时报告
            if (_options.EnablePeriodicReporting)
            {
                _reportTimer = new Timer(ReportDiagnostics, null, _options.ReportInterval, _options.ReportInterval);
            }
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (_disposed) return;

            // 订阅RPC相关的诊断事件
            if (listener.Name.StartsWith("Wombat.Extensions.JsonRpc") || 
                listener.Name.StartsWith("Microsoft.AspNetCore") ||
                listener.Name.StartsWith("System.Net.Http") ||
                listener.Name.StartsWith("System.Threading.Tasks"))
            {
                var subscription = listener.Subscribe(new RpcDiagnosticSubscriber(_logger, _metricsCollector, _options));
                _subscriptions.TryAdd(listener.Name, subscription);
                
                _logger.LogDebug("订阅诊断监听器: {ListenerName}", listener.Name);
            }
        }

        public void OnError(Exception error)
        {
            _logger.LogError(error, "诊断监听器发生错误");
        }

        public void OnCompleted()
        {
            _logger.LogInformation("诊断监听器完成");
        }

        /// <summary>
        /// 获取诊断数据快照
        /// </summary>
        /// <returns>诊断数据快照</returns>
        public async Task<RpcDiagnosticSnapshot> GetDiagnosticSnapshotAsync()
        {
            if (_disposed) return null;

            return await Task.Run(() =>
            {
                var snapshot = new RpcDiagnosticSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ProcessId = Process.GetCurrentProcess().Id,
                    MachineName = Environment.MachineName,
                    ThreadCount = Process.GetCurrentProcess().Threads.Count,
                    HandleCount = Process.GetCurrentProcess().HandleCount,
                    WorkingSet = Process.GetCurrentProcess().WorkingSet64,
                    VirtualMemorySize = Process.GetCurrentProcess().VirtualMemorySize64,
                    PagedMemorySize = Process.GetCurrentProcess().PagedMemorySize64,
                    ProcessorTime = Process.GetCurrentProcess().TotalProcessorTime,
                    StartTime = Process.GetCurrentProcess().StartTime,
                    Uptime = DateTime.Now - Process.GetCurrentProcess().StartTime
                };

                // 添加性能计数器数据
                if (_cpuCounter != null)
                {
                    try
                    {
                        snapshot.CpuUsage = _cpuCounter.NextValue();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "无法获取CPU使用率");
                    }
                }

                if (_memoryCounter != null)
                {
                    try
                    {
                        snapshot.AvailableMemory = (long)_memoryCounter.NextValue() * 1024 * 1024; // MB to bytes
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "无法获取可用内存");
                    }
                }

                // 添加诊断数据
                snapshot.DiagnosticData = _diagnosticData.Values.ToList();

                // 添加GC信息
                snapshot.GcInfo = new GcInfo
                {
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    TotalMemory = GC.GetTotalMemory(false),
                    IsServerGC = GCSettings.IsServerGC
                };

                // 添加线程池信息
                ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);

                snapshot.ThreadPoolInfo = new ThreadPoolInfo
                {
                    AvailableWorkerThreads = workerThreads,
                    AvailableCompletionPortThreads = completionPortThreads,
                    MaxWorkerThreads = maxWorkerThreads,
                    MaxCompletionPortThreads = maxCompletionPortThreads,
                    MinWorkerThreads = minWorkerThreads,
                    MinCompletionPortThreads = minCompletionPortThreads
                };

                return snapshot;
            });
        }

        /// <summary>
        /// 记录诊断数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="data">数据</param>
        public void RecordDiagnosticData(string key, object data)
        {
            if (_disposed || string.IsNullOrEmpty(key)) return;

            var diagnosticData = new RpcDiagnosticData
            {
                Key = key,
                Data = data,
                Timestamp = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId
            };

            _diagnosticData.AddOrUpdate(key, diagnosticData, (k, v) => diagnosticData);
        }

        /// <summary>
        /// 开始性能分析
        /// </summary>
        /// <param name="name">分析名称</param>
        /// <returns>性能分析器</returns>
        public IDisposable StartProfiling(string name)
        {
            if (_disposed) return new NullDisposable();

            return new RpcProfiler(name, this, _logger);
        }

        /// <summary>
        /// 检测性能异常
        /// </summary>
        /// <returns>异常检测结果</returns>
        public async Task<List<RpcPerformanceAnomaly>> DetectPerformanceAnomaliesAsync()
        {
            if (_disposed) return new List<RpcPerformanceAnomaly>();

            var anomalies = new List<RpcPerformanceAnomaly>();

            try
            {
                var snapshot = await GetDiagnosticSnapshotAsync();
                if (snapshot == null) return anomalies;

                // 检测高内存使用
                if (snapshot.WorkingSet > _options.MemoryThreshold)
                {
                    anomalies.Add(new RpcPerformanceAnomaly
                    {
                        Type = "高内存使用",
                        Description = $"工作集内存使用过高: {snapshot.WorkingSet / (1024 * 1024):F2}MB (阈值: {_options.MemoryThreshold / (1024 * 1024)}MB)",
                        Severity = AnomalySeverity.Warning,
                        Timestamp = DateTime.UtcNow,
                        Data = new { snapshot.WorkingSet, Threshold = _options.MemoryThreshold }
                    });
                }

                // 检测高CPU使用
                if (snapshot.CpuUsage > _options.CpuThreshold)
                {
                    anomalies.Add(new RpcPerformanceAnomaly
                    {
                        Type = "高CPU使用",
                        Description = $"CPU使用率过高: {snapshot.CpuUsage:F2}% (阈值: {_options.CpuThreshold}%)",
                        Severity = AnomalySeverity.Warning,
                        Timestamp = DateTime.UtcNow,
                        Data = new { snapshot.CpuUsage, Threshold = _options.CpuThreshold }
                    });
                }

                // 检测线程池饥饿
                if (snapshot.ThreadPoolInfo.AvailableWorkerThreads < _options.MinAvailableThreads)
                {
                    anomalies.Add(new RpcPerformanceAnomaly
                    {
                        Type = "线程池饥饿",
                        Description = $"可用工作线程过少: {snapshot.ThreadPoolInfo.AvailableWorkerThreads} (阈值: {_options.MinAvailableThreads})",
                        Severity = AnomalySeverity.Error,
                        Timestamp = DateTime.UtcNow,
                        Data = new { snapshot.ThreadPoolInfo.AvailableWorkerThreads, Threshold = _options.MinAvailableThreads }
                    });
                }

                // 检测频繁GC
                var totalGC = snapshot.GcInfo.Gen0Collections + snapshot.GcInfo.Gen1Collections + snapshot.GcInfo.Gen2Collections;
                if (totalGC > _options.GcThreshold)
                {
                    anomalies.Add(new RpcPerformanceAnomaly
                    {
                        Type = "频繁GC",
                        Description = $"垃圾回收次数过多: {totalGC} (阈值: {_options.GcThreshold})",
                        Severity = AnomalySeverity.Warning,
                        Timestamp = DateTime.UtcNow,
                        Data = new { TotalGC = totalGC, Threshold = _options.GcThreshold, snapshot.GcInfo }
                    });
                }

                return anomalies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测性能异常时发生错误");
                return anomalies;
            }
        }

        private async void ReportDiagnostics(object state)
        {
            if (_disposed) return;

            try
            {
                var snapshot = await GetDiagnosticSnapshotAsync();
                if (snapshot == null) return;

                // 更新指标收集器
                _metricsCollector?.RecordResourceUsage(
                    snapshot.WorkingSet,
                    snapshot.CpuUsage,
                    snapshot.ThreadCount);

                // 检测异常
                var anomalies = await DetectPerformanceAnomaliesAsync();
                if (anomalies.Any())
                {
                    _logger.LogWarning("检测到性能异常: {AnomaliesCount}个", anomalies.Count);
                    foreach (var anomaly in anomalies)
                    {
                        _logger.LogWarning("性能异常: {Type} - {Description}", anomaly.Type, anomaly.Description);
                    }
                }

                // 记录诊断报告
                if (_options.EnableVerboseLogging)
                {
                    _logger.LogInformation("RPC诊断报告: CPU={CpuUsage:F2}%, 内存={MemoryUsage:F2}MB, 线程={ThreadCount}, GC={GcCount}",
                        snapshot.CpuUsage,
                        snapshot.WorkingSet / (1024 * 1024),
                        snapshot.ThreadCount,
                        snapshot.GcInfo.Gen0Collections + snapshot.GcInfo.Gen1Collections + snapshot.GcInfo.Gen2Collections);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "诊断报告过程中发生错误");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // 取消所有订阅
            foreach (var subscription in _subscriptions.Values)
            {
                subscription?.Dispose();
            }
            _subscriptions.Clear();

            // 释放计时器
            _reportTimer?.Dispose();

            // 释放性能计数器
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();

            _logger.LogInformation("RPC诊断观察者已释放");
        }
    }

    /// <summary>
    /// RPC诊断配置选项
    /// </summary>
    public class RpcDiagnosticOptions
    {
        /// <summary>
        /// 是否启用定期报告
        /// </summary>
        public bool EnablePeriodicReporting { get; set; } = true;

        /// <summary>
        /// 报告间隔
        /// </summary>
        public TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 内存阈值（字节）
        /// </summary>
        public long MemoryThreshold { get; set; } = 500 * 1024 * 1024; // 500MB

        /// <summary>
        /// CPU阈值（百分比）
        /// </summary>
        public double CpuThreshold { get; set; } = 80.0;

        /// <summary>
        /// 最小可用线程数
        /// </summary>
        public int MinAvailableThreads { get; set; } = 10;

        /// <summary>
        /// GC阈值
        /// </summary>
        public int GcThreshold { get; set; } = 1000;

        /// <summary>
        /// 自定义阈值
        /// </summary>
        public Dictionary<string, object> CustomThresholds { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// RPC诊断数据快照
    /// </summary>
    public class RpcDiagnosticSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int ProcessId { get; set; }
        public string MachineName { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long WorkingSet { get; set; }
        public long VirtualMemorySize { get; set; }
        public long PagedMemorySize { get; set; }
        public TimeSpan ProcessorTime { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public double CpuUsage { get; set; }
        public long AvailableMemory { get; set; }
        public GcInfo GcInfo { get; set; }
        public ThreadPoolInfo ThreadPoolInfo { get; set; }
        public List<RpcDiagnosticData> DiagnosticData { get; set; } = new List<RpcDiagnosticData>();
    }

    /// <summary>
    /// GC信息
    /// </summary>
    public class GcInfo
    {
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long TotalMemory { get; set; }
        public bool IsServerGC { get; set; }
    }

    /// <summary>
    /// 线程池信息
    /// </summary>
    public class ThreadPoolInfo
    {
        public int AvailableWorkerThreads { get; set; }
        public int AvailableCompletionPortThreads { get; set; }
        public int MaxWorkerThreads { get; set; }
        public int MaxCompletionPortThreads { get; set; }
        public int MinWorkerThreads { get; set; }
        public int MinCompletionPortThreads { get; set; }
    }

    /// <summary>
    /// RPC诊断数据
    /// </summary>
    public class RpcDiagnosticData
    {
        public string Key { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
        public int ThreadId { get; set; }
    }

    /// <summary>
    /// RPC性能异常
    /// </summary>
    public class RpcPerformanceAnomaly
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public AnomalySeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
        public object Data { get; set; }
    }

    /// <summary>
    /// 异常严重程度
    /// </summary>
    public enum AnomalySeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// RPC诊断订阅者
    /// </summary>
    internal class RpcDiagnosticSubscriber : IObserver<KeyValuePair<string, object>>
    {
        private readonly ILogger _logger;
        private readonly IRpcMetricsCollector _metricsCollector;
        private readonly RpcDiagnosticOptions _options;

        public RpcDiagnosticSubscriber(ILogger logger, IRpcMetricsCollector metricsCollector, RpcDiagnosticOptions options)
        {
            _logger = logger;
            _metricsCollector = metricsCollector;
            _options = options;
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            try
            {
                // 处理诊断事件
                if (_options.EnableVerboseLogging)
                {
                    _logger.LogDebug("诊断事件: {Key}, 数据: {Data}", value.Key, value.Value);
                }

                // 根据事件类型进行特殊处理
                switch (value.Key)
                {
                    case "System.Threading.Tasks.TaskScheduled":
                        // 任务调度事件
                        break;
                    case "System.Threading.Tasks.TaskCompleted":
                        // 任务完成事件
                        break;
                    case "Microsoft.AspNetCore.Hosting.RequestStarted":
                        // HTTP请求开始事件
                        break;
                    case "Microsoft.AspNetCore.Hosting.RequestFinished":
                        // HTTP请求完成事件
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "处理诊断事件时发生错误: {Key}", value.Key);
            }
        }

        public void OnError(Exception error)
        {
            _logger.LogError(error, "诊断订阅者发生错误");
        }

        public void OnCompleted()
        {
            _logger.LogDebug("诊断订阅者完成");
        }
    }

    /// <summary>
    /// RPC性能分析器
    /// </summary>
    internal class RpcProfiler : IDisposable
    {
        private readonly string _name;
        private readonly RpcDiagnosticObserver _observer;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private readonly DateTime _startTime;

        public RpcProfiler(string name, RpcDiagnosticObserver observer, ILogger logger)
        {
            _name = name;
            _observer = observer;
            _logger = logger;
            _startTime = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
            
            _logger.LogDebug("开始性能分析: {Name}", _name);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            var duration = _stopwatch.Elapsed;
            
            _logger.LogDebug("结束性能分析: {Name}, 耗时: {Duration}ms", _name, duration.TotalMilliseconds);
            
            // 记录性能数据
            _observer.RecordDiagnosticData($"Performance.{_name}", new
            {
                Duration = duration.TotalMilliseconds,
                StartTime = _startTime,
                EndTime = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId
            });
        }
    }

    /// <summary>
    /// 空的Disposable实现
    /// </summary>
    internal class NullDisposable : IDisposable
    {
        public void Dispose()
        {
            // 空实现
        }
    }
} 