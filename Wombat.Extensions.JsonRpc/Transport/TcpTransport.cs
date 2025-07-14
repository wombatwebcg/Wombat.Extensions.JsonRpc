using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Transport
{
    /// <summary>
    /// TCP传输层实现
    /// </summary>
    public class TcpTransport : ITwoWayChannel
    {
        private readonly ILogger<TcpTransport> _logger;
        private readonly TcpTransportOptions _options;
        private readonly ChannelStatistics _statistics;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isConnected;
        private bool _disposed;

        /// <summary>
        /// 构造函数（客户端模式）
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public TcpTransport(string host, int port, TcpTransportOptions options = null, ILogger<TcpTransport> logger = null)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("主机地址不能为空", nameof(host));

            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535范围内");

            _logger = logger;
            _options = options ?? new TcpTransportOptions();
            _statistics = new ChannelStatistics();
            
            RemoteEndPoint = $"{host}:{port}";
            LocalEndPoint = "未连接";
        }

        /// <summary>
        /// 构造函数（服务器模式）
        /// </summary>
        /// <param name="tcpClient">已连接的TCP客户端</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public TcpTransport(TcpClient tcpClient, TcpTransportOptions options = null, ILogger<TcpTransport> logger = null)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _logger = logger;
            _options = options ?? new TcpTransportOptions();
            _statistics = new ChannelStatistics();

            if (tcpClient.Connected)
            {
                _stream = tcpClient.GetStream();
                _isConnected = true;
                _statistics.ConnectedAt = DateTime.UtcNow;
                
                RemoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "未知";
                LocalEndPoint = tcpClient.Client.LocalEndPoint?.ToString() ?? "未知";
            }
            else
            {
                RemoteEndPoint = "未连接";
                LocalEndPoint = "未连接";
            }
        }

        #region ITwoWayChannel 实现

        public Stream InputStream => _stream;
        public Stream OutputStream => _stream;
        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
        public TransportType TransportType => TransportType.Tcp;
        public string RemoteEndPoint { get; private set; }
        public string LocalEndPoint { get; private set; }

        public event EventHandler<ChannelEventArgs> Connected;
        public event EventHandler<ChannelEventArgs> Disconnected;
        public event EventHandler<ChannelErrorEventArgs> Error;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpTransport));

            if (_isConnected)
                return;

            try
            {
                _logger?.LogInformation("正在连接到 {RemoteEndPoint}", RemoteEndPoint);

                if (_tcpClient == null)
                {
                    _tcpClient = new TcpClient();
                    ConfigureTcpClient(_tcpClient);
                }

                var parts = RemoteEndPoint.Split(':');
                var host = parts[0];
                var port = int.Parse(parts[1]);

                using (var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout))
                using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    await _tcpClient.ConnectAsync(host, port).ConfigureAwait(false);
                }

                _stream = _tcpClient.GetStream();
                _isConnected = true;
                _statistics.ConnectedAt = DateTime.UtcNow;
                _statistics.LastActivity = DateTime.UtcNow;

                LocalEndPoint = _tcpClient.Client.LocalEndPoint?.ToString() ?? "未知";

                _logger?.LogInformation("成功连接到 {RemoteEndPoint}，本地端点: {LocalEndPoint}", RemoteEndPoint, LocalEndPoint);

                // 触发连接事件
                Connected?.Invoke(this, new ChannelEventArgs(this, "TCP连接已建立"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "连接到 {RemoteEndPoint} 失败", RemoteEndPoint);
                _statistics.ErrorCount++;
                
                // 触发错误事件
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "TCP连接失败", 0, true));
                
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                return;

            try
            {
                _logger?.LogInformation("正在断开与 {RemoteEndPoint} 的连接", RemoteEndPoint);

                _isConnected = false;
                
                if (_stream != null)
                {
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    _stream.Close();
                    _stream = null;
                }

                _tcpClient?.Close();
                _tcpClient = null;

                _logger?.LogInformation("已断开与 {RemoteEndPoint} 的连接", RemoteEndPoint);

                // 触发断开连接事件
                Disconnected?.Invoke(this, new ChannelEventArgs(this, "TCP连接已断开"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "断开连接时发生错误");
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "断开连接失败", 0, false));
                throw;
            }
        }

        public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                return;

            try
            {
                // 发送简单的心跳包（空字节）
                await _stream.WriteAsync(new byte[0], 0, 0, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                
                _statistics.LastActivity = DateTime.UtcNow;
                _statistics.MessagesSent++;
                
                _logger?.LogDebug("发送心跳包到 {RemoteEndPoint}", RemoteEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "发送心跳包失败");
                _statistics.ErrorCount++;
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "心跳包发送失败", 0, true));
            }
        }

        public ChannelStatistics GetStatistics()
        {
            return _statistics;
        }

        #endregion

        #region 私有方法

        private void ConfigureTcpClient(TcpClient tcpClient)
        {
            // 配置TCP客户端选项
            tcpClient.ReceiveTimeout = (int)_options.ReceiveTimeout.TotalMilliseconds;
            tcpClient.SendTimeout = (int)_options.SendTimeout.TotalMilliseconds;
            tcpClient.NoDelay = _options.NoDelay;
            tcpClient.ReceiveBufferSize = _options.ReceiveBufferSize;
            tcpClient.SendBufferSize = _options.SendBufferSize;

            if (_options.KeepAlive)
            {
                tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
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
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放资源时发生错误");
            }

            _stream?.Dispose();
            _tcpClient?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// TCP传输选项
    /// </summary>
    public class TcpTransportOptions
    {
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否启用Nagle算法
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// 发送缓冲区大小
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// 是否启用Keep-Alive
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// 重连尝试次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 3;

        /// <summary>
        /// 重连间隔时间
        /// </summary>
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// TCP服务器传输层
    /// </summary>
    public class TcpServerTransport : IDisposable
    {
        private readonly ILogger<TcpServerTransport> _logger;
        private readonly TcpTransportOptions _options;
        private TcpListener _tcpListener;
        private bool _isListening;
        private bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public TcpServerTransport(int port, TcpTransportOptions options = null, ILogger<TcpServerTransport> logger = null)
        {
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535范围内");

            _logger = logger;
            _options = options ?? new TcpTransportOptions();
            _tcpListener = new TcpListener(IPAddress.Any, port);
            
            LocalEndPoint = $"0.0.0.0:{port}";
        }

        /// <summary>
        /// 本地端点
        /// </summary>
        public string LocalEndPoint { get; private set; }

        /// <summary>
        /// 是否正在监听
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// 开始监听
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpServerTransport));

            if (_isListening)
                return;

            try
            {
                _tcpListener.Start();
                _isListening = true;

                _logger?.LogInformation("TCP服务器开始监听 {LocalEndPoint}", LocalEndPoint);

                // 异步接受客户端连接
                _ = Task.Run(async () =>
                {
                    while (_isListening && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var tcpClient = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                            var transport = new TcpTransport(tcpClient, _options, null);
                            
                            _logger?.LogInformation("接受客户端连接: {RemoteEndPoint}", transport.RemoteEndPoint);
                            
                            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(transport));
                        }
                        catch (Exception ex) when (!_disposed)
                        {
                            _logger?.LogError(ex, "接受客户端连接时发生错误");
                        }
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动TCP服务器失败");
                throw;
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isListening)
                return;

            try
            {
                _isListening = false;
                _tcpListener?.Stop();
                
                _logger?.LogInformation("TCP服务器已停止监听");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止TCP服务器时发生错误");
                throw;
            }
        }

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
                _logger?.LogError(ex, "释放TCP服务器资源时发生错误");
            }

            _tcpListener?.Stop();
        }
    }

    /// <summary>
    /// 客户端连接事件参数
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 传输通道
        /// </summary>
        public TcpTransport Transport { get; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;

        public ClientConnectedEventArgs(TcpTransport transport)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }
    }
} 