using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Transport;

namespace Wombat.Extensions.JsonRpc.Client.Pool
{
    /// <summary>
    /// 池化连接实现
    /// </summary>
    public class PooledConnection : IPooledConnection
    {
        private readonly ITwoWayChannel _channel;
        private readonly ILogger<PooledConnection> _logger;
        private readonly object _lock = new object();
        private readonly ConnectionStatistics _statistics;
        private readonly ConnectionProperties _properties;
        private readonly SemaphoreSlim _leaseSemaphore;
        
        private volatile ConnectionState _state;
        private volatile bool _disposed;
        private DateTime _lastValidationTime;
        private DateTime _lastUsedAt;
        private long _useCount;
        private ConnectionLease _currentLease;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="endpoint">连接端点</param>
        /// <param name="channel">底层传输通道</param>
        /// <param name="logger">日志记录器</param>
        public PooledConnection(
            string connectionId,
            ConnectionEndpoint endpoint,
            ITwoWayChannel channel,
            ILogger<PooledConnection> logger = null)
        {
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _logger = logger;
            
            _statistics = new ConnectionStatistics();
            _properties = new ConnectionProperties();
            _leaseSemaphore = new SemaphoreSlim(1, 1);
            
            _state = ConnectionState.Created;
            _lastValidationTime = DateTime.UtcNow;
            _lastUsedAt = DateTime.UtcNow;
            _useCount = 0;
            
            CreatedAt = DateTime.UtcNow;
            
            _logger?.LogDebug("已创建池化连接: {ConnectionId} -> {Endpoint}", 
                ConnectionId, Endpoint);
        }

        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// 连接端点
        /// </summary>
        public ConnectionEndpoint Endpoint { get; }

        /// <summary>
        /// 底层传输通道
        /// </summary>
        public ITwoWayChannel Channel => _channel;

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionState State => _state;

        /// <summary>
        /// 是否健康
        /// </summary>
        public bool IsHealthy => _state == ConnectionState.Connected || 
                               _state == ConnectionState.Idle;

        /// <summary>
        /// 是否可用
        /// </summary>
        public bool IsAvailable => _state == ConnectionState.Idle && 
                                  _currentLease == null;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsedAt => _lastUsedAt;

        /// <summary>
        /// 使用次数
        /// </summary>
        public long UseCount => _useCount;

        /// <summary>
        /// 连接统计信息
        /// </summary>
        public ConnectionStatistics Statistics => _statistics;

        /// <summary>
        /// 连接属性
        /// </summary>
        public ConnectionProperties Properties => _properties;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 连接错误事件
        /// </summary>
        public event EventHandler<ConnectionErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 获取连接使用权
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接使用权</returns>
        public async Task<IConnectionLease> AcquireAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var acquisitionStart = DateTime.UtcNow;
            
            try
            {
                await _leaseSemaphore.WaitAsync(cancellationToken);
                
                if (_currentLease != null)
                {
                    throw new InvalidOperationException("连接已被占用");
                }

                // 验证连接状态
                if (!IsHealthy)
                {
                    throw new InvalidOperationException($"连接不健康: {_state}");
                }

                // 创建租约
                var lease = new ConnectionLease(this);
                _currentLease = lease;
                
                // 更新状态和统计信息
                ChangeState(ConnectionState.InUse, "连接被获取");
                Interlocked.Increment(ref _useCount);
                _lastUsedAt = DateTime.UtcNow;
                
                _statistics.UseCount++;
                
                var acquisitionTime = (DateTime.UtcNow - acquisitionStart).TotalMilliseconds;
                _logger?.LogDebug("连接使用权已获取: {ConnectionId}, 耗时: {AcquisitionTime}ms", 
                    ConnectionId, acquisitionTime);
                
                return lease;
            }
            catch (Exception ex)
            {
                _leaseSemaphore.Release();
                
                _logger?.LogError(ex, "获取连接使用权失败: {ConnectionId}", ConnectionId);
                OnErrorOccurred(ex, "获取连接使用权失败", false);
                
                throw;
            }
        }

