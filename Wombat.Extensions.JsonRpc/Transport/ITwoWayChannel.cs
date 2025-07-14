using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Transport
{
    /// <summary>
    /// 双向通信通道接口 - 统一各种传输层的抽象
    /// </summary>
    public interface ITwoWayChannel : IDisposable
    {
        /// <summary>
        /// 输入流
        /// </summary>
        Stream InputStream { get; }

        /// <summary>
        /// 输出流
        /// </summary>
        Stream OutputStream { get; }

        /// <summary>
        /// 通道是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 传输类型
        /// </summary>
        TransportType TransportType { get; }

        /// <summary>
        /// 远程端点信息
        /// </summary>
        string RemoteEndPoint { get; }

        /// <summary>
        /// 本地端点信息
        /// </summary>
        string LocalEndPoint { get; }

        /// <summary>
        /// 连接事件
        /// </summary>
        event EventHandler<ChannelEventArgs> Connected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        event EventHandler<ChannelEventArgs> Disconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<ChannelErrorEventArgs> Error;

        /// <summary>
        /// 异步连接到远程端点
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接任务</returns>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步断开连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>断开连接任务</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送心跳包
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>心跳任务</returns>
        Task SendHeartbeatAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        /// <returns>连接统计信息</returns>
        ChannelStatistics GetStatistics();
    }

    /// <summary>
    /// 传输类型枚举
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// TCP传输
        /// </summary>
        Tcp,

        /// <summary>
        /// WebSocket传输
        /// </summary>
        WebSocket,

        /// <summary>
        /// HTTP传输
        /// </summary>
        Http,

        /// <summary>
        /// Named Pipe传输
        /// </summary>
        NamedPipe,

        /// <summary>
        /// 内存传输
        /// </summary>
        Memory
    }

    /// <summary>
    /// 通道事件参数
    /// </summary>
    public class ChannelEventArgs : EventArgs
    {
        /// <summary>
        /// 通道实例
        /// </summary>
        public ITwoWayChannel Channel { get; set; }

        /// <summary>
        /// 事件时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 事件消息
        /// </summary>
        public string Message { get; set; }

        public ChannelEventArgs(ITwoWayChannel channel, string message = null)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 通道错误事件参数
    /// </summary>
    public class ChannelErrorEventArgs : ChannelEventArgs
    {
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 错误代码
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 是否可恢复
        /// </summary>
        public bool IsRecoverable { get; set; }

        public ChannelErrorEventArgs(ITwoWayChannel channel, Exception exception, string message = null, int errorCode = 0, bool isRecoverable = false)
            : base(channel, message)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ErrorCode = errorCode;
            IsRecoverable = isRecoverable;
        }
    }

    /// <summary>
    /// 通道统计信息
    /// </summary>
    public class ChannelStatistics
    {
        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// 连接持续时间
        /// </summary>
        public TimeSpan ConnectionDuration => DateTime.UtcNow - ConnectedAt;

        /// <summary>
        /// 发送字节数
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// 接收字节数
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// 发送消息数
        /// </summary>
        public long MessagesSent { get; set; }

        /// <summary>
        /// 接收消息数
        /// </summary>
        public long MessagesReceived { get; set; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// 错误计数
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectCount { get; set; }

        /// <summary>
        /// 平均延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// 最大延迟（毫秒）
        /// </summary>
        public double MaxLatencyMs { get; set; }

        /// <summary>
        /// 传输效率（字节/秒）
        /// </summary>
        public double ThroughputBytesPerSecond
        {
            get
            {
                var duration = ConnectionDuration.TotalSeconds;
                return duration > 0 ? (BytesSent + BytesReceived) / duration : 0;
            }
        }

        /// <summary>
        /// 消息效率（消息/秒）
        /// </summary>
        public double MessageRatePerSecond
        {
            get
            {
                var duration = ConnectionDuration.TotalSeconds;
                return duration > 0 ? (MessagesSent + MessagesReceived) / duration : 0;
            }
        }
    }
} 