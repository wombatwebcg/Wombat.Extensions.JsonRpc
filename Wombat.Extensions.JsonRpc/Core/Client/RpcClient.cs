using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Core.Transport;

namespace Wombat.Extensions.JsonRpc.Core.Client
{
    /// <summary>
    /// 高级RPC客户端
    /// </summary>
    public class RpcClient : IDisposable
    {
        private readonly ILogger<RpcClient> _logger;
        private readonly RpcClientOptions _options;
        private readonly ITransportFactory _transportFactory;
        private ITwoWayChannel _transport;
        private StreamJsonRpc.JsonRpc _jsonRpc;
        private readonly RpcClientStatistics _statistics;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private bool _disposed;
        private Timer _reconnectTimer;
        private Timer _heartbeatTimer;

        public RpcClient(RpcClientOptions options = null, ITransportFactory transportFactory = null, ILogger<RpcClient> logger = null)
        {
            _options = options ?? new RpcClientOptions();
            _transportFactory = transportFactory ?? new TransportFactory();
            _logger = logger;
            _statistics = new RpcClientStatistics();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 客户端是否已连接
        /// </summary>
        public bool IsConnected => _isConnected && _jsonRpc?.IsDisposed == false;

        /// <summary>
        /// 客户端统计信息
        /// </summary>
        public RpcClientStatistics Statistics => _statistics;

        /// <summary>
        /// 远程端点
        /// </summary>
        public string RemoteEndPoint => _transport?.RemoteEndPoint;

        /// <summary>
        /// 本地端点
        /// </summary>
        public string LocalEndPoint => _transport?.LocalEndPoint;

        /// <summary>
        /// 连接事件
        /// </summary>
        public event EventHandler<ConnectedEventArgs> Connected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>
        /// 客户端错误事件
        /// </summary>
        public event EventHandler<ClientErrorEventArgs> Error;

        /// <summary>
        /// 连接到TCP服务器
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        /// <param name="options">TCP选项</param>
        /// <returns>连接任务</returns>
        public async Task ConnectTcpAsync(string host, int port, TcpTransportOptions options = null)
        {
            var transport = _transportFactory.CreateTcpClient(host, port, options);
            await ConnectAsync(transport);
        }

        /// <summary>
        /// 连接到Named Pipe服务器
        /// </summary>
        /// <param name="serverName">服务器名称</param>
        /// <param name="pipeName">管道名称</param>
        /// <param name="options">Named Pipe选项</param>
        /// <returns>连接任务</returns>
        public async Task ConnectNamedPipeAsync(string serverName, string pipeName, NamedPipeTransportOptions options = null)
        {
            var transport = _transportFactory.CreateNamedPipeClient(serverName, pipeName, options);
            await ConnectAsync(transport);
        }

        /// <summary>
        /// 使用传输配置连接
        /// </summary>
        /// <param name="config">传输配置</param>
        /// <returns>连接任务</returns>
        public async Task ConnectAsync(TransportConfiguration config)
        {
            var transport = _transportFactory.CreateTransport(config);
            await ConnectAsync(transport);
        }

        /// <summary>
        /// 使用传输通道连接
        /// </summary>
        /// <param name="transport">传输通道</param>
        /// <returns>连接任务</returns>
        public async Task ConnectAsync(ITwoWayChannel transport)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RpcClient));

            if (_isConnected)
                await DisconnectAsync();

            try
            {
                _transport = transport ?? throw new ArgumentNullException(nameof(transport));
                
                _logger?.LogInformation("正在连接到RPC服务器: {RemoteEndPoint}", transport.RemoteEndPoint);

                // 连接传输层
                await _transport.ConnectAsync(_cancellationTokenSource.Token);

                // 创建JsonRpc实例
                _jsonRpc = new StreamJsonRpc.JsonRpc(_transport);
                
                // 配置JsonRpc选项
                ConfigureJsonRpc(_jsonRpc);

                // 启动JsonRpc监听
                _jsonRpc.StartListening();

                _isConnected = true;
                _statistics.ConnectedAt = DateTime.UtcNow;
                _statistics.ConnectionAttempts++;

                _logger?.LogInformation("成功连接到RPC服务器: {RemoteEndPoint}", transport.RemoteEndPoint);

                // 启动心跳定时器
                if (_options.EnableHeartbeat)
                {
                    StartHeartbeat();
                }

                // 触发连接事件
                Connected?.Invoke(this, new ConnectedEventArgs(_transport.RemoteEndPoint));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "连接到RPC服务器失败: {RemoteEndPoint}", transport?.RemoteEndPoint);
                _statistics.FailedConnections++;
                