        /// <summary>
        /// 获取连接使用权（同步版本）
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>连接使用权</returns>
        public IConnectionLease Acquire(TimeSpan? timeout = null)
        {
            var cancellationToken = timeout.HasValue 
                ? new CancellationTokenSource(timeout.Value).Token 
                : CancellationToken.None;
                
            return AcquireAsync(cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 释放连接使用权
        /// </summary>
        /// <param name="lease">连接使用权</param>
        public void Release(IConnectionLease lease)
        {
            if (lease == null || lease != _currentLease)
            {
                return;
            }

            lock (_lock)
            {
                if (_currentLease == lease)
                {
                    _currentLease = null;
                    ChangeState(ConnectionState.Idle, "连接使用权已释放");
                    _lastUsedAt = DateTime.UtcNow;
                    
                    _logger?.LogDebug("连接使用权已释放: {ConnectionId}", ConnectionId);
                }
            }
            
            _leaseSemaphore.Release();
        }

        /// <summary>
        /// 验证连接健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否健康</returns>
        public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (_state == ConnectionState.Closed || _state == ConnectionState.Error)
            {
                return false;
            }
            
            try
            {
                ChangeState(ConnectionState.Validating, "正在验证连接健康状态");
                
                // 检查底层通道状态
                if (_channel == null || _channel.InputStream == null || _channel.OutputStream == null)
                {
                    _logger?.LogWarning("连接验证失败: 底层通道无效 - {ConnectionId}", ConnectionId);
                    ChangeState(ConnectionState.Error, "底层通道无效");
                    return false;
                }
                
                // 可以添加更多验证逻辑，如ping测试等
                
                _lastValidationTime = DateTime.UtcNow;
                ChangeState(ConnectionState.Idle, "连接验证成功");
                
                _logger?.LogDebug("连接验证成功: {ConnectionId}", ConnectionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "连接验证失败: {ConnectionId}", ConnectionId);
                ChangeState(ConnectionState.Error, "连接验证异常");
                OnErrorOccurred(ex, "连接验证失败", false);
                return false;
            }
        }

        /// <summary>
        /// 重置连接状态
        /// </summary>
        /// <returns>重置任务</returns>
        public async Task ResetAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                _logger?.LogDebug("正在重置连接状态: {ConnectionId}", ConnectionId);
                
                // 等待当前租约释放
                while (_currentLease != null)
                {
                    await Task.Delay(100);
                }
                
                // 重置统计信息
                _statistics.ErrorCount = 0;
                _lastValidationTime = DateTime.UtcNow;
                _lastUsedAt = DateTime.UtcNow;
                
                // 重置状态
                ChangeState(ConnectionState.Idle, "连接已重置");
                
                _logger?.LogDebug("连接状态重置完成: {ConnectionId}", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "重置连接状态失败: {ConnectionId}", ConnectionId);
                OnErrorOccurred(ex, "重置连接状态失败", false);
                throw;
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="force">是否强制关闭</param>
        /// <returns>关闭任务</returns>
        public async Task CloseAsync(bool force = false)
        {
            if (_state == ConnectionState.Closed)
            {
                return;
            }
            
            try
            {
                ChangeState(ConnectionState.Closing, "正在关闭连接");
                
                if (!force && _currentLease != null)
                {
                    // 等待当前租约释放
                    var timeout = TimeSpan.FromSeconds(10);
                    var start = DateTime.UtcNow;
                    
                    while (_currentLease != null && DateTime.UtcNow - start < timeout)
                    {
                        await Task.Delay(100);
                    }
                    
                    if (_currentLease != null)
                    {
                        _logger?.LogWarning("强制关闭连接: 租约未在超时时间内释放 - {ConnectionId}", ConnectionId);
                        _currentLease?.Invalidate();
                        _currentLease = null;
                    }
                }
                
                // 关闭底层通道
                _channel?.Dispose();
                
                ChangeState(ConnectionState.Closed, "连接已关闭");
                
                _logger?.LogDebug("连接已关闭: {ConnectionId}", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭连接失败: {ConnectionId}", ConnectionId);
                OnErrorOccurred(ex, "关闭连接失败", true);
                throw;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
            
            try
            {
                CloseAsync(true).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放连接资源时发生异常: {ConnectionId}", ConnectionId);
            }
            
            _leaseSemaphore?.Dispose();
            _channel?.Dispose();
            
            _logger?.LogDebug("连接资源已释放: {ConnectionId}", ConnectionId);
        }

        /// <summary>
        /// 改变连接状态
        /// </summary>
        /// <param name="newState">新状态</param>
        /// <param name="reason">状态改变原因</param>
        private void ChangeState(ConnectionState newState, string reason = null)
        {
            var oldState = _state;
            if (oldState == newState)
            {
                return;
            }
            
            _state = newState;
            
            _logger?.LogDebug("连接状态变化: {ConnectionId} {OldState} -> {NewState} ({Reason})", 
                ConnectionId, oldState, newState, reason);
            
            try
            {
                StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    ConnectionId = ConnectionId,
                    OldState = oldState,
                    NewState = newState,
                    Reason = reason
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发状态变化事件时发生异常: {ConnectionId}", ConnectionId);
            }
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="exception">异常</param>
        /// <param name="errorType">错误类型</param>
        /// <param name="isFatal">是否为致命错误</param>
        private void OnErrorOccurred(Exception exception, string errorType, bool isFatal)
        {
            _statistics.ErrorCount++;
            
            try
            {
                ErrorOccurred?.Invoke(this, new ConnectionErrorEventArgs
                {
                    ConnectionId = ConnectionId,
                    Exception = exception,
                    ErrorType = errorType,
                    IsFatal = isFatal
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发错误事件时发生异常: {ConnectionId}", ConnectionId);
            }
        }

        /// <summary>
        /// 检查是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledConnection));
            }
        }
    }

    /// <summary>
    /// 连接租约实现
    /// </summary>
    internal class ConnectionLease : IConnectionLease
    {
        private readonly PooledConnection _connection;
        private volatile bool _disposed;
        private volatile bool _valid;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connection">连接实例</param>
        public ConnectionLease(PooledConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _valid = true;
            
            LeaseId = Guid.NewGuid().ToString("N");
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 连接实例
        /// </summary>
        public IPooledConnection Connection => _connection;

        /// <summary>
        /// 租约ID
        /// </summary>
        public string LeaseId { get; }

        /// <summary>
        /// 租约创建时间
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 租约是否有效
        /// </summary>
        public bool IsValid => _valid && !_disposed;

        /// <summary>
        /// 底层传输通道
        /// </summary>
        public ITwoWayChannel Channel => _connection.Channel;

        /// <summary>
        /// 标记租约为无效
        /// </summary>
        public void Invalidate()
        {
            _valid = false;
        }

        /// <summary>
        /// 释放租约
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
            _valid = false;
            
            _connection?.Release(this);
        }
    }
} 