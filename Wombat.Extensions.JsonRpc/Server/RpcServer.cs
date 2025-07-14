using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Builder;
using Wombat.Extensions.JsonRpc.Contracts;
using Wombat.Extensions.JsonRpc.Validation;
using Wombat.Extensions.JsonRpc.Transport;


namespace Wombat.Extensions.JsonRpc.Server
{
    /// <summary>
    /// 高级RPC服务器
    /// </summary>
    public class RpcServer : IDisposable
    {
        private readonly ILogger<RpcServer> _logger;
        private readonly RpcServerOptions _options;
        private readonly ITransportFactory _transportFactory;
        private readonly RpcTargetBuilder _targetBuilder;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, RpcConnection> _connections;
        private readonly ConcurrentDictionary<string, object> _serverTransports;
        private readonly RpcServerStatistics _statistics;
        private bool _isStarted;
        private bool _disposed;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public RpcServer(RpcServerOptions options = null, IServiceProvider serviceProvider = null)
        {
            _options = options ?? new RpcServerOptions();
            _serviceProvider = serviceProvider;
            _logger = serviceProvider?.GetService<ILogger<RpcServer>>();
            _transportFactory = serviceProvider?.GetService<ITransportFactory>() ?? new TransportFactory(serviceProvider);
            
            // 创建RPC目标构建器
            var targetBuilderFactory = serviceProvider?.GetService<RpcTargetBuilderFactory>() ?? new RpcTargetBuilderFactory(serviceProvider);
            _targetBuilder = targetBuilderFactory.CreateStandard();
            
            _connections = new ConcurrentDictionary<string, RpcConnection>();
            _serverTransports = new ConcurrentDictionary<string, object>();
            _statistics = new RpcServerStatistics();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 服务器是否已启动
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>
        /// 活动连接数
        /// </summary>
        public int ActiveConnections => _connections.Count;

        /// <summary>
        /// 服务器统计信息
        /// </summary>
        public RpcServerStatistics Statistics => _statistics;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// 客户端断开事件
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// 服务器错误事件
        /// </summary>
        public event EventHandler<ServerErrorEventArgs> ServerError;

        /// <summary>
        /// 注册RPC服务
        /// </summary>
        /// <param name="serviceInstance">服务实例</param>
        /// <returns>是否注册成功</returns>
        public async Task<bool> RegisterServiceAsync(object serviceInstance)
        {
            if (serviceInstance == null)
                throw new ArgumentNullException(nameof(serviceInstance));

            try
            {
                var result = await _targetBuilder.RegisterServiceInstanceAsync(serviceInstance);
                
                if (result)
                {
                    _logger?.LogInformation("成功注册RPC服务: {ServiceType}", serviceInstance.GetType().Name);
                    _statistics.RegisteredServices++;
                }
                else
                {
                    _logger?.LogWarning("注册RPC服务失败: {ServiceType}", serviceInstance.GetType().Name);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注册RPC服务时发生异常: {ServiceType}", serviceInstance.GetType().Name);
                return false;
            }
        }

        /// <summary>
        /// 注册RPC服务类型
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>是否注册成功</returns>
        public async Task<bool> RegisterServiceAsync(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            try
            {
                var result = await _targetBuilder.RegisterServiceAsync(serviceType);
                
                if (result)
                {
                    _logger?.LogInformation("成功注册RPC服务类型: {ServiceType}", serviceType.Name);
                    _statistics.RegisteredServices++;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注册RPC服务类型时发生异常: {ServiceType}", serviceType.Name);
                return false;
            }
        }

        /// <summary>
        /// 自动扫描并注册程序集中的RPC服务
        /// </summary>
        /// <param name="assemblies">要扫描的程序集</param>
        /// <returns>注册的服务数量</returns>
        public async Task<int> ScanAndRegisterServicesAsync(params System.Reflection.Assembly[] assemblies)
        {
            try
            {
                var registeredCount = await _targetBuilder.ScanAndRegisterAsync(assemblies);
                _statistics.RegisteredServices += registeredCount;
                
                _logger?.LogInformation("自动扫描注册了 {Count} 个RPC服务", registeredCount);
                
                return registeredCount;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "自动扫描注册RPC服务时发生异常");
                return 0;
            }
        }

        /// <summary>
        /// 启动TCP服务器
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="options">TCP选项</param>
        /// <returns>启动任务</returns>
        public async Task StartTcpAsync(int port, TcpTransportOptions options = null)
        {
            await StartTransportServerAsync($"tcp_{port}", () => _transportFactory.CreateTcpServer(port, options));
        }

        /// <summary>
        /// 启动Named Pipe服务器
        /// </summary>
        /// <param name="pipeName">管道名称</param>
        /// <param name="options">Named Pipe选项</param>
        /// <returns>启动任务</returns>
        public async Task StartNamedPipeAsync(string pipeName, NamedPipeTransportOptions options = null)
        {
            await StartTransportServerAsync($"pipe_{pipeName}", () => _transportFactory.CreateNamedPipeServer(pipeName, options));
        }

        /// <summary>
        /// 启动多种传输服务器
        /// </summary>
        /// <param name="configs">传输配置列表</param>
        /// <returns>启动任务</returns>
        public async Task StartMultipleAsync(params TransportServerConfig[] configs)
        {
            var tasks = new List<Task>();

            foreach (var config in configs)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        switch (config.TransportType)
                        {
                            case TransportType.Tcp:
                                await StartTcpAsync(config.Port, config.TcpOptions);
                                break;
                            case TransportType.NamedPipe:
                                await StartNamedPipeAsync(config.PipeName, config.NamedPipeOptions);
                                break;
                            default:
                                _logger?.LogWarning("不支持的传输类型: {TransportType}", config.TransportType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "启动传输服务器失败: {TransportType}", config.TransportType);
                        ServerError?.Invoke(this, new ServerErrorEventArgs(ex, $"启动{config.TransportType}服务器失败"));
                    }
                }));
            }

