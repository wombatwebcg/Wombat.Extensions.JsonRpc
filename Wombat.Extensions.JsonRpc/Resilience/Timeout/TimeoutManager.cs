using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Resilience.Timeout
{
    /// <summary>
    /// 超时管理器接口
    /// </summary>
    public interface ITimeoutManager
    {
        /// <summary>
        /// 执行带超时的操作
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行带超时的操作（无返回值）
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建超时令牌
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>超时令牌</returns>
        CancellationToken CreateTimeoutToken(TimeSpan timeout);

        /// <summary>
        /// 组合多个取消令牌
        /// </summary>
        /// <param name="tokens">令牌数组</param>
        /// <returns>组合令牌</returns>
        CancellationToken CombineTokens(params CancellationToken[] tokens);

        /// <summary>
        /// 获取超时统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        TimeoutStatistics GetStatistics();
    }

    /// <summary>
    /// 超时管理器实现
    /// </summary>
    public class TimeoutManager : ITimeoutManager, IDisposable
    {
        private readonly ILogger<TimeoutManager> _logger;
        private readonly TimeoutOptions _options;
        private readonly ConcurrentDictionary<string, TimeoutOperation> _activeOperations;
        private long _totalOperations = 0;
        private long _timeoutOperations = 0;
        private long _successfulOperations = 0;
        private long _cancelledOperations = 0;
        private volatile bool _disposed = false;

        public TimeoutManager(TimeoutOptions options = null, ILogger<TimeoutManager> logger = null)
        {
            _options = options ?? new TimeoutOptions();
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TimeoutManager>.Instance;
            _activeOperations = new ConcurrentDictionary<string, TimeoutOperation>();

            _logger.LogInformation("超时管理器已初始化，默认超时: {DefaultTimeout}", _options.DefaultTimeout);
        }

        public async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (_disposed) throw new ObjectDisposedException(nameof(TimeoutManager));

            var operationId = Guid.NewGuid().ToString("N");
            var effectiveTimeout = timeout == TimeSpan.Zero ? _options.DefaultTimeout : timeout;
            
            Interlocked.Increment(ref _totalOperations);

            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var timeoutOp = new TimeoutOperation
            {
                Id = operationId,
                StartTime = DateTime.UtcNow,
                Timeout = effectiveTimeout,
                CancellationTokenSource = timeoutCts
            };

            _activeOperations[operationId] = timeoutOp;

            try
            {
                _logger.LogDebug("开始执行操作 {OperationId}，超时: {Timeout}", operationId, effectiveTimeout);

                var result = await operation(combinedCts.Token);

                Interlocked.Increment(ref _successfulOperations);
                _logger.LogDebug("操作 {OperationId} 成功完成，耗时: {Duration}ms", 
                    operationId, (DateTime.UtcNow - timeoutOp.StartTime).TotalMilliseconds);

                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // 超时异常
                Interlocked.Increment(ref _timeoutOperations);
                var elapsed = DateTime.UtcNow - timeoutOp.StartTime;
                
                _logger.LogWarning("操作 {OperationId} 超时，配置超时: {Timeout}, 实际耗时: {Elapsed}ms", 
                    operationId, effectiveTimeout, elapsed.TotalMilliseconds);

                throw new TimeoutException($"操作在 {effectiveTimeout} 内未完成，实际耗时: {elapsed}");
            }
            catch (OperationCanceledException)
            {
                // 用户取消
                Interlocked.Increment(ref _cancelledOperations);
                _logger.LogDebug("操作 {OperationId} 被用户取消", operationId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "操作 {OperationId} 执行失败", operationId);
                throw;
            }
            finally
            {
                _activeOperations.TryRemove(operationId, out _);
            }
        }

        public async Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await ExecuteWithTimeoutAsync(async token =>
            {
                await operation(token);
                return true; // 返回虚拟值
            }, timeout, cancellationToken);
        }

        public CancellationToken CreateTimeoutToken(TimeSpan timeout)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TimeoutManager));

            var effectiveTimeout = timeout == TimeSpan.Zero ? _options.DefaultTimeout : timeout;
            var cts = new CancellationTokenSource(effectiveTimeout);
            
            _logger.LogDebug("创建超时令牌，超时: {Timeout}", effectiveTimeout);
            return cts.Token;
        }

        public CancellationToken CombineTokens(params CancellationToken[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return CancellationToken.None;

            if (tokens.Length == 1)
                return tokens[0];

            var cts = CancellationTokenSource.CreateLinkedTokenSource(tokens);
            return cts.Token;
        }

        public TimeoutStatistics GetStatistics()
        {
            var activeOperationsList = new System.Collections.Generic.List<ActiveOperationInfo>();
            var currentTime = DateTime.UtcNow;

            foreach (var kvp in _activeOperations)
            {
                var op = kvp.Value;
                var elapsed = currentTime - op.StartTime;
                var remaining = op.Timeout - elapsed;

                activeOperationsList.Add(new ActiveOperationInfo
                {
                    OperationId = op.Id,
                    StartTime = op.StartTime,
                    Timeout = op.Timeout,
                    ElapsedTime = elapsed,
                    RemainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                    IsExpired = remaining <= TimeSpan.Zero
                });
            }

            var timeoutRate = _totalOperations > 0 ? (_timeoutOperations / (double)_totalOperations) * 100 : 0;
            var successRate = _totalOperations > 0 ? (_successfulOperations / (double)_totalOperations) * 100 : 0;

            return new TimeoutStatistics
            {
                TotalOperations = _totalOperations,
                SuccessfulOperations = _successfulOperations,
                TimeoutOperations = _timeoutOperations,
                CancelledOperations = _cancelledOperations,
                ActiveOperations = _activeOperations.Count,
                TimeoutRate = timeoutRate,
                SuccessRate = successRate,
                DefaultTimeout = _options.DefaultTimeout,
                ActiveOperationDetails = activeOperationsList
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // 取消所有活跃操作
            foreach (var operation in _activeOperations.Values)
            {
                try
                {
                    operation.CancellationTokenSource?.Cancel();
                    operation.CancellationTokenSource?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "释放操作 {OperationId} 时发生错误", operation.Id);
                }
            }

            _activeOperations.Clear();
            _logger.LogInformation("超时管理器已释放");
        }

        /// <summary>
        /// 超时操作内部类
        /// </summary>
        private class TimeoutOperation
        {
            public string Id { get; set; }
            public DateTime StartTime { get; set; }
            public TimeSpan Timeout { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
        }
    }

    /// <summary>
    /// 超时配置选项
    /// </summary>
    public class TimeoutOptions
    {
        /// <summary>
        /// 默认超时时间
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用超时警告日志
        /// </summary>
        public bool EnableTimeoutWarnings { get; set; } = true;

        /// <summary>
        /// 超时警告阈值（达到超时时间的百分比时发出警告）
        /// </summary>
        public double TimeoutWarningThreshold { get; set; } = 0.8; // 80%

        /// <summary>
        /// 是否启用操作统计
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// 最大活跃操作数（用于保护资源）
        /// </summary>
        public int MaxActiveOperations { get; set; } = 1000;
    }

    /// <summary>
    /// 超时统计信息
    /// </summary>
    public class TimeoutStatistics
    {
        /// <summary>
        /// 总操作数
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// 成功操作数
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// 超时操作数
        /// </summary>
        public long TimeoutOperations { get; set; }

        /// <summary>
        /// 取消操作数
        /// </summary>
        public long CancelledOperations { get; set; }

        /// <summary>
        /// 当前活跃操作数
        /// </summary>
        public int ActiveOperations { get; set; }

        /// <summary>
        /// 超时率（百分比）
        /// </summary>
        public double TimeoutRate { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 默认超时时间
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; }

        /// <summary>
        /// 活跃操作详情
        /// </summary>
        public System.Collections.Generic.List<ActiveOperationInfo> ActiveOperationDetails { get; set; }
    }

    /// <summary>
    /// 活跃操作信息
    /// </summary>
    public class ActiveOperationInfo
    {
        /// <summary>
        /// 操作ID
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 已耗时
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// 剩余时间
        /// </summary>
        public TimeSpan RemainingTime { get; set; }

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired { get; set; }
    }

    /// <summary>
    /// 超时管理器扩展方法
    /// </summary>
    public static class TimeoutManagerExtensions
    {
        /// <summary>
        /// 为任务添加超时
        /// </summary>
        /// <typeparam name="T">任务返回类型</typeparam>
        /// <param name="task">原始任务</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="timeoutManager">超时管理器</param>
        /// <returns>带超时的任务</returns>
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, ITimeoutManager timeoutManager = null)
        {
            if (timeoutManager != null)
            {
                return await timeoutManager.ExecuteWithTimeoutAsync(_ => task, timeout);
            }
            else
            {
                using var cts = new CancellationTokenSource(timeout);
                var timeoutTask = Task.Delay(timeout, cts.Token);
                var completedTask = await Task.WhenAny(task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"操作在 {timeout} 内未完成");
                }

                cts.Cancel(); // 取消超时任务
                return await task;
            }
        }

        /// <summary>
        /// 为任务添加超时
        /// </summary>
        /// <param name="task">原始任务</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="timeoutManager">超时管理器</param>
        /// <returns>带超时的任务</returns>
        public static async Task WithTimeout(this Task task, TimeSpan timeout, ITimeoutManager timeoutManager = null)
        {
            if (timeoutManager != null)
            {
                await timeoutManager.ExecuteWithTimeoutAsync(_ => task, timeout);
            }
            else
            {
                using var cts = new CancellationTokenSource(timeout);
                var timeoutTask = Task.Delay(timeout, cts.Token);
                var completedTask = await Task.WhenAny(task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"操作在 {timeout} 内未完成");
                }

                cts.Cancel(); // 取消超时任务
                await task;
            }
        }

        /// <summary>
        /// 创建带超时的取消令牌
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>取消令牌源</returns>
        public static CancellationTokenSource CreateTimeoutTokenSource(TimeSpan timeout)
        {
            return new CancellationTokenSource(timeout);
        }

        /// <summary>
        /// 合并超时和用户取消令牌
        /// </summary>
        /// <param name="userToken">用户取消令牌</param>
        /// <param name="timeout">超时时间</param>
        /// <returns>合并的取消令牌</returns>
        public static CancellationToken WithTimeout(this CancellationToken userToken, TimeSpan timeout)
        {
            if (userToken.IsCancellationRequested)
                return userToken;

            if (timeout == TimeSpan.FromMilliseconds(-1))
                return userToken;

            using var timeoutCts = new CancellationTokenSource(timeout);
            return CancellationTokenSource.CreateLinkedTokenSource(userToken, timeoutCts.Token).Token;
        }
    }

    /// <summary>
    /// 超时策略
    /// </summary>
    public static class TimeoutPolicies
    {
        /// <summary>
        /// 创建短超时策略（适用于快速操作）
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions Short()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromSeconds(5),
                TimeoutWarningThreshold = 0.7,
                EnableTimeoutWarnings = true
            };
        }

        /// <summary>
        /// 创建中等超时策略（适用于一般操作）
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions Medium()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromSeconds(30),
                TimeoutWarningThreshold = 0.8,
                EnableTimeoutWarnings = true
            };
        }

        /// <summary>
        /// 创建长超时策略（适用于耗时操作）
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions Long()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromMinutes(5),
                TimeoutWarningThreshold = 0.9,
                EnableTimeoutWarnings = true
            };
        }

        /// <summary>
        /// 创建自定义超时策略
        /// </summary>
        /// <param name="timeout">默认超时时间</param>
        /// <param name="warningThreshold">警告阈值</param>
        /// <param name="enableWarnings">是否启用警告</param>
        /// <returns>超时选项</returns>
        public static TimeoutOptions Custom(TimeSpan timeout, double warningThreshold = 0.8, bool enableWarnings = true)
        {
            return new TimeoutOptions
            {
                DefaultTimeout = timeout,
                TimeoutWarningThreshold = warningThreshold,
                EnableTimeoutWarnings = enableWarnings
            };
        }

        /// <summary>
        /// 创建HTTP操作超时策略
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions ForHttp()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromSeconds(30),
                TimeoutWarningThreshold = 0.8,
                EnableTimeoutWarnings = true,
                MaxActiveOperations = 500
            };
        }

        /// <summary>
        /// 创建数据库操作超时策略
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions ForDatabase()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromSeconds(60),
                TimeoutWarningThreshold = 0.9,
                EnableTimeoutWarnings = true,
                MaxActiveOperations = 100
            };
        }

        /// <summary>
        /// 创建文件操作超时策略
        /// </summary>
        /// <returns>超时选项</returns>
        public static TimeoutOptions ForFileOperations()
        {
            return new TimeoutOptions
            {
                DefaultTimeout = TimeSpan.FromMinutes(2),
                TimeoutWarningThreshold = 0.9,
                EnableTimeoutWarnings = true,
                MaxActiveOperations = 50
            };
        }
    }
} 