using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Client.Pool
{
    /// <summary>
    /// 端点连接池
    /// 管理单个端点的连接集合
    /// </summary>
    internal class EndpointConnectionPool : IDisposable
    {
        private readonly ConnectionEndpoint _endpoint;
        private readonly ConnectionPoolOptions _options;
        private readonly ConnectionPool _parentPool;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<IPooledConnection> _idleConnections;
        private readonly ConcurrentDictionary<string, IPooledConnection> _allConnections;
        private readonly Timer _prewarmTimer;
        
        private volatile bool _disposed;
        private int _creatingConnections;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="options">连接池选项</param>
        /// <param name="parentPool">父连接池</param>
        /// <param name="logger">日志记录器</param>
        public EndpointConnectionPool(
            ConnectionEndpoint endpoint,
            ConnectionPoolOptions options,
            ConnectionPool parentPool,
            ILogger logger)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _parentPool = parentPool ?? throw new ArgumentNullException(nameof(parentPool));
            _logger = logger;
            
            _semaphore = new SemaphoreSlim(_options.MaxConnectionsPerEndpoint, _options.MaxConnectionsPerEndpoint);
            _idleConnections = new ConcurrentQueue<IPooledConnection>();
            _allConnections = new ConcurrentDictionary<string, IPooledConnection>();
            
            // 如果启用预热，启动预热定时器
            if (_options.EnablePrewarming)
            {
                _prewarmTimer = new Timer(PrewarmCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            }
            
            _logger?.LogDebug("端点连接池已创建: {Endpoint}", _endpoint);
        }

        /// <summary>
        /// 获取连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接</returns>
        public async Task<IPooledConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // 首先尝试获取空闲连接
            if (_idleConnections.TryDequeue(out var connection))
            {
                if (await ValidateConnectionAsync(connection))
                {
                    _logger?.LogDebug("使用空闲连接: {ConnectionId}", connection.ConnectionId);
                    return connection;
                }
                else
                {
                    // 连接无效，移除并销毁
                    await RemoveConnectionAsync(connection);
                }
            }
            
            // 如果没有空闲连接，尝试创建新连接
            if (_allConnections.Count < _options.MaxConnectionsPerEndpoint)
            {
                connection = await CreateConnectionAsync(cancellationToken);
                if (connection != null)
                {
                    _logger?.LogDebug("创建新连接: {ConnectionId}", connection.ConnectionId);
                    return connection;
                }
            }
            
            // 等待连接可用
            var timeout = TimeSpan.FromMilliseconds(_options.AcquireTimeoutMs);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            try
            {
                await _semaphore.WaitAsync(cts.Token);
                
                // 再次尝试获取空闲连接
                if (_idleConnections.TryDequeue(out connection))
                {
                    if (await ValidateConnectionAsync(connection))
                    {
                        _logger?.LogDebug("等待后获取空闲连接: {ConnectionId}", connection.ConnectionId);
                        return connection;
                    }
                    else
                    {
                        await RemoveConnectionAsync(connection);
                    }
                }
                
                // 如果仍然没有，创建新连接
                connection = await CreateConnectionAsync(cts.Token);
                if (connection != null)
                {
                    _logger?.LogDebug("等待后创建新连接: {ConnectionId}", connection.ConnectionId);
                    return connection;
                }
                
                throw new TimeoutException($"获取连接超时: {_endpoint}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 释放连接
        /// </summary>
        /// <param name="connection">连接</param>
        /// <param name="forceClose">是否强制关闭</param>
        /// <returns>释放任务</returns>
        public async Task ReleaseConnectionAsync(IPooledConnection connection, bool forceClose = false)
        {
            if (connection == null)
                return;
            
            if (forceClose || !connection.IsHealthy)
            {
                await RemoveConnectionAsync(connection);
                return;
            }
            
            // 检查连接是否过期
            if (IsConnectionExpired(connection))
            {
                await RemoveConnectionAsync(connection);
                return;
            }
            
            // 将连接放回空闲队列
            _idleConnections.Enqueue(connection);
            
            _logger?.LogDebug("连接已释放回池: {ConnectionId}", connection.ConnectionId);
        }

        /// <summary>
        /// 清理过期连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理任务</returns>
        public async Task CleanupExpiredConnectionsAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;
            
            var expiredConnections = new List<IPooledConnection>();
            var remainingConnections = new List<IPooledConnection>();
            
            // 检查空闲连接
            while (_idleConnections.TryDequeue(out var connection))
            {
                if (IsConnectionExpired(connection))
                {
                    expiredConnections.Add(connection);
                }
                else
                {
                    remainingConnections.Add(connection);
                }
            }
            
            // 将未过期的连接放回队列
            foreach (var connection in remainingConnections)
            {
                _idleConnections.Enqueue(connection);
            }
            
            // 关闭过期连接
            var closeTasks = expiredConnections.Select(conn => RemoveConnectionAsync(conn));
            await Task.WhenAll(closeTasks);
            
            _logger?.LogDebug("过期连接清理完成: {Endpoint}, 清理数量: {Count}", 
                _endpoint, expiredConnections.Count);
        }

        /// <summary>
        /// 验证连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证任务</returns>
        public async Task ValidateConnectionsAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;
            
            var validationTasks = new List<Task>();
            
            foreach (var connection in _allConnections.Values)
            {
                if (connection.IsAvailable)
                {
                    validationTasks.Add(ValidateConnectionAsync(connection));
                }
            }
            
            await Task.WhenAll(validationTasks);
            
            _logger?.LogDebug("连接验证完成: {Endpoint}", _endpoint);
        }

        /// <summary>
        /// 关闭所有连接
        /// </summary>
        /// <returns>关闭任务</returns>
        public async Task CloseAllConnectionsAsync()
        {
            if (_disposed)
                return;
            
            var closeTasks = new List<Task>();
            
            foreach (var connection in _allConnections.Values)
            {
                closeTasks.Add(RemoveConnectionAsync(connection));
            }
            
            await Task.WhenAll(closeTasks);
            
            _logger?.LogDebug("所有连接已关闭: {Endpoint}", _endpoint);
        }

        /// <summary>
        /// 获取活动连接
        /// </summary>
        /// <returns>活动连接列表</returns>
        public IEnumerable<IPooledConnection> GetActiveConnections()
        {
            return _allConnections.Values.Where(c => c.State == ConnectionState.InUse);
        }

        /// <summary>
        /// 获取连接数
        /// </summary>
        /// <returns>连接数信息</returns>
        public ConnectionCount GetConnectionCount()
        {
            var total = _allConnections.Count;
            var active = _allConnections.Values.Count(c => c.State == ConnectionState.InUse);
            var idle = _allConnections.Values.Count(c => c.State == ConnectionState.Idle);
            
            return new ConnectionCount
            {
                Active = active,
                Idle = idle,
                Maximum = _options.MaxConnectionsPerEndpoint,
                Minimum = _options.MinConnectionsPerEndpoint
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            
            _disposed = true;
            
            try
            {
                CloseAllConnectionsAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放端点连接池时发生异常: {Endpoint}", _endpoint);
            }
            
            _prewarmTimer?.Dispose();
            _semaphore?.Dispose();
            
            _logger?.LogDebug("端点连接池已释放: {Endpoint}", _endpoint);
        }

        /// <summary>
        /// 创建连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新连接</returns>
        private async Task<IPooledConnection> CreateConnectionAsync(CancellationToken cancellationToken)
        {
            if (_disposed || _allConnections.Count >= _options.MaxConnectionsPerEndpoint)
                return null;
            
            if (Interlocked.Increment(ref _creatingConnections) > 1)
            {
                // 已有其他线程在创建连接
                Interlocked.Decrement(ref _creatingConnections);
                return null;
            }
            
            try
            {
                var connection = await _parentPool.CreateConnectionAsync(_endpoint, cancellationToken);
                
                // 订阅连接事件
                connection.StateChanged += OnConnectionStateChanged;
                connection.ErrorOccurred += OnConnectionErrorOccurred;
                
                _allConnections[connection.ConnectionId] = connection;
                
                _logger?.LogDebug("端点连接已创建: {ConnectionId} -> {Endpoint}", 
                    connection.ConnectionId, _endpoint);
                
                return connection;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建端点连接失败: {Endpoint}", _endpoint);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _creatingConnections);
            }
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        /// <param name="connection">连接</param>
        /// <returns>移除任务</returns>
        private async Task RemoveConnectionAsync(IPooledConnection connection)
        {
            if (connection == null)
                return;
            
            _allConnections.TryRemove(connection.ConnectionId, out _);
            
            // 取消订阅事件
            connection.StateChanged -= OnConnectionStateChanged;
            connection.ErrorOccurred -= OnConnectionErrorOccurred;
            
            try
            {
                await connection.CloseAsync(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭连接时发生异常: {ConnectionId}", connection.ConnectionId);
            }
            
            connection.Dispose();
            
            _logger?.LogDebug("连接已移除: {ConnectionId}", connection.ConnectionId);
        }

        /// <summary>
        /// 验证连接
        /// </summary>
        /// <param name="connection">连接</param>
        /// <returns>是否有效</returns>
        private async Task<bool> ValidateConnectionAsync(IPooledConnection connection)
        {
            if (connection == null)
                return false;
            
            try
            {
                // 使用自定义验证器
                if (_options.ConnectionValidator != null)
                {
                    return _options.ConnectionValidator(connection);
                }
                
                // 使用内置验证
                return await connection.ValidateAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "验证连接失败: {ConnectionId}", connection.ConnectionId);
                return false;
            }
        }

        /// <summary>
        /// 检查连接是否过期
        /// </summary>
        /// <param name="connection">连接</param>
        /// <returns>是否过期</returns>
        private bool IsConnectionExpired(IPooledConnection connection)
        {
            if (connection == null)
                return true;
            
            var now = DateTime.UtcNow;
            
            // 检查生存时间
            if (_options.MaxLifetimeMs > 0)
            {
                var lifetime = (now - connection.CreatedAt).TotalMilliseconds;
                if (lifetime > _options.MaxLifetimeMs)
                {
                    return true;
                }
            }
            
            // 检查空闲时间
            if (_options.IdleTimeoutMs > 0)
            {
                var idleTime = (now - connection.LastUsedAt).TotalMilliseconds;
                if (idleTime > _options.IdleTimeoutMs)
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 预热回调
        /// </summary>
        /// <param name="state">状态</param>
        private void PrewarmCallback(object state)
        {
            try
            {
                var currentCount = _allConnections.Count;
                var minCount = _options.MinConnectionsPerEndpoint;
                
                if (currentCount < minCount)
                {
                    var createCount = minCount - currentCount;
                    var createTasks = new List<Task>();
                    
                    for (int i = 0; i < createCount; i++)
                    {
                        createTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var connection = await CreateConnectionAsync(CancellationToken.None);
                                if (connection != null)
                                {
                                    _idleConnections.Enqueue(connection);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "预热连接失败: {Endpoint}", _endpoint);
                            }
                        }));
                    }
                    
                    Task.WhenAll(createTasks).Wait();
                    
                    _logger?.LogDebug("连接预热完成: {Endpoint}, 创建数量: {Count}", 
                        _endpoint, createCount);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "预热任务执行失败: {Endpoint}", _endpoint);
            }
        }

        /// <summary>
        /// 连接状态变化处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            if (e.NewState == ConnectionState.Error || e.NewState == ConnectionState.Closed)
            {
                if (sender is IPooledConnection connection)
                {
                    Task.Run(async () => await RemoveConnectionAsync(connection));
                }
            }
        }

        /// <summary>
        /// 连接错误处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnConnectionErrorOccurred(object sender, ConnectionErrorEventArgs e)
        {
            if (e.IsFatal && sender is IPooledConnection connection)
            {
                Task.Run(async () => await RemoveConnectionAsync(connection));
            }
        }

        /// <summary>
        /// 检查是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EndpointConnectionPool));
            }
        }
    }
} 