            await Task.WhenAll(tasks);
            _isStarted = true;
            
            _logger?.LogInformation("RPC服务器已启动，支持 {Count} 种传输方式", configs.Length);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns>停止任务</returns>
        public async Task StopAsync()
        {
            if (!_isStarted)
                return;

            try
            {
                _logger?.LogInformation("正在停止RPC服务器...");

                // 取消所有操作
                _cancellationTokenSource.Cancel();

                // 断开所有客户端连接
                var disconnectTasks = new List<Task>();
                foreach (var connection in _connections.Values)
                {
                    disconnectTasks.Add(connection.DisconnectAsync());
                }
                await Task.WhenAll(disconnectTasks);

                // 停止所有传输服务器
                var stopTasks = new List<Task>();
                foreach (var serverTransport in _serverTransports.Values)
                {
                    if (serverTransport is TcpServerTransport tcpServer)
                    {
                        stopTasks.Add(tcpServer.StopAsync());
                    }
                    else if (serverTransport is NamedPipeServerTransport pipeServer)
                    {
                        stopTasks.Add(pipeServer.StopAsync());
                    }
                }
                await Task.WhenAll(stopTasks);

                _isStarted = false;
                _logger?.LogInformation("RPC服务器已停止");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止RPC服务器时发生异常");
                throw;
            }
        }

        /// <summary>
        /// 获取服务注册信息
        /// </summary>
        /// <returns>服务注册信息列表</returns>
        public List<Builder.RpcServiceRegistration> GetServiceRegistrations()
        {
            return _targetBuilder.GetServiceRegistrations();
        }

        /// <summary>
        /// 获取连接信息
        /// </summary>
        /// <returns>连接信息列表</returns>
        public List<RpcConnection> GetConnections()
        {
            return new List<RpcConnection>(_connections.Values);
        }

        #region 私有方法

        private async Task StartTransportServerAsync<T>(string serverId, Func<T> createServer) where T : class
        {
            try
            {
                var server = createServer();
                _serverTransports[serverId] = server;

                if (server is TcpServerTransport tcpServer)
                {
                    tcpServer.ClientConnected += OnTcpClientConnected;
                    await tcpServer.StartAsync(_cancellationTokenSource.Token);
                }
                else if (server is NamedPipeServerTransport pipeServer)
                {
                    pipeServer.ClientConnected += OnNamedPipeClientConnected;
                    await pipeServer.StartAsync(_cancellationTokenSource.Token);
                }

                _logger?.LogInformation("传输服务器已启动: {ServerId}", serverId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动传输服务器失败: {ServerId}", serverId);
                throw;
            }
        }

        private void OnTcpClientConnected(object sender, Transport.ClientConnectedEventArgs e)
        {
            HandleClientConnection(e.Transport, TransportType.Tcp);
        }

        private void OnNamedPipeClientConnected(object sender, NamedPipeClientConnectedEventArgs e)
        {
            HandleClientConnection(e.Transport, TransportType.NamedPipe);
        }

        private void HandleClientConnection(ITwoWayChannel transport, TransportType transportType)
        {
            try
            {
                var connectionId = Guid.NewGuid().ToString();
                
                // 创建JsonRpc实例
                var jsonRpc = new StreamJsonRpc.JsonRpc(transport.InputStream, transport.OutputStream);
                
                // 应用RPC目标
                _targetBuilder.ApplyToJsonRpc(jsonRpc);
                
                // 创建连接对象
                var connection = new RpcConnection
                {
                    Id = connectionId,
                    Transport = transport,
                    JsonRpc = jsonRpc,
                    TransportType = transportType,
                    ConnectedAt = DateTime.UtcNow,
                    RemoteEndPoint = transport.RemoteEndPoint,
                    LocalEndPoint = transport.LocalEndPoint
                };

                // 配置JsonRpc事件
                jsonRpc.Disconnected += (s, args) => OnClientDisconnected(connectionId, args.Reason.ToString());
                
                // 启动JsonRpc监听
                jsonRpc.StartListening();
                
                // 添加到连接字典
                _connections[connectionId] = connection;
                _statistics.TotalConnections++;
                _statistics.ActiveConnections = _connections.Count;

                _logger?.LogInformation("客户端已连接: {ConnectionId}, 传输: {TransportType}, 端点: {RemoteEndPoint}", 
                    connectionId, transportType, transport.RemoteEndPoint);

                // 触发连接事件
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(connection));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理客户端连接时发生异常");
                ServerError?.Invoke(this, new ServerErrorEventArgs(ex, "处理客户端连接失败"));
            }
        }

