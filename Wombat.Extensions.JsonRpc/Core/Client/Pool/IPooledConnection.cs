using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Core.Client.Pool
{
    /// <summary>
    /// 池化连接接口
    /// 表示连接池中的一个连接实例
    /// </summary>
    public interface IPooledConnection : IDisposable
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// 连接端点
        /// </summary>
        ConnectionEndpoint Endpoint { get; }

        /// <summary>
        /// 底层传输通道
        /// </summary>
        ITwoWayChannel Channel { get; }

        /// <summary>
        /// 连接状态
        /// </summary>
        ConnectionState State { get; }

        /// <summary>
        /// 是否健康
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// 是否可用
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 创建时间
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        DateTime LastUsedAt { get; }

        /// <summary>
        /// 使用次数
        /// </summary>
        long UseCount { get; }

        /// <summary>
        /// 连接统计信息
        /// </summary>
        ConnectionStatistics Statistics { get; }

        /// <summary>
        /// 连接属性
        /// </summary>
        ConnectionProperties Properties { get; }

        /// <summary>
        /// 获取连接使用权
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接使用权</returns>
        Task<IConnectionLease> AcquireAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取连接使用权（同步版本）
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>连接使用权</returns>
        IConnectionLease Acquire(TimeSpan? timeout = null);

        /// <summary>
        /// 释放连接使用权
        /// </summary>
        /// <param name="lease">连接使用权</param>
        void Release(IConnectionLease lease);

        /// <summary>
        /// 验证连接健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否健康</returns>
        Task<bool> ValidateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 重置连接状态
        /// </summary>
        /// <returns>重置任务</returns>
        Task ResetAsync();

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="force">是否强制关闭</param>
        /// <returns>关闭任务</returns>
        Task CloseAsync(bool force = false);

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 连接错误事件
        /// </summary>
        event EventHandler<ConnectionErrorEventArgs> ErrorOccurred;
    }

    /// <summary>
    /// 连接使用权接口
    /// </summary>
    public interface IConnectionLease : IDisposable
    {
        /// <summary>
        /// 连接实例
        /// </summary>
        IPooledConnection Connection { get; }

        /// <summary>
        /// 租约ID
        /// </summary>
        string LeaseId { get; }

        /// <summary>
        /// 租约创建时间
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// 租约是否有效
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 底层传输通道
        /// </summary>
        ITwoWayChannel Channel { get; }

        /// <summary>
        /// 标记租约为无效
        /// </summary>
        void Invalidate();
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// 已创建
        /// </summary>
        Created,

        /// <summary>
        /// 正在连接
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 空闲中
        /// </summary>
        Idle,

        /// <summary>
        /// 使用中
        /// </summary>
        InUse,

        /// <summary>
        /// 正在验证
        /// </summary>
        Validating,

        /// <summary>
        /// 出现错误
        /// </summary>
        Error,

        /// <summary>
        /// 正在关闭
        /// </summary>
        Closing,

        /// <summary>
        /// 已关闭
        /// </summary>
        Closed
    }

    /// <summary>
    /// 连接统计信息
    /// </summary>
    public class ConnectionStatistics
    {
        /// <summary>
        /// 连接次数
        /// </summary>
        public long ConnectCount { get; set; }

        /// <summary>
        /// 断开次数
        /// </summary>
        public long DisconnectCount { get; set; }

        /// <summary>
        /// 使用次数
        /// </summary>
        public long UseCount { get; set; }

        /// <summary>
        /// 错误次数
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// 发送字节数
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// 接收字节数
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// 平均响应时间（毫秒）
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// 总连接时间（毫秒）
        /// </summary>
        public long TotalConnectionTimeMs { get; set; }

        /// <summary>
        /// 上次统计时间
        /// </summary>
        public DateTime LastStatisticsTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 连接属性
    /// </summary>
    public class ConnectionProperties
    {
        /// <summary>
        /// 连接优先级
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 连接权重
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// 连接标签
        /// </summary>
        public string[] Tags { get; set; } = new string[0];

        /// <summary>
        /// 连接元数据
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; } = 
            new System.Collections.Generic.Dictionary<string, object>();

        /// <summary>
        /// 是否为预热连接
        /// </summary>
        public bool IsPrewarmed { get; set; }

        /// <summary>
        /// 是否为专用连接
        /// </summary>
        public bool IsExclusive { get; set; }

        /// <summary>
        /// 连接版本
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 连接分组
        /// </summary>
        public string Group { get; set; }
    }

    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// 旧状态
        /// </summary>
        public ConnectionState OldState { get; set; }

        /// <summary>
        /// 新状态
        /// </summary>
        public ConnectionState NewState { get; set; }

        /// <summary>
        /// 状态变化时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 状态变化原因
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// 连接错误事件参数
    /// </summary>
    public class ConnectionErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 错误发生时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 错误类型
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// 是否为致命错误
        /// </summary>
        public bool IsFatal { get; set; }
    }
} 