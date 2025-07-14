using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Wombat.Extensions.JsonRpc.Transport;

namespace Wombat.Extensions.JsonRpc.Client.Pool
{
    /// <summary>
    /// 连接池实现
    /// </summary>
    public class ConnectionPool : IConnectionPool
    {
        private readonly ConnectionPoolOptions _options;
        private readonly ILogger<ConnectionPool> _logger;
        private readonly ConnectionPoolStatistics _statistics;
        private readonly ConcurrentDictionary<string, EndpointConnectionPool> _endpointPools;
        private readonly Timer _cleanupTimer;
        private readonly Timer _validationTimer;
        private readonly SemaphoreSlim _createConnectionSemaphore;
        
        private volatile bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">连接池配置</param>
        /// <param name="logger">日志记录器</param>
        public ConnectionPool(ConnectionPoolOptions options, ILogger<ConnectionPool> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            
            // 验证配置
            var validationResult = _options.Validate();
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"连接池配置无效: {string.Join(", ", validationResult.Errors)}");
            }
            
            _statistics = new ConnectionPoolStatistics();
            _endpointPools = new ConcurrentDictionary<string, EndpointConnectionPool>();
            _createConnectionSemaphore = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);
            
            // 启动定时器
            if (_options.CleanupIntervalMs > 0)
            {
                _cleanupTimer = new Timer(CleanupCallback, null, 
                    TimeSpan.FromMilliseconds(_options.CleanupIntervalMs),
                    TimeSpan.FromMilliseconds(_options.CleanupIntervalMs));
            }
            
            if (_options.EnableHealthCheck && _options.ValidationIntervalMs > 0)
            {
                _validationTimer = new Timer(ValidationCallback, null,
                    TimeSpan.FromMilliseconds(_options.ValidationIntervalMs),
                    TimeSpan.FromMilliseconds(_options.ValidationIntervalMs));
            }
            
            _logger?.LogDebug("连接池已创建: MaxConnections={MaxConnections}, MinConnections={MinConnections}", 
                _options.MaxConnections, _options.MinConnections);
        }

        /// <summary>
        /// 连接池统计信息
        /// </summary>
        public ConnectionPoolStatistics Statistics => _statistics;

        /// <summary>
        /// 连接池配置
        /// </summary>
        public ConnectionPoolOptions Options => _options;

        /// <summary>
        /// 连接池事件
        /// </summary>
        public event EventHandler<ConnectionPoolEventArgs> ConnectionCreated;
        public event EventHandler<ConnectionPoolEventArgs> ConnectionDestroyed;
        public event EventHandler<ConnectionPoolEventArgs> ConnectionAcquired;
        public event EventHandler<ConnectionPoolEventArgs> ConnectionReleased;
        public event EventHandler<ConnectionPoolEventArgs> ConnectionValidationFailed;

        /// <summary>
        /// 获取连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>池化连接</returns>
        public async Task<IPooledConnection> GetConnectionAsync(ConnectionEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            
            var acquisitionStart = DateTime.UtcNow;
            var endpointPool = GetOrCreateEndpointPool(endpoint);
            
            try
            {
                _statistics.IncrementWaitingRequests(endpoint);
                
                var connection = await endpointPool.GetConnectionAsync(cancellationToken);
                
                _statistics.DecrementWaitingRequests(endpoint);
                _statistics.IncrementActiveConnections(endpoint);
                
                var acquisitionTime = (DateTime.UtcNow - acquisitionStart).TotalMilliseconds;
                _statistics.UpdateConnectionAcquisitionTime(endpoint, acquisitionTime);
                
                OnConnectionAcquired(endpoint, connection);
                
                _logger?.LogDebug("连接已获取: {ConnectionId} -> {Endpoint}, 耗时: {AcquisitionTime}ms", 
                    connection.ConnectionId, endpoint, acquisitionTime);
                
                return connection;
            }
            catch (Exception ex)
            {
                _statistics.DecrementWaitingRequests(endpoint);
                _statistics.RecordConnectionAcquisitionFailure(endpoint);
                
                _logger?.LogError(ex, "获取连接失败: {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// 获取连接（同步版本）
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="timeout">超时时间</param>
        /// <returns>池化连接</returns>
        public IPooledConnection GetConnection(ConnectionEndpoint endpoint, TimeSpan? timeout = null)
        {
            var cancellationToken = timeout.HasValue 
                ? new CancellationTokenSource(timeout.Value).Token 
                : CancellationToken.None;
                
            return GetConnectionAsync(endpoint, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 释放连接回池
        /// </summary>
        /// <param name="connection">池化连接</param>
        /// <param name="forceClose">是否强制关闭</param>
        /// <returns>释放任务</returns>
        public async Task ReleaseConnectionAsync(IPooledConnection connection, bool forceClose = false)
        {
            if (connection == null)
                return;
            
            try
            {
                var endpointPool = GetOrCreateEndpointPool(connection.Endpoint);
                
                await endpointPool.ReleaseConnectionAsync(connection, forceClose);
                
                _statistics.DecrementActiveConnections(connection.Endpoint);
                
                OnConnectionReleased(connection.Endpoint, connection);
                
                _logger?.LogDebug("连接已释放: {ConnectionId} -> {Endpoint}", 
                    connection.ConnectionId, connection.Endpoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放连接失败: {ConnectionId}", connection.ConnectionId);
                throw;
            }
        }

        /// <summary>
        /// 创建新连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新连接</returns>
        public async Task<IPooledConnection> CreateConnectionAsync(ConnectionEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            
            await _createConnectionSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                var connectionId = Guid.NewGuid().ToString("N");
                var channel = await CreateChannelAsync(endpoint, cancellationToken);
                
                var connection = new PooledConnection(connectionId, endpoint, channel, 
                    null);
                
                _statistics.IncrementConnections(endpoint);
                
                OnConnectionCreated(endpoint, connection);
                
                _logger?.LogDebug("新连接已创建: {ConnectionId} -> {Endpoint}", 
                    connectionId, endpoint);
                
                return connection;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建连接失败: {Endpoint}", endpoint);
                throw;
            }
            finally
            {
                _createConnectionSemaphore.Release();
            }
        }

        /// <summary>
        /// 验证连接健康状态
        /// </summary>
        /// <param name="connection">连接</param>
        /// <returns>是否健康</returns>
        public async Task<bool> ValidateConnectionAsync(IPooledConnection connection)
        {
            if (connection == null)
                return false;
            
            try
            {
                var isValid = await connection.ValidateAsync();
                
                if (!isValid)
                {
                    _statistics.RecordConnectionValidationFailure(connection.Endpoint);
                    OnConnectionValidationFailed(connection.Endpoint, connection);
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "验证连接失败: {ConnectionId}", connection.ConnectionId);
                _statistics.RecordConnectionValidationFailure(connection.Endpoint);
                OnConnectionValidationFailed(connection.Endpoint, connection);
                return false;
            }
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
            
            var cleanupTasks = new List<Task>();
            
            foreach (var endpointPool in _endpointPools.Values)
            {
                cleanupTasks.Add(endpointPool.CleanupExpiredConnectionsAsync(cancellationToken));
            }
            
            await Task.WhenAll(cleanupTasks);
            
            _logger?.LogDebug("过期连接清理完成");
        }

        /// <summary>
        /// 关闭指定端点的所有连接
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>关闭任务</returns>
        public async Task CloseEndpointConnectionsAsync(ConnectionEndpoint endpoint)
        {
            if (endpoint == null)
                return;
            
            var endpointKey = endpoint.GetEndpointKey();
            if (_endpointPools.TryRemove(endpointKey, out var endpointPool))
            {
                await endpointPool.CloseAllConnectionsAsync();
                endpointPool.Dispose();
                
                _logger?.LogDebug("端点连接已关闭: {Endpoint}", endpoint);
            }
        }

        /// <summary>
        /// 关闭连接池中的所有连接
        /// </summary>
        /// <returns>关闭任务</returns>
        public async Task CloseAllConnectionsAsync()
        {
            var closeTasks = new List<Task>();
            
            foreach (var kvp in _endpointPools)
            {
                closeTasks.Add(kvp.Value.CloseAllConnectionsAsync());
            }
            
            await Task.WhenAll(closeTasks);
            
            foreach (var kvp in _endpointPools)
            {
                kvp.Value.Dispose();
            }
            
            _endpointPools.Clear();
            
            _logger?.LogDebug("所有连接已关闭");
        }

        /// <summary>
        /// 获取所有活动连接
        /// </summary>
        /// <returns>活动连接列表</returns>
        public IEnumerable<IPooledConnection> GetActiveConnections()
        {
            var activeConnections = new List<IPooledConnection>();
            
            foreach (var endpointPool in _endpointPools.Values)
            {
                activeConnections.AddRange(endpointPool.GetActiveConnections());
            }
            
            return activeConnections;
        }

        /// <summary>
        /// 获取指定端点的连接数
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>连接数信息</returns>
        public ConnectionCount GetConnectionCount(ConnectionEndpoint endpoint)
        {
            if (endpoint == null)
                return new ConnectionCount();
            
            var endpointPool = GetOrCreateEndpointPool(endpoint);
            return endpointPool.GetConnectionCount();
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
                CloseAllConnectionsAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭连接池时发生异常");
            }
            
            _cleanupTimer?.Dispose();
            _validationTimer?.Dispose();
            _createConnectionSemaphore?.Dispose();
            
            _logger?.LogDebug("连接池已释放");
        }

        /// <summary>
        /// 获取或创建端点连接池
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>端点连接池</returns>
        private EndpointConnectionPool GetOrCreateEndpointPool(ConnectionEndpoint endpoint)
        {
            var endpointKey = endpoint.GetEndpointKey();
            return _endpointPools.GetOrAdd(endpointKey, key => 
                new EndpointConnectionPool(endpoint, _options, this, _logger));
        }

        /// <summary>
        /// 创建传输通道
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>传输通道</returns>
        private async Task<ITwoWayChannel> CreateChannelAsync(ConnectionEndpoint endpoint, CancellationToken cancellationToken)
        {
            // 如果有自定义连接工厂，使用自定义工厂创建连接
            if (_options.ConnectionFactory != null)
            {
                var customConnection = _options.ConnectionFactory(endpoint);
                return customConnection.Channel;
            }
            
            // 创建传输配置
            var config = CreateTransportConfiguration(endpoint);
            
            // 使用传输工厂创建连接
            var transportFactory = new TransportFactory();
            var transport = transportFactory.CreateTransport(config);
            
            // 建立连接
            await transport.ConnectAsync(cancellationToken);
            
            return transport;
        }

        /// <summary>
        /// 创建传输配置
        /// </summary>
        /// <param name="endpoint">连接端点</param>
        /// <returns>传输配置</returns>
        private TransportConfiguration CreateTransportConfiguration(ConnectionEndpoint endpoint)
        {
            return endpoint.TransportType switch
            {
                TransportType.Tcp => TransportConfiguration.CreateTcp(endpoint.Address, endpoint.Port),
                TransportType.NamedPipe => TransportConfiguration.CreateNamedPipe(".", endpoint.Address),
                _ => throw new NotSupportedException($"不支持的传输类型: {endpoint.TransportType}")
            };
        }

        /// <summary>
        /// 清理回调
        /// </summary>
        /// <param name="state">状态</param>
        private void CleanupCallback(object state)
        {
            try
            {
                CleanupExpiredConnectionsAsync().Wait();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "定时清理任务执行失败");
            }
        }

        /// <summary>
        /// 验证回调
        /// </summary>
        /// <param name="state">状态</param>
        private void ValidationCallback(object state)
        {
            try
            {
                var validationTasks = new List<Task>();
                
                foreach (var endpointPool in _endpointPools.Values)
                {
                    validationTasks.Add(endpointPool.ValidateConnectionsAsync());
                }
                
                Task.WhenAll(validationTasks).Wait();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "定时验证任务执行失败");
            }
        }

        /// <summary>
        /// 触发连接创建事件
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="connection">连接</param>
        private void OnConnectionCreated(ConnectionEndpoint endpoint, IPooledConnection connection)
        {
            try
            {
                ConnectionCreated?.Invoke(this, new ConnectionPoolEventArgs
                {
                    Endpoint = endpoint,
                    Connection = connection
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发连接创建事件失败");
            }
        }

        /// <summary>
        /// 触发连接销毁事件
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="connection">连接</param>
        private void OnConnectionDestroyed(ConnectionEndpoint endpoint, IPooledConnection connection)
        {
            try
            {
                ConnectionDestroyed?.Invoke(this, new ConnectionPoolEventArgs
                {
                    Endpoint = endpoint,
                    Connection = connection
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发连接销毁事件失败");
            }
        }

        /// <summary>
        /// 触发连接获取事件
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="connection">连接</param>
        private void OnConnectionAcquired(ConnectionEndpoint endpoint, IPooledConnection connection)
        {
            try
            {
                ConnectionAcquired?.Invoke(this, new ConnectionPoolEventArgs
                {
                    Endpoint = endpoint,
                    Connection = connection
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发连接获取事件失败");
            }
        }

        /// <summary>
        /// 触发连接释放事件
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="connection">连接</param>
        private void OnConnectionReleased(ConnectionEndpoint endpoint, IPooledConnection connection)
        {
            try
            {
                ConnectionReleased?.Invoke(this, new ConnectionPoolEventArgs
                {
                    Endpoint = endpoint,
                    Connection = connection
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发连接释放事件失败");
            }
        }

        /// <summary>
        /// 触发连接验证失败事件
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="connection">连接</param>
        private void OnConnectionValidationFailed(ConnectionEndpoint endpoint, IPooledConnection connection)
        {
            try
            {
                ConnectionValidationFailed?.Invoke(this, new ConnectionPoolEventArgs
                {
                    Endpoint = endpoint,
                    Connection = connection
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "触发连接验证失败事件失败");
            }
        }

        /// <summary>
        /// 检查是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConnectionPool));
            }
        }
    }
} 