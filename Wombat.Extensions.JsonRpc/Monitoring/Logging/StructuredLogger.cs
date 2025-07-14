using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Monitoring.Logging
{
    /// <summary>
    /// 结构化日志器
    /// 提供高性能、异步、分级的日志记录功能
    /// </summary>
    public class StructuredLogger : IDisposable
    {
        private readonly ILogger _logger;
        private readonly StructuredLoggerOptions _options;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly Timer _flushTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundTask;
        private readonly SemaphoreSlim _flushSemaphore;
        private volatile bool _disposed = false;

        // 性能统计
        private long _totalLogsWritten;
        private long _totalLogsFlushed;
        private long _queueSize;
        private DateTime _lastFlushTime;

        public StructuredLogger(ILogger logger, StructuredLoggerOptions options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new StructuredLoggerOptions();
            _logQueue = new ConcurrentQueue<LogEntry>();
            _flushSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            _lastFlushTime = DateTime.UtcNow;

            // 启动后台处理任务
            _backgroundTask = Task.Run(ProcessLogQueueAsync);

            // 启动定时刷新
            _flushTimer = new Timer(FlushLogs, null, _options.FlushInterval, _options.FlushInterval);
        }

        /// <summary>
        /// 记录RPC请求开始
        /// </summary>
        /// <param name="requestId">请求ID</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="parameters">参数</param>
        /// <param name="clientId">客户端ID</param>
        /// <param name="correlationId">关联ID</param>
        public void LogRequestStart(
            string requestId,
            string methodName,
            string serviceName,
            object parameters = null,
            string clientId = null,
            string correlationId = null)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Level = LogLevel.Information,
                Message = "RPC请求开始",
                Category = "RPC.Request.Start",
                Timestamp = DateTime.UtcNow,
                RequestId = requestId,
                MethodName = methodName,
                ServiceName = serviceName,
                ClientId = clientId,
                CorrelationId = correlationId,
                Parameters = parameters,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录RPC请求完成
        /// </summary>
        /// <param name="requestId">请求ID</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="duration">持续时间</param>
        /// <param name="success">是否成功</param>
        /// <param name="result">结果</param>
        /// <param name="clientId">客户端ID</param>
        /// <param name="correlationId">关联ID</param>
        public void LogRequestComplete(
            string requestId,
            string methodName,
            string serviceName,
            TimeSpan duration,
            bool success,
            object result = null,
            string clientId = null,
            string correlationId = null)
        {
            if (_disposed) return;

            var level = success ? LogLevel.Information : LogLevel.Warning;
            var message = success ? "RPC请求完成" : "RPC请求失败";

            var logEntry = new LogEntry
            {
                Level = level,
                Message = message,
                Category = "RPC.Request.Complete",
                Timestamp = DateTime.UtcNow,
                RequestId = requestId,
                MethodName = methodName,
                ServiceName = serviceName,
                ClientId = clientId,
                CorrelationId = correlationId,
                Duration = duration,
                Success = success,
                Result = result,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            // 慢请求警告
            if (duration.TotalMilliseconds > _options.SlowRequestThreshold)
            {
                logEntry.Level = LogLevel.Warning;
                logEntry.Message = "RPC慢请求完成";
                logEntry.Category = "RPC.Request.Slow";
                logEntry.SlowRequest = true;
            }

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录RPC请求错误
        /// </summary>
        /// <param name="requestId">请求ID</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="exception">异常</param>
        /// <param name="duration">持续时间</param>
        /// <param name="clientId">客户端ID</param>
        /// <param name="correlationId">关联ID</param>
        public void LogRequestError(
            string requestId,
            string methodName,
            string serviceName,
            Exception exception,
            TimeSpan duration = default,
            string clientId = null,
            string correlationId = null)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Level = LogLevel.Error,
                Message = "RPC请求错误",
                Category = "RPC.Request.Error",
                Timestamp = DateTime.UtcNow,
                RequestId = requestId,
                MethodName = methodName,
                ServiceName = serviceName,
                ClientId = clientId,
                CorrelationId = correlationId,
                Duration = duration,
                Success = false,
                Exception = exception,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录连接事件
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="transportType">传输类型</param>
        /// <param name="connected">是否连接</param>
        /// <param name="clientAddress">客户端地址</param>
        /// <param name="clientPort">客户端端口</param>
        public void LogConnectionEvent(
            string connectionId,
            string transportType,
            bool connected,
            string clientAddress = null,
            int? clientPort = null)
        {
            if (_disposed) return;

            var message = connected ? "RPC连接建立" : "RPC连接断开";
            var category = connected ? "RPC.Connection.Connected" : "RPC.Connection.Disconnected";

            var logEntry = new LogEntry
            {
                Level = LogLevel.Information,
                Message = message,
                Category = category,
                Timestamp = DateTime.UtcNow,
                ConnectionId = connectionId,
                TransportType = transportType,
                ClientAddress = clientAddress,
                ClientPort = clientPort,
                Connected = connected,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录性能指标
        /// </summary>
        /// <param name="category">类别</param>
        /// <param name="metrics">指标数据</param>
        public void LogPerformanceMetrics(string category, object metrics)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Level = LogLevel.Information,
                Message = "RPC性能指标",
                Category = $"RPC.Performance.{category}",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录系统事件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        /// <param name="category">类别</param>
        /// <param name="properties">属性</param>
        /// <param name="exception">异常</param>
        public void LogSystemEvent(
            LogLevel level,
            string message,
            string category = null,
            Dictionary<string, object> properties = null,
            Exception exception = null)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Level = level,
                Message = message,
                Category = category ?? "RPC.System",
                Timestamp = DateTime.UtcNow,
                Properties = properties,
                Exception = exception,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 记录自定义日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        /// <param name="category">类别</param>
        /// <param name="data">数据</param>
        /// <param name="exception">异常</param>
        /// <param name="correlationId">关联ID</param>
        public void LogCustom(
            LogLevel level,
            string message,
            string category = null,
            object data = null,
            Exception exception = null,
            string correlationId = null)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Level = level,
                Message = message,
                Category = category ?? "RPC.Custom",
                Timestamp = DateTime.UtcNow,
                Data = data,
                Exception = exception,
                CorrelationId = correlationId,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName
            };

            EnqueueLog(logEntry);
        }

        /// <summary>
        /// 获取日志统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public LoggerStatistics GetStatistics()
        {
            return new LoggerStatistics
            {
                TotalLogsWritten = _totalLogsWritten,
                TotalLogsFlushed = _totalLogsFlushed,
                QueueSize = _queueSize,
                LastFlushTime = _lastFlushTime,
                IsHealthy = _queueSize < _options.MaxQueueSize * 0.8
            };
        }

        /// <summary>
        /// 立即刷新日志
        /// </summary>
        /// <returns>刷新任务</returns>
        public async Task FlushAsync()
        {
            if (_disposed) return;

            await _flushSemaphore.WaitAsync();
            try
            {
                await ProcessQueueBatchAsync();
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        private void EnqueueLog(LogEntry entry)
        {
            if (_disposed) return;

            // 检查队列大小
            if (_queueSize >= _options.MaxQueueSize)
            {
                // 队列满时的处理策略
                if (_options.OverflowStrategy == LogOverflowStrategy.DropOldest)
                {
                    // 丢弃最旧的日志
                    _logQueue.TryDequeue(out _);
                    Interlocked.Decrement(ref _queueSize);
                }
                else if (_options.OverflowStrategy == LogOverflowStrategy.DropNewest)
                {
                    // 丢弃最新的日志
                    return;
                }
            }

            _logQueue.Enqueue(entry);
            Interlocked.Increment(ref _queueSize);
            Interlocked.Increment(ref _totalLogsWritten);
        }

        private async Task ProcessLogQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.ProcessInterval, _cancellationTokenSource.Token);
                    
                    if (_flushSemaphore.CurrentCount > 0)
                    {
                        await _flushSemaphore.WaitAsync(_cancellationTokenSource.Token);
                        try
                        {
                            await ProcessQueueBatchAsync();
                        }
                        finally
                        {
                            _flushSemaphore.Release();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 记录后台处理错误，但不能使用自身记录，避免递归
                    Debug.WriteLine($"结构化日志器后台处理错误: {ex.Message}");
                }
            }
        }

        private async Task ProcessQueueBatchAsync()
        {
            var processedCount = 0;
            var maxBatchSize = _options.BatchSize;

            while (processedCount < maxBatchSize && _logQueue.TryDequeue(out var entry))
            {
                try
                {
                    await WriteLogEntryAsync(entry);
                    processedCount++;
                    Interlocked.Decrement(ref _queueSize);
                    Interlocked.Increment(ref _totalLogsFlushed);
                }
                catch (Exception ex)
                {
                    // 记录写入错误，但不能使用自身记录，避免递归
                    Debug.WriteLine($"结构化日志器写入错误: {ex.Message}");
                }
            }

            if (processedCount > 0)
            {
                _lastFlushTime = DateTime.UtcNow;
            }
        }

        private async Task WriteLogEntryAsync(LogEntry entry)
        {
            var logData = CreateLogData(entry);

            // 根据配置决定是否异步写入
            if (_options.AsyncWrite)
            {
                await Task.Run(() => WriteLogData(entry.Level, logData, entry.Exception));
            }
            else
            {
                WriteLogData(entry.Level, logData, entry.Exception);
            }
        }

        private Dictionary<string, object> CreateLogData(LogEntry entry)
        {
            var logData = new Dictionary<string, object>
            {
                ["Message"] = entry.Message,
                ["Category"] = entry.Category,
                ["Timestamp"] = entry.Timestamp,
                ["ThreadId"] = entry.ThreadId,
                ["ProcessId"] = entry.ProcessId,
                ["MachineName"] = entry.MachineName
            };

            // 添加可选字段
            if (!string.IsNullOrEmpty(entry.RequestId))
                logData["RequestId"] = entry.RequestId;
            
            if (!string.IsNullOrEmpty(entry.MethodName))
                logData["MethodName"] = entry.MethodName;
            
            if (!string.IsNullOrEmpty(entry.ServiceName))
                logData["ServiceName"] = entry.ServiceName;
            
            if (!string.IsNullOrEmpty(entry.ClientId))
                logData["ClientId"] = entry.ClientId;
            
            if (!string.IsNullOrEmpty(entry.CorrelationId))
                logData["CorrelationId"] = entry.CorrelationId;
            
            if (!string.IsNullOrEmpty(entry.ConnectionId))
                logData["ConnectionId"] = entry.ConnectionId;
            
            if (!string.IsNullOrEmpty(entry.TransportType))
                logData["TransportType"] = entry.TransportType;
            
            if (!string.IsNullOrEmpty(entry.ClientAddress))
                logData["ClientAddress"] = entry.ClientAddress;
            
            if (entry.ClientPort.HasValue)
                logData["ClientPort"] = entry.ClientPort.Value;
            
            if (entry.Connected.HasValue)
                logData["Connected"] = entry.Connected.Value;
            
            if (entry.Duration != TimeSpan.Zero)
                logData["Duration"] = entry.Duration.TotalMilliseconds;
            
            if (entry.Success.HasValue)
                logData["Success"] = entry.Success.Value;
            
            if (entry.SlowRequest.HasValue)
                logData["SlowRequest"] = entry.SlowRequest.Value;

            // 添加复杂对象
            if (entry.Parameters != null && _options.IncludeParameters)
            {
                logData["Parameters"] = SerializeObject(entry.Parameters);
            }
            
            if (entry.Result != null && _options.IncludeResult)
            {
                logData["Result"] = SerializeObject(entry.Result);
            }
            
            if (entry.Metrics != null)
            {
                logData["Metrics"] = SerializeObject(entry.Metrics);
            }
            
            if (entry.Data != null)
            {
                logData["Data"] = SerializeObject(entry.Data);
            }
            
            if (entry.Properties != null)
            {
                foreach (var prop in entry.Properties)
                {
                    logData[prop.Key] = prop.Value;
                }
            }

            return logData;
        }

        private object SerializeObject(object obj)
        {
            if (obj == null) return null;

            try
            {
                // 如果是简单类型，直接返回
                if (obj is string || obj.GetType().IsPrimitive || obj is DateTime || obj is TimeSpan)
                {
                    return obj;
                }

                // 序列化复杂对象
                var json = JsonSerializer.Serialize(obj, _options.JsonSerializerOptions);
                
                // 检查长度限制
                if (json.Length > _options.MaxObjectLength)
                {
                    return json.Substring(0, _options.MaxObjectLength) + "...";
                }

                return json;
            }
            catch
            {
                return obj?.ToString() ?? "[序列化失败]";
            }
        }

        private void WriteLogData(LogLevel level, Dictionary<string, object> logData, Exception exception)
        {
            var message = logData["Message"]?.ToString() ?? "Unknown";
            
            if (exception != null)
            {
                _logger.Log(level, exception, message + " - {LogData}", JsonSerializer.Serialize(logData, _options.JsonSerializerOptions));
            }
            else
            {
                _logger.Log(level, message + " - {LogData}", JsonSerializer.Serialize(logData, _options.JsonSerializerOptions));
            }
        }

        private void FlushLogs(object state)
        {
            Task.Run(async () =>
            {
                try
                {
                    await FlushAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"定时刷新日志时发生错误: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cancellationTokenSource?.Cancel();
            _flushTimer?.Dispose();

            // 等待后台任务完成
            try
            {
                _backgroundTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // 忽略超时
            }

            // 最后一次刷新队列
            try
            {
                FlushAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // 忽略超时
            }

            _cancellationTokenSource?.Dispose();
            _flushSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// 结构化日志器配置选项
    /// </summary>
    public class StructuredLoggerOptions
    {
        /// <summary>
        /// 是否启用异步写入
        /// </summary>
        public bool AsyncWrite { get; set; } = true;

        /// <summary>
        /// 批处理大小
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// 最大队列大小
        /// </summary>
        public int MaxQueueSize { get; set; } = 10000;

        /// <summary>
        /// 处理间隔
        /// </summary>
        public TimeSpan ProcessInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// 刷新间隔
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 是否包含参数
        /// </summary>
        public bool IncludeParameters { get; set; } = true;

        /// <summary>
        /// 是否包含结果
        /// </summary>
        public bool IncludeResult { get; set; } = false;

        /// <summary>
        /// 慢请求阈值（毫秒）
        /// </summary>
        public double SlowRequestThreshold { get; set; } = 1000;

        /// <summary>
        /// 最大对象长度
        /// </summary>
        public int MaxObjectLength { get; set; } = 1000;

        /// <summary>
        /// 队列溢出策略
        /// </summary>
        public LogOverflowStrategy OverflowStrategy { get; set; } = LogOverflowStrategy.DropOldest;

        /// <summary>
        /// JSON序列化选项
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// 日志溢出策略
    /// </summary>
    public enum LogOverflowStrategy
    {
        /// <summary>
        /// 丢弃最旧的日志
        /// </summary>
        DropOldest,

        /// <summary>
        /// 丢弃最新的日志
        /// </summary>
        DropNewest,

        /// <summary>
        /// 阻塞写入
        /// </summary>
        Block
    }

    /// <summary>
    /// 日志条目
    /// </summary>
    internal class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
        public string RequestId { get; set; }
        public string MethodName { get; set; }
        public string ServiceName { get; set; }
        public string ClientId { get; set; }
        public string CorrelationId { get; set; }
        public string ConnectionId { get; set; }
        public string TransportType { get; set; }
        public string ClientAddress { get; set; }
        public int? ClientPort { get; set; }
        public bool? Connected { get; set; }
        public TimeSpan Duration { get; set; }
        public bool? Success { get; set; }
        public bool? SlowRequest { get; set; }
        public object Parameters { get; set; }
        public object Result { get; set; }
        public object Metrics { get; set; }
        public object Data { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public Exception Exception { get; set; }
        public int ThreadId { get; set; }
        public int ProcessId { get; set; }
        public string MachineName { get; set; }
    }

    /// <summary>
    /// 日志器统计信息
    /// </summary>
    public class LoggerStatistics
    {
        /// <summary>
        /// 总写入日志数
        /// </summary>
        public long TotalLogsWritten { get; set; }

        /// <summary>
        /// 总刷新日志数
        /// </summary>
        public long TotalLogsFlushed { get; set; }

        /// <summary>
        /// 队列大小
        /// </summary>
        public long QueueSize { get; set; }

        /// <summary>
        /// 最后刷新时间
        /// </summary>
        public DateTime LastFlushTime { get; set; }

        /// <summary>
        /// 是否健康
        /// </summary>
        public bool IsHealthy { get; set; }
    }
} 