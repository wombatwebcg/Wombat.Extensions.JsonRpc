using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Resilience.Core
{
    /// <summary>
    /// 断路器接口
    /// 实现断路器模式，防止级联故障
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        CircuitBreakerState State { get; }

        /// <summary>
        /// 失败计数
        /// </summary>
        int FailureCount { get; }

        /// <summary>
        /// 成功计数
        /// </summary>
        int SuccessCount { get; }

        /// <summary>
        /// 最后失败时间
        /// </summary>
        DateTime? LastFailureTime { get; }

        /// <summary>
        /// 下次尝试时间
        /// </summary>
        DateTime? NextAttemptTime { get; }

        /// <summary>
        /// 执行操作
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行操作（无返回值）
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// 手动打开断路器
        /// </summary>
        void Trip();

        /// <summary>
        /// 手动关闭断路器
        /// </summary>
        void Reset();

        /// <summary>
        /// 获取断路器统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        CircuitBreakerStatistics GetStatistics();

        /// <summary>
        /// 状态变更事件
        /// </summary>
        event EventHandler<CircuitBreakerStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 操作成功事件
        /// </summary>
        event EventHandler<CircuitBreakerOperationEventArgs> OperationSucceeded;

        /// <summary>
        /// 操作失败事件
        /// </summary>
        event EventHandler<CircuitBreakerOperationEventArgs> OperationFailed;

        /// <summary>
        /// 断路器被触发事件
        /// </summary>
        event EventHandler<CircuitBreakerStateChangedEventArgs> CircuitBreakerTripped;
    }

    /// <summary>
    /// 断路器状态
    /// </summary>
    public enum CircuitBreakerState
    {
        /// <summary>
        /// 关闭状态 - 正常运行
        /// </summary>
        Closed,

        /// <summary>
        /// 打开状态 - 断路器激活，阻止请求
        /// </summary>
        Open,

        /// <summary>
        /// 半开状态 - 测试服务是否恢复
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// 断路器统计信息
    /// </summary>
    public class CircuitBreakerStatistics
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public CircuitBreakerState State { get; set; }

        /// <summary>
        /// 总请求数
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// 成功请求数
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// 失败请求数
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// 被阻止的请求数
        /// </summary>
        public long RejectedRequests { get; set; }

        /// <summary>
        /// 失败率（百分比）
        /// </summary>
        public double FailureRate { get; set; }

        /// <summary>
        /// 平均响应时间（毫秒）
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// 最后失败时间
        /// </summary>
        public DateTime? LastFailureTime { get; set; }

        /// <summary>
        /// 最后成功时间
        /// </summary>
        public DateTime? LastSuccessTime { get; set; }

        /// <summary>
        /// 断路器打开次数
        /// </summary>
        public int CircuitBreakerOpenCount { get; set; }

        /// <summary>
        /// 当前失败计数
        /// </summary>
        public int CurrentFailureCount { get; set; }

        /// <summary>
        /// 下次尝试时间
        /// </summary>
        public DateTime? NextAttemptTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdatedTime { get; set; }
    }

    /// <summary>
    /// 断路器状态变更事件参数
    /// </summary>
    public class CircuitBreakerStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 之前状态
        /// </summary>
        public CircuitBreakerState PreviousState { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public CircuitBreakerState CurrentState { get; set; }

        /// <summary>
        /// 状态变更时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 变更原因
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// 失败计数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 统计信息
        /// </summary>
        public CircuitBreakerStatistics Statistics { get; set; }
    }

    /// <summary>
    /// 断路器操作事件参数
    /// </summary>
    public class CircuitBreakerOperationEventArgs : EventArgs
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public double ExecutionTime { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 断路器状态
        /// </summary>
        public CircuitBreakerState State { get; set; }

        /// <summary>
        /// 失败计数
        /// </summary>
        public int FailureCount { get; set; }
    }

    /// <summary>
    /// 断路器配置选项
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        public string Name { get; set; } = "DefaultCircuitBreaker";

        /// <summary>
        /// 失败阈值 - 连续失败多少次后打开断路器
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// 成功阈值 - 半开状态下连续成功多少次后关闭断路器
        /// </summary>
        public int SuccessThreshold { get; set; } = 3;

        /// <summary>
        /// 超时时间 - 断路器打开后多长时间进入半开状态
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 采样期间 - 统计失败率的时间窗口
        /// </summary>
        public TimeSpan SamplingPeriod { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 最小请求数 - 采样期间内最少需要多少请求才能计算失败率
        /// </summary>
        public int MinimumThroughput { get; set; } = 10;

        /// <summary>
        /// 失败率阈值 - 失败率超过多少百分比后打开断路器
        /// </summary>
        public double FailureRateThreshold { get; set; } = 50.0;

        /// <summary>
        /// 是否监控超时异常
        /// </summary>
        public bool MonitorTimeoutExceptions { get; set; } = true;

        /// <summary>
        /// 需要监控的异常类型
        /// </summary>
        public Type[] MonitoredExceptions { get; set; } = { typeof(Exception) };

        /// <summary>
        /// 忽略的异常类型
        /// </summary>
        public Type[] IgnoredExceptions { get; set; } = { typeof(ArgumentException) };

        /// <summary>
        /// 是否启用自动重置
        /// </summary>
        public bool EnableAutoReset { get; set; } = true;

        /// <summary>
        /// 最大统计条目数
        /// </summary>
        public int MaxStatisticsCount { get; set; } = 1000;
    }

    /// <summary>
    /// 断路器异常
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// 断路器名称
        /// </summary>
        public string CircuitBreakerName { get; }

        /// <summary>
        /// 下次尝试时间
        /// </summary>
        public DateTime? NextAttemptTime { get; }

        /// <summary>
        /// 失败计数
        /// </summary>
        public int FailureCount { get; }

        public CircuitBreakerOpenException(string circuitBreakerName, DateTime? nextAttemptTime = null, int failureCount = 0)
            : base($"断路器 '{circuitBreakerName}' 处于打开状态，请求被阻止")
        {
            CircuitBreakerName = circuitBreakerName;
            NextAttemptTime = nextAttemptTime;
            FailureCount = failureCount;
        }

        public CircuitBreakerOpenException(string circuitBreakerName, string message)
            : base(message)
        {
            CircuitBreakerName = circuitBreakerName;
        }

        public CircuitBreakerOpenException(string circuitBreakerName, string message, Exception innerException)
            : base(message, innerException)
        {
            CircuitBreakerName = circuitBreakerName;
        }
    }
} 