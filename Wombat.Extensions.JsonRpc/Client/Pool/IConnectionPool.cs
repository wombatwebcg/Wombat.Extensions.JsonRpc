using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Client.Pool
{
    /// <summary>
    /// 连接池接口
    /// 提供连接复用、管理和监控功能
    /// </summary>
    public interface IConnectionPool : IDisposable
    {
        /// <summary>
        /// 连接池统计信息
        /// </summary>
        ConnectionPoolStatistics Statistics { get; }

        /// <summary>
        /// 连接池配置
        /// </summary>
        ConnectionPoolOptions Options { get; }

        /// <summary>
        /// 获取连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>池化连接</returns>
        Task<IPooledConnection> GetConnectionAsync(ConnectionEndpoint endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取连接（同步版本）
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="timeout">超时时间</param>
        /// <returns>池化连接</returns>
        IPooledConnection GetConnection(ConnectionEndpoint endpoint, TimeSpan? timeout = null);

        /// <summary>
        /// 释放连接回池
        /// </summary>
        /// <param name="connection">池化连接</param>
        /// <param name="forceClose">是否强制关闭</param>
        /// <returns>释放任务</returns>
        Task ReleaseConnectionAsync(IPooledConnection connection, bool forceClose = false);

        /// <summary>
        /// 创建新连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新连接</returns>
        Task<IPooledConnection> CreateConnectionAsync(ConnectionEndpoint endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证连接健康状态
        /// </summary>
        /// <param name="connection">连接</param>
        /// <returns>是否健康</returns>
        Task<bool> ValidateConnectionAsync(IPooledConnection connection);

        /// <summary>
        /// 清理过期连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理任务</returns>
        Task CleanupExpiredConnectionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 关闭指定端点的所有连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>关闭任务</returns>
        Task CloseEndpointConnectionsAsync(ConnectionEndpoint endpoint);

        /// <summary>
        /// 关闭连接池中的所有连接
        /// </summary>
        /// <returns>关闭任务</returns>
        Task CloseAllConnectionsAsync();

        /// <summary>
        /// 获取所有活动连接
        /// </summary>
        /// <returns>活动连接列表</returns>
        IEnumerable<IPooledConnection> GetActiveConnections();

        /// <summary>
        /// 获取指定端点的连接数
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>连接数信息</returns>
        ConnectionCount GetConnectionCount(ConnectionEndpoint endpoint);

        /// <summary>
        /// 连接池事件
        /// </summary>
        event EventHandler<ConnectionPoolEventArgs> ConnectionCreated;
        event EventHandler<ConnectionPoolEventArgs> ConnectionDestroyed;
        event EventHandler<ConnectionPoolEventArgs> ConnectionAcquired;
        event EventHandler<ConnectionPoolEventArgs> ConnectionReleased;
        event EventHandler<ConnectionPoolEventArgs> ConnectionValidationFailed;
    }

    /// <summary>
    /// 连接端点信息
    /// </summary>
    public class ConnectionEndpoint
    {
        /// <summary>
        /// 端点地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 端点端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 传输协议类型
        /// </summary>
        public TransportType TransportType { get; set; }

        /// <summary>
        /// 连接参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建TCP端点
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="port">端口</param>
        /// <returns>TCP端点</returns>
        public static ConnectionEndpoint CreateTcp(string address, int port)
        {
            return new ConnectionEndpoint
            {
                Address = address,
                Port = port,
                TransportType = TransportType.Tcp
            };
        }

        /// <summary>
        /// 创建WebSocket端点
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="port">端口</param>
        /// <returns>WebSocket端点</returns>
        public static ConnectionEndpoint CreateWebSocket(string address, int port)
        {
            return new ConnectionEndpoint
            {
                Address = address,
                Port = port,
                TransportType = TransportType.WebSocket
            };
        }

        /// <summary>
        /// 创建Named Pipe端点
        /// </summary>
        /// <param name="pipeName">管道名称</param>
        /// <returns>Named Pipe端点</returns>
        public static ConnectionEndpoint CreateNamedPipe(string pipeName)
        {
            return new ConnectionEndpoint
            {
                Address = pipeName,
                Port = 0,
                TransportType = TransportType.NamedPipe
            };
        }

        /// <summary>
        /// 获取端点键
        /// </summary>
        /// <returns>端点键</returns>
        public string GetEndpointKey()
        {
            return $"{TransportType}://{Address}:{Port}";
        }

        public override string ToString()
        {
            return GetEndpointKey();
        }

        public override bool Equals(object obj)
        {
            if (obj is ConnectionEndpoint other)
            {
                return Address == other.Address && 
                       Port == other.Port && 
                       TransportType == other.TransportType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (Address?.GetHashCode() ?? 0);
                hash = hash * 23 + Port.GetHashCode();
                hash = hash * 23 + TransportType.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// 传输协议类型
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// TCP协议
        /// </summary>
        Tcp,

        /// <summary>
        /// WebSocket协议
        /// </summary>
        WebSocket,

        /// <summary>
        /// Named Pipe协议
        /// </summary>
        NamedPipe,

        /// <summary>
        /// HTTP协议
        /// </summary>
        Http
    }

    /// <summary>
    /// 连接数信息
    /// </summary>
    public class ConnectionCount
    {
        /// <summary>
        /// 活动连接数
        /// </summary>
        public int Active { get; set; }

        /// <summary>
        /// 空闲连接数
        /// </summary>
        public int Idle { get; set; }

        /// <summary>
        /// 总连接数
        /// </summary>
        public int Total => Active + Idle;

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int Maximum { get; set; }

        /// <summary>
        /// 最小连接数
        /// </summary>
        public int Minimum { get; set; }
    }

    /// <summary>
    /// 连接池事件参数
    /// </summary>
    public class ConnectionPoolEventArgs : EventArgs
    {
        /// <summary>
        /// 连接端点
        /// </summary>
        public ConnectionEndpoint Endpoint { get; set; }

        /// <summary>
        /// 连接实例
        /// </summary>
        public IPooledConnection Connection { get; set; }

        /// <summary>
        /// 事件时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 附加信息
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
} 