        private void OnClientDisconnected(string connectionId, string reason)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                connection.DisconnectedAt = DateTime.UtcNow;
                connection.DisconnectReason = reason.ToString();
                _statistics.ActiveConnections = _connections.Count;

                _logger?.LogInformation("客户端已断开: {ConnectionId}, 原因: {Reason}", connectionId, reason);

                // 触发断开连接事件
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(connection, reason.ToString()));
            }
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
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放RPC服务器资源时发生异常");
            }

            _cancellationTokenSource?.Dispose();
            
            // 释放所有连接
            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }
            _connections.Clear();

            // 释放传输服务器
            foreach (var serverTransport in _serverTransports.Values)
            {
                if (serverTransport is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _serverTransports.Clear();
        }

        #endregion
    }

    /// <summary>
    /// RPC连接信息
    /// </summary>
    public class RpcConnection : IDisposable
    {
        public string Id { get; set; }
        public ITwoWayChannel Transport { get; set; }
        public StreamJsonRpc.JsonRpc JsonRpc { get; set; }
        public TransportType TransportType { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public string RemoteEndPoint { get; set; }
        public string LocalEndPoint { get; set; }
        public string DisconnectReason { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public bool IsConnected => JsonRpc?.IsDisposed == false && Transport?.IsConnected == true;
        public TimeSpan ConnectionDuration => (DisconnectedAt ?? DateTime.UtcNow) - ConnectedAt;

        public async Task DisconnectAsync()
        {
            try
            {
                if (JsonRpc != null && !JsonRpc.IsDisposed)
                {
                    JsonRpc.Dispose();
                }

                if (Transport != null && Transport.IsConnected)
                {
                    await Transport.DisconnectAsync();
                }
            }
            catch
            {
                // 忽略断开连接时的异常
            }
        }

        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
            Transport?.Dispose();
        }
    }

    /// <summary>
    /// RPC服务器统计信息
    /// </summary>
    public class RpcServerStatistics
    {
        public int RegisteredServices { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;
        public double RequestsPerSecond => Uptime.TotalSeconds > 0 ? TotalRequests / Uptime.TotalSeconds : 0;
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
    }

    /// <summary>
    /// 传输服务器配置
    /// </summary>
    public class TransportServerConfig
    {
        public TransportType TransportType { get; set; }
        public int Port { get; set; }
        public string PipeName { get; set; }
        public TcpTransportOptions TcpOptions { get; set; }
        public NamedPipeTransportOptions NamedPipeOptions { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;

        public static TransportServerConfig CreateTcp(int port, TcpTransportOptions options = null, string name = null)
        {
            return new TransportServerConfig
            {
                TransportType = TransportType.Tcp,
                Port = port,
                TcpOptions = options,
                Name = name ?? $"TCP_{port}"
            };
        }

        public static TransportServerConfig CreateNamedPipe(string pipeName, NamedPipeTransportOptions options = null, string name = null)
        {
            return new TransportServerConfig
            {
                TransportType = TransportType.NamedPipe,
                PipeName = pipeName,
                NamedPipeOptions = options,
                Name = name ?? $"Pipe_{pipeName}"
            };
        }
    }

    /// <summary>
    /// 客户端连接事件参数
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        public RpcConnection Connection { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ClientConnectedEventArgs(RpcConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }
    }

    /// <summary>
    /// 客户端断开连接事件参数
    /// </summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        public RpcConnection Connection { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ClientDisconnectedEventArgs(RpcConnection connection, string reason)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Reason = reason ?? "未知原因";
        }
    }

    /// <summary>
    /// 服务器错误事件参数
    /// </summary>
    public class ServerErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Message { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ServerErrorEventArgs(Exception exception, string message = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message ?? exception.Message;
        }
    }
} 