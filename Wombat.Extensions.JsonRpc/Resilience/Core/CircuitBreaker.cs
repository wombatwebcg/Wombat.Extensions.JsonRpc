using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Resilience.Core
{
    /// <summary>
    /// 断路器实现
    /// 提供完整的故障保护和自动恢复功能
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker, IDisposable
    {
        private readonly ILogger<CircuitBreaker> _logger;
        private readonly CircuitBreakerOptions _options;
        private readonly ConcurrentQueue<OperationResult> _recentResults;
        private readonly Timer _monitoringTimer;
        private readonly object _stateLock = new object();

        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount = 0;
        private int _successCount = 0;
        private DateTime? _lastFailureTime;
        private DateTime? _lastSuccessTime;
        private DateTime? _stateChangedTime;
        private DateTime _createdTime;
        private int _openCount = 0;
        private long _totalRequests = 0;
        private long _successfulRequests = 0;
        private long _failedRequests = 0;
        private long _rejectedRequests = 0;
        private readonly ConcurrentQueue<double> _responseTimes;
        private volatile bool _disposed = false;

        public event EventHandler<CircuitBreakerStateChangedEventArgs> StateChanged;
        public event EventHandler<CircuitBreakerOperationEventArgs> OperationSucceeded;
        public event EventHandler<CircuitBreakerOperationEventArgs> OperationFailed;
        public event EventHandler<CircuitBreakerStateChangedEventArgs> CircuitBreakerTripped;

        public string Name => _options.Name;
        public CircuitBreakerState State => _state;
        public int FailureCount => _failureCount;
        public int SuccessCount => _successCount;
        public DateTime? LastFailureTime => _lastFailureTime;
        public DateTime? NextAttemptTime
        {
            get
            {
                if (_state == CircuitBreakerState.Open && _stateChangedTime.HasValue)
                {
                    return _stateChangedTime.Value.Add(_options.Timeout);
                }
                return null;
            }
        }

        public CircuitBreaker(CircuitBreakerOptions options, ILogger<CircuitBreaker> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CircuitBreaker>.Instance;
            _recentResults = new ConcurrentQueue<OperationResult>();
            _responseTimes = new ConcurrentQueue<double>();
            _createdTime = DateTime.UtcNow;
            _stateChangedTime = _createdTime;

            // 启动监控定时器
            _monitoringTimer = new Timer(MonitorState, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            _logger.LogInformation("断路器 '{Name}' 已创建，配置: 失败阈值={FailureThreshold}, 超时={Timeout}, 失败率阈值={FailureRateThreshold}%",
                _options.Name, _options.FailureThreshold, _options.Timeout, _options.FailureRateThreshold);
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CircuitBreaker));
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            Interlocked.Increment(ref _totalRequests);

            // 检查断路器状态
            if (!CanExecute())
            {
                Interlocked.Increment(ref _rejectedRequests);
                _logger.LogWarning("断路器 '{Name}' 处于 {State} 状态，请求被拒绝", _options.Name, _state);
                throw new CircuitBreakerOpenException(_options.Name, NextAttemptTime, _failureCount);
            }

            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;

            try
            {
                var result = await operation(cancellationToken);
                stopwatch.Stop();
                
                OnOperationSuccess(stopwatch.Elapsed);
                
                var eventArgs = new CircuitBreakerOperationEventArgs
                {
                    Name = _options.Name,
                    IsSuccess = true,
                    ExecutionTime = stopwatch.Elapsed.TotalMilliseconds,
                    Timestamp = startTime,
                    State = _state,
                    FailureCount = _failureCount
                };
                
                OperationSucceeded?.Invoke(this, eventArgs);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                OnOperationFailure(ex, stopwatch.Elapsed);
                
                var eventArgs = new CircuitBreakerOperationEventArgs
                {
                    Name = _options.Name,
                    IsSuccess = false,
                    ExecutionTime = stopwatch.Elapsed.TotalMilliseconds,
                    Exception = ex,
                    Timestamp = startTime,
                    State = _state,
                    FailureCount = _failureCount
                };
                
                OperationFailed?.Invoke(this, eventArgs);
                
                throw;
            }
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async token =>
            {
                await operation(token);
                return true; // 返回一个虚拟值
            }, cancellationToken);
        }

        public void Trip()
        {
            _logger.LogWarning("手动触发断路器 '{Name}'", _options.Name);
            ChangeState(CircuitBreakerState.Open, "手动触发");
        }

        public void Reset()
        {
            lock (_stateLock)
            {
                _failureCount = 0;
                _successCount = 0;
                _lastFailureTime = null;
                
                // 清理历史记录
                while (_recentResults.TryDequeue(out _)) { }
                while (_responseTimes.TryDequeue(out _)) { }
                
                ChangeState(CircuitBreakerState.Closed, "手动重置");
                
                _logger.LogInformation("断路器 '{Name}' 已手动重置", _options.Name);
            }
        }

        public CircuitBreakerStatistics GetStatistics()
        {
            var now = DateTime.UtcNow;
            var failureRate = _totalRequests > 0 ? (_failedRequests / (double)_totalRequests) * 100 : 0;
            var avgResponseTime = _responseTimes.Count > 0 ? _responseTimes.Average() : 0;

            return new CircuitBreakerStatistics
            {
                Name = _options.Name,
                State = _state,
                TotalRequests = _totalRequests,
                SuccessfulRequests = _successfulRequests,
                FailedRequests = _failedRequests,
                RejectedRequests = _rejectedRequests,
                FailureRate = failureRate,
                AverageResponseTime = avgResponseTime,
                LastFailureTime = _lastFailureTime,
                LastSuccessTime = _lastSuccessTime,
                CircuitBreakerOpenCount = _openCount,
                CurrentFailureCount = _failureCount,
                NextAttemptTime = NextAttemptTime,
                CreatedTime = _createdTime,
                LastUpdatedTime = now
            };
        }

        private bool CanExecute()
        {
            lock (_stateLock)
            {
                switch (_state)
                {
                    case CircuitBreakerState.Closed:
                        return true;

                    case CircuitBreakerState.Open:
                        // 检查是否应该进入半开状态
                        if (_stateChangedTime.HasValue && DateTime.UtcNow >= _stateChangedTime.Value.Add(_options.Timeout))
                        {
                            ChangeState(CircuitBreakerState.HalfOpen, "超时后自动进入半开状态");
                            return true;
                        }
                        return false;

                    case CircuitBreakerState.HalfOpen:
                        return true;

                    default:
                        return false;
                }
            }
        }

        private void OnOperationSuccess(TimeSpan duration)
        {
            Interlocked.Increment(ref _successfulRequests);
            _lastSuccessTime = DateTime.UtcNow;
            
            // 记录响应时间
            _responseTimes.Enqueue(duration.TotalMilliseconds);
            TrimQueue(_responseTimes, 100);

            // 记录操作结果
            var result = new OperationResult
            {
                IsSuccess = true,
                Timestamp = DateTime.UtcNow,
                Duration = duration
            };
            _recentResults.Enqueue(result);
            TrimQueue(_recentResults, _options.MaxStatisticsCount);

            lock (_stateLock)
            {
                if (_state == CircuitBreakerState.HalfOpen)
                {
                    _successCount++;
                    if (_successCount >= _options.SuccessThreshold)
                    {
                        _failureCount = 0;
                        _successCount = 0;
                        ChangeState(CircuitBreakerState.Closed, $"半开状态下连续成功 {_options.SuccessThreshold} 次");
                    }
                }
                else if (_state == CircuitBreakerState.Closed)
                {
                    // 在关闭状态下，成功操作可以重置失败计数
                    if (_failureCount > 0)
                    {
                        _failureCount = Math.Max(0, _failureCount - 1);
                    }
                }
            }

            _logger.LogDebug("断路器 '{Name}' 操作成功，当前状态: {State}, 失败计数: {FailureCount}, 成功计数: {SuccessCount}",
                _options.Name, _state, _failureCount, _successCount);
        }

        private void OnOperationFailure(Exception exception, TimeSpan duration)
        {
            // 检查是否应该监控这个异常
            if (!ShouldMonitorException(exception))
            {
                _logger.LogDebug("断路器 '{Name}' 忽略异常类型: {ExceptionType}", _options.Name, exception.GetType().Name);
                return;
            }

            Interlocked.Increment(ref _failedRequests);
            _lastFailureTime = DateTime.UtcNow;

            // 记录响应时间
            _responseTimes.Enqueue(duration.TotalMilliseconds);
            TrimQueue(_responseTimes, 100);

            // 记录操作结果
            var result = new OperationResult
            {
                IsSuccess = false,
                Timestamp = DateTime.UtcNow,
                Duration = duration,
                Exception = exception
            };
            _recentResults.Enqueue(result);
            TrimQueue(_recentResults, _options.MaxStatisticsCount);

            lock (_stateLock)
            {
                _failureCount++;
                _successCount = 0; // 重置成功计数

                if (_state == CircuitBreakerState.Closed)
                {
                    // 检查是否应该打开断路器
                    if (ShouldOpenCircuitBreaker())
                    {
                        _openCount++;
                        ChangeState(CircuitBreakerState.Open, $"连续失败 {_failureCount} 次或失败率过高");
                        
                        CircuitBreakerTripped?.Invoke(this, new CircuitBreakerStateChangedEventArgs
                        {
                            Name = _options.Name,
                            PreviousState = CircuitBreakerState.Closed,
                            CurrentState = CircuitBreakerState.Open,
                            Timestamp = DateTime.UtcNow,
                            Reason = "断路器被触发",
                            FailureCount = _failureCount,
                            Statistics = GetStatistics()
                        });
                    }
                }
                else if (_state == CircuitBreakerState.HalfOpen)
                {
                    // 半开状态下任何失败都会重新打开断路器
                    _openCount++;
                    ChangeState(CircuitBreakerState.Open, "半开状态下操作失败");
                }
            }

            _logger.LogWarning("断路器 '{Name}' 操作失败，异常: {ExceptionType}, 当前状态: {State}, 失败计数: {FailureCount}",
                _options.Name, exception.GetType().Name, _state, _failureCount);
        }

        private bool ShouldMonitorException(Exception exception)
        {
            var exceptionType = exception.GetType();

            // 检查是否在忽略列表中
            if (_options.IgnoredExceptions?.Any(t => t.IsAssignableFrom(exceptionType)) == true)
            {
                return false;
            }

            // 检查是否在监控列表中
            if (_options.MonitoredExceptions?.Any(t => t.IsAssignableFrom(exceptionType)) == true)
            {
                return true;
            }

            // 特殊处理超时异常
            if (_options.MonitorTimeoutExceptions && (exception is TimeoutException || exception is OperationCanceledException))
            {
                return true;
            }

            return false;
        }

        private bool ShouldOpenCircuitBreaker()
        {
            // 检查连续失败次数
            if (_failureCount >= _options.FailureThreshold)
            {
                return true;
            }

            // 检查失败率
            if (_options.FailureRateThreshold > 0 && _totalRequests >= _options.MinimumThroughput)
            {
                var currentFailureRate = (_failedRequests / (double)_totalRequests) * 100;
                if (currentFailureRate >= _options.FailureRateThreshold)
                {
                    return true;
                }
            }

            // 检查时间窗口内的失败率
            if (_options.SamplingPeriod > TimeSpan.Zero)
            {
                var windowResults = GetResultsInWindow(_options.SamplingPeriod);
                if (windowResults.Count >= _options.MinimumThroughput)
                {
                    var windowFailureRate = (windowResults.Count(r => !r.IsSuccess) / (double)windowResults.Count) * 100;
                    if (windowFailureRate >= _options.FailureRateThreshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private System.Collections.Generic.List<OperationResult> GetResultsInWindow(TimeSpan window)
        {
            var cutoff = DateTime.UtcNow.Subtract(window);
            return _recentResults.Where(r => r.Timestamp >= cutoff).ToList();
        }

        private void ChangeState(CircuitBreakerState newState, string reason)
        {
            if (_state == newState) return;

            var previousState = _state;
            _state = newState;
            _stateChangedTime = DateTime.UtcNow;

            var eventArgs = new CircuitBreakerStateChangedEventArgs
            {
                Name = _options.Name,
                PreviousState = previousState,
                CurrentState = newState,
                Timestamp = _stateChangedTime.Value,
                Reason = reason,
                FailureCount = _failureCount,
                Statistics = GetStatistics()
            };

            StateChanged?.Invoke(this, eventArgs);

            _logger.LogWarning("断路器 '{Name}' 状态变更: {PreviousState} -> {CurrentState}, 原因: {Reason}",
                _options.Name, previousState, newState, reason);
        }

        private void MonitorState(object state)
        {
            if (_disposed) return;

            try
            {
                // 清理过期的统计数据
                CleanupExpiredResults();

                // 在关闭状态下，定期检查是否需要自动重置失败计数
                if (_state == CircuitBreakerState.Closed && _options.EnableAutoReset && _failureCount > 0)
                {
                    var timeSinceLastFailure = _lastFailureTime.HasValue ? 
                        DateTime.UtcNow - _lastFailureTime.Value : TimeSpan.MaxValue;
                    
                    if (timeSinceLastFailure > _options.Timeout)
                    {
                        lock (_stateLock)
                        {
                            if (_failureCount > 0)
                            {
                                _failureCount = 0;
                                _logger.LogInformation("断路器 '{Name}' 自动重置失败计数", _options.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断路器 '{Name}' 监控过程中发生错误", _options.Name);
            }
        }

        private void CleanupExpiredResults()
        {
            var cutoff = DateTime.UtcNow.Subtract(_options.SamplingPeriod);
            
            // 清理过期的操作结果
            while (_recentResults.TryPeek(out var result) && result.Timestamp < cutoff)
            {
                _recentResults.TryDequeue(out _);
            }

            // 限制响应时间队列大小
            TrimQueue(_responseTimes, 100);
        }

        private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
        {
            while (queue.Count > maxSize)
            {
                queue.TryDequeue(out _);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _monitoringTimer?.Dispose();

            _logger.LogInformation("断路器 '{Name}' 已释放", _options.Name);
        }

        /// <summary>
        /// 操作结果内部类
        /// </summary>
        private class OperationResult
        {
            public bool IsSuccess { get; set; }
            public DateTime Timestamp { get; set; }
            public TimeSpan Duration { get; set; }
            public Exception Exception { get; set; }
        }
    }
} 