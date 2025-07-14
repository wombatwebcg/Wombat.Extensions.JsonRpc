using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Transport
{
    /// <summary>
    /// Named Pipe传输层实现
    /// </summary>
    public class NamedPipeTransport : ITwoWayChannel
    {
        private readonly ILogger<NamedPipeTransport> _logger;
        private readonly NamedPipeTransportOptions _options;
        private readonly ChannelStatistics _statistics;
        private PipeStream _pipeStream;
        private bool _disposed;

        /// <summary>
        /// 构造函数（客户端模式）
        /// </summary>
        /// <param name="serverName">服务器名称</param>
        /// <param name="pipeName">管道名称</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public NamedPipeTransport(string serverName, string pipeName, NamedPipeTransportOptions options = null, ILogger<NamedPipeTransport> logger = null)
        {
            if (string.IsNullOrEmpty(serverName))
                throw new ArgumentException("服务器名称不能为空", nameof(serverName));

            if (string.IsNullOrEmpty(pipeName))
                throw new ArgumentException("管道名称不能为空", nameof(pipeName));

            _logger = logger;
            _options = options ?? new NamedPipeTransportOptions();
            _statistics = new ChannelStatistics();
            
            RemoteEndPoint = $"\\\\{serverName}\\pipe\\{pipeName}";
            LocalEndPoint = "客户端";
        }

        /// <summary>
        /// 构造函数（服务器模式）
        /// </summary>
        /// <param name="pipeStream">已连接的管道流</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public NamedPipeTransport(PipeStream pipeStream, NamedPipeTransportOptions options = null, ILogger<NamedPipeTransport> logger = null)
        {
            _pipeStream = pipeStream ?? throw new ArgumentNullException(nameof(pipeStream));
            _logger = logger;
            _options = options ?? new NamedPipeTransportOptions();
            _statistics = new ChannelStatistics();

            if (pipeStream.IsConnected)
            {
                _statistics.ConnectedAt = DateTime.UtcNow;
                RemoteEndPoint = "客户端";
                LocalEndPoint = "服务器";
            }
            else
            {
                RemoteEndPoint = "未连接";
                LocalEndPoint = "未连接";
            }
        }

        #region ITwoWayChannel 实现

        public Stream InputStream => _pipeStream;
        public Stream OutputStream => _pipeStream;
        public bool IsConnected => _pipeStream?.IsConnected == true;
        public TransportType TransportType => TransportType.NamedPipe;
        public string RemoteEndPoint { get; private set; }
        public string LocalEndPoint { get; private set; }

        public event EventHandler<ChannelEventArgs> Connected;
        public event EventHandler<ChannelEventArgs> Disconnected;
        public event EventHandler<ChannelErrorEventArgs> Error;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NamedPipeTransport));

            if (IsConnected)
                return;

            try
            {
                _logger?.LogInformation("正在连接到Named Pipe: {RemoteEndPoint}", RemoteEndPoint);

                if (_pipeStream == null)
                {
                    var parts = RemoteEndPoint.Split('\\');
                    var serverName = parts[2];
                    var pipeName = parts[4];

                    var clientPipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.None);
                    _pipeStream = clientPipe;

                    // 连接到服务器
                    using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    await clientPipe.ConnectAsync(combinedCts.Token).ConfigureAwait(false);
                }

                _statistics.ConnectedAt = DateTime.UtcNow;
                _statistics.LastActivity = DateTime.UtcNow;

                _logger?.LogInformation("成功连接到Named Pipe: {RemoteEndPoint}", RemoteEndPoint);

                // 触发连接事件
                Connected?.Invoke(this, new ChannelEventArgs(this, "Named Pipe连接已建立"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "连接到Named Pipe失败: {RemoteEndPoint}", RemoteEndPoint);
                _statistics.ErrorCount++;
                
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "Named Pipe连接失败", 0, true));
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return;

            try
            {
                _logger?.LogInformation("正在断开Named Pipe连接");

                if (_pipeStream != null)
                {
                    await _pipeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    _pipeStream.Close();
                    _pipeStream = null;
                }

                _logger?.LogInformation("Named Pipe连接已断开");

                // 触发断开连接事件
                Disconnected?.Invoke(this, new ChannelEventArgs(this, "Named Pipe连接已断开"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "断开Named Pipe连接时发生错误");
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "断开连接失败", 0, false));
                throw;
            }
        }

        public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return;

            try
            {
                // 发送心跳包（空字节）
                await _pipeStream.WriteAsync(new byte[0], 0, 0, cancellationToken).ConfigureAwait(false);
                await _pipeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                
                _statistics.LastActivity = DateTime.UtcNow;
                _statistics.MessagesSent++;
                
                _logger?.LogDebug("发送Named Pipe心跳包");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "发送Named Pipe心跳包失败");
                _statistics.ErrorCount++;
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "心跳包发送失败", 0, true));
            }
        }

        public ChannelStatistics GetStatistics()
        {
            return _statistics;
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
                _logger?.LogError(ex, "释放Named Pipe资源时发生错误");
            }

            _pipeStream?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Named Pipe服务器传输层
    /// </summary>
    public class NamedPipeServerTransport : IDisposable
    {
        private readonly ILogger<NamedPipeServerTransport> _logger;
        private readonly NamedPipeTransportOptions _options;
        private readonly string _pipeName;
        private bool _isListening;
        private bool _disposed;
        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pipeName">管道名称</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public NamedPipeServerTransport(string pipeName, NamedPipeTransportOptions options = null, ILogger<NamedPipeServerTransport> logger = null)
        {
            if (string.IsNullOrEmpty(pipeName))
                throw new ArgumentException("管道名称不能为空", nameof(pipeName));

            _pipeName = pipeName;
            _logger = logger;
            _options = options ?? new NamedPipeTransportOptions();
            _cancellationTokenSource = new CancellationTokenSource();
            
            LocalEndPoint = $"\\\\.\\pipe\\{pipeName}";
        }

        /// <summary>
        /// 本地端点
        /// </summary>
        public string LocalEndPoint { get; }

        /// <summary>
        /// 是否正在监听
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event EventHandler<NamedPipeClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// 开始监听
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NamedPipeServerTransport));

            if (_isListening)
                return;

            try
            {
                _isListening = true;

                _logger?.LogInformation("Named Pipe服务器开始监听: {LocalEndPoint}", LocalEndPoint);

                // 启动监听循环
                _ = Task.Run(async () =>
                {
                    while (_isListening && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var serverPipe = new NamedPipeServerStream(
                                _pipeName,
                                PipeDirection.InOut,
                                _options.MaxServerInstances,
                                PipeTransmissionMode.Byte,
                                PipeOptions.Asynchronous,
                                _options.InBufferSize,
                                _options.OutBufferSize);

                            _logger?.LogDebug("等待客户端连接到Named Pipe: {PipeName}", _pipeName);

                            await serverPipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                            _logger?.LogInformation("客户端已连接到Named Pipe: {PipeName}", _pipeName);

                            var transport = new NamedPipeTransport(serverPipe, _options, null);
                            ClientConnected?.Invoke(this, new NamedPipeClientConnectedEventArgs(transport));
                        }
                        catch (Exception ex) when (!_disposed && !cancellationToken.IsCancellationRequested)
                        {
                            _logger?.LogError(ex, "接受Named Pipe客户端连接时发生错误");
                            await Task.Delay(1000, cancellationToken); // 等待一秒后重试
                        }
                    }
                }, cancellationToken);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动Named Pipe服务器失败");
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
                _cancellationTokenSource.Cancel();
                
                _logger?.LogInformation("Named Pipe服务器已停止监听");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止Named Pipe服务器时发生错误");
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
                _logger?.LogError(ex, "释放Named Pipe服务器资源时发生错误");
            }

            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Named Pipe传输选项
    /// </summary>
    public class NamedPipeTransportOptions
    {
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 最大服务器实例数
        /// </summary>
        public int MaxServerInstances { get; set; } = 10;

        /// <summary>
        /// 输入缓冲区大小
        /// </summary>
        public int InBufferSize { get; set; } = 8192;

        /// <summary>
        /// 输出缓冲区大小
        /// </summary>
        public int OutBufferSize { get; set; } = 8192;

        /// <summary>
        /// 读取超时时间
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 写入超时时间
        /// </summary>
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 是否允许匿名访问
        /// </summary>
        public bool AllowAnonymous { get; set; } = true;

        /// <summary>
        /// 管道访问权限
        /// </summary>
        public PipeAccessRights AccessRights { get; set; } = PipeAccessRights.FullControl;
    }

    /// <summary>
    /// Named Pipe客户端连接事件参数
    /// </summary>
    public class NamedPipeClientConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 传输通道
        /// </summary>
        public NamedPipeTransport Transport { get; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;

        public NamedPipeClientConnectedEventArgs(NamedPipeTransport transport)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }
    }

    /// <summary>
    /// 管道访问权限
    /// </summary>
    [Flags]
    public enum PipeAccessRights
    {
        /// <summary>
        /// 读取权限
        /// </summary>
        Read = 1,

        /// <summary>
        /// 写入权限
        /// </summary>
        Write = 2,

        /// <summary>
        /// 读写权限
        /// </summary>
        ReadWrite = Read | Write,

        /// <summary>
        /// 完全控制
        /// </summary>
        FullControl = ReadWrite | 4
    }
} 