                Error?.Invoke(this, new ClientErrorEventArgs(ex, "连接失败"));
                
                if (_options.EnableAutoReconnect)
                {
                    StartReconnectTimer();
                }
                
                throw;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开连接任务</returns>
        public async Task DisconnectAsync()
        {
            if (!_isConnected)
                return;

            try
            {
                _logger?.LogInformation("正在断开RPC客户端连接");

                // 停止定时器
                StopHeartbeat();
                StopReconnectTimer();

                _isConnected = false;

                // 关闭JsonRpc
                if (_jsonRpc != null && !_jsonRpc.IsDisposed)
                {
                    _jsonRpc.Dispose();
                    _jsonRpc = null;
                }

                // 断开传输连接
                if (_transport != null && _transport.IsConnected)
                {
                    await _transport.DisconnectAsync();
                }

                _statistics.DisconnectedAt = DateTime.UtcNow;

                _logger?.LogInformation("RPC客户端连接已断开");

                // 触发断开连接事件
                Disconnected?.Invoke(this, new DisconnectedEventArgs("手动断开"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "断开RPC客户端连接时发生异常");
                Error?.Invoke(this, new ClientErrorEventArgs(ex, "断开连接失败"));
                throw;
            }
        }

        /// <summary>
        /// 调用RPC方法
        /// </summary>
        /// <param name="targetName">目标方法名</param>
        /// <param name="arguments">参数</param>
        /// <returns>调用结果</returns>
        public async Task<object> InvokeAsync(string targetName, params object[] arguments)
        {
            return await InvokeAsync<object>(targetName, arguments);
        }

        /// <summary>
        /// 调用RPC方法（泛型版本）
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="targetName">目标方法名</param>
        /// <param name="arguments">参数</param>
        /// <returns>调用结果</returns>
        public async Task<T> InvokeAsync<T>(string targetName, params object[] arguments)
        {
            if (!_isConnected)
                throw new InvalidOperationException("客户端未连接");

            try
            {
                _statistics.TotalRequests++;
                var startTime = DateTime.UtcNow;

                _logger?.LogDebug("调用RPC方法: {Method}, 参数数量: {ArgCount}", targetName, arguments?.Length ?? 0);

                T result;
                if (_options.RequestTimeout.HasValue)
                {
                    using var timeoutCts = new CancellationTokenSource(_options.RequestTimeout.Value);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutCts.Token);
                    
                    result = await _jsonRpc.InvokeAsync<T>(targetName, arguments, combinedCts.Token);
                }
                else
                {
                    result = await _jsonRpc.InvokeAsync<T>(targetName, arguments, _cancellationTokenSource.Token);
                }

                var duration = DateTime.UtcNow - startTime;
                _statistics.SuccessfulRequests++;
                _statistics.TotalLatency += duration;
                _statistics.LastRequestAt = DateTime.UtcNow;

                _logger?.LogDebug("RPC方法调用成功: {Method}, 耗时: {Duration}ms", targetName, duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _statistics.FailedRequests++;
                _logger?.LogError(ex, "RPC方法调用失败: {Method}", targetName);
                
                Error?.Invoke(this, new ClientErrorEventArgs(ex, $"方法调用失败: {targetName}"));
                
                throw;
            }
        }

        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="targetName">目标方法名</param>
        /// <param name="arguments">参数</param>
        /// <returns>发送任务</returns>
        public async Task NotifyAsync(string targetName, params object[] arguments)
        {
            if (!_isConnected)
                throw new InvalidOperationException("客户端未连接");

            try
            {
                _statistics.TotalNotifications++;

                _logger?.LogDebug("发送RPC通知: {Method}, 参数数量: {ArgCount}", targetName, arguments?.Length ?? 0);

                await _jsonRpc.NotifyAsync(targetName, arguments, _cancellationTokenSource.Token);

                _logger?.LogDebug("RPC通知发送成功: {Method}", targetName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送RPC通知失败: {Method}", targetName);
                Error?.Invoke(this, new ClientErrorEventArgs(ex, $"通知发送失败: {targetName}"));
                throw;
            }
        }

        /// <summary>
        /// 创建代理对象
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <returns>代理对象</returns>
        public T CreateProxy<T>() where T : class
        {
            if (!_isConnected)
                throw new InvalidOperationException("客户端未连接");

            try
            {
                var proxy = _jsonRpc.Attach<T>();
                _logger?.LogDebug("创建RPC代理对象: {Type}", typeof(T).Name);
                return proxy;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建RPC代理对象失败: {Type}", typeof(T).Name);
                Error?.Invoke(this, new ClientErrorEventArgs(ex, $"创建代理失败: {typeof(T).Name}"));
                throw;
            }
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        /// <returns>心跳任务</returns>
        public async Task SendHeartbeatAsync()
        {
            if (!_isConnected)
                return;

            try
            {
                await _transport.SendHeartbeatAsync(_cancellationTokenSource.Token);
                _statistics.HeartbeatsSent++;
                _logger?.LogDebug("发送心跳包");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "发送心跳包失败");
                Error?.Invoke(this, new ClientErrorEventArgs(ex, "心跳包发送失败"));
            }
        }

        #region 私有方法

        private void ConfigureJsonRpc(StreamJsonRpc.JsonRpc jsonRpc)
        {
            // 配置断开连接事件
            jsonRpc.Disconnected += OnJsonRpcDisconnected;

            // 配置其他选项
            if (_options.EnableTracing)
            {
                // 配置追踪
            }
        }

        private void OnJsonRpcDisconnected(object sender, JsonRpcDisconnectedEventArgs e)
        {
            _isConnected = false;
            _statistics.DisconnectedAt = DateTime.UtcNow;

            _logger?.LogWarning("JsonRpc连接已断开: {Reason}", e.Reason);

            // 触发断开连接事件
            Disconnected?.Invoke(this, new DisconnectedEventArgs(e.Reason.ToString()));

            // 自动重连
            if (_options.EnableAutoReconnect && !_disposed)
            {
                StartReconnectTimer();
            }
        }

        private void StartHeartbeat()
        {
            if (_heartbeatTimer != null)
                return;

            _heartbeatTimer = new Timer(async _ =>
            {
                try
                {
                    await SendHeartbeatAsync();
                }
                catch
                {
                    // 忽略心跳异常
                }
            }, null, _options.HeartbeatInterval, _options.HeartbeatInterval);
        }

        private void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        private void StartReconnectTimer()
        {
            if (_reconnectTimer != null || _disposed)
                return;

            _logger?.LogInformation("启动自动重连定时器，间隔: {Interval}", _options.ReconnectInterval);

            _reconnectTimer = new Timer(async _ =>
            {
                try
                {
                    if (_statistics.ReconnectAttempts >= _options.MaxReconnectAttempts)
                    {
                        _logger?.LogWarning("达到最大重连次数限制: {MaxAttempts}", _options.MaxReconnectAttempts);
                        StopReconnectTimer();
                        return;
                    }

                    _statistics.ReconnectAttempts++;
                    _logger?.LogInformation("尝试重连，第 {Attempt} 次", _statistics.ReconnectAttempts);

                    // 重新连接
                    if (_transport != null)
                    {
                        await ConnectAsync(_transport);
                        StopReconnectTimer(); // 连接成功后停止重连定时器
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "自动重连失败");
                }
            }, null, _options.ReconnectInterval, _options.ReconnectInterval);
        }

        private void StopReconnectTimer()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        #endregion

        #region IDisposable 实现

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放RPC客户端资源时发生异常");
            }

            StopHeartbeat();
            StopReconnectTimer();
            _cancellationTokenSource?.Dispose();
            _transport?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// RPC客户端统计信息
    /// </summary>
    public class RpcClientStatistics
    {
        public DateTime? ConnectedAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public DateTime? LastRequestAt { get; set; }
        public int ConnectionAttempts { get; set; }
        public int FailedConnections { get; set; }
        public int ReconnectAttempts { get; set; }
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long TotalNotifications { get; set; }
        public long HeartbeatsSent { get; set; }
        public TimeSpan TotalLatency { get; set; }

        public TimeSpan? ConnectionDuration => ConnectedAt.HasValue ? (DisconnectedAt ?? DateTime.UtcNow) - ConnectedAt.Value : null;
        public double AverageLatency => TotalRequests > 0 ? TotalLatency.TotalMilliseconds / TotalRequests : 0;
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
        public double RequestsPerSecond => ConnectionDuration?.TotalSeconds > 0 ? TotalRequests / ConnectionDuration.Value.TotalSeconds : 0;
    }

    /// <summary>
    /// 连接事件参数
    /// </summary>
    public class ConnectedEventArgs : EventArgs
    {
        public string RemoteEndPoint { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ConnectedEventArgs(string remoteEndPoint)
        {
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        }
    }

    /// <summary>
    /// 断开连接事件参数
    /// </summary>
    public class DisconnectedEventArgs : EventArgs
    {
        public string Reason { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public DisconnectedEventArgs(string reason)
        {
            Reason = reason ?? "未知原因";
        }
    }

    /// <summary>
    /// 客户端错误事件参数
    /// </summary>
    public class ClientErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Message { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ClientErrorEventArgs(Exception exception, string message = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message ?? exception.Message;
        }
    }
} 