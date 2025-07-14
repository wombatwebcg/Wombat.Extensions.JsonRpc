using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Wombat.Extensions.JsonRpc.Transport
{
    /// <summary>
    /// WebSocket传输层实现
    /// </summary>
    public class WebSocketTransport : ITwoWayChannel
    {
        private readonly ILogger<WebSocketTransport> _logger;
        private readonly WebSocketTransportOptions _options;
        private readonly ChannelStatistics _statistics;
        private readonly WebSocketMessageStream _inputStream;
        private readonly WebSocketMessageStream _outputStream;
        private WebSocket _webSocket;
        private bool _disposed;
        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 构造函数（客户端模式）
        /// </summary>
        /// <param name="uri">WebSocket URI</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public WebSocketTransport(Uri uri, WebSocketTransportOptions options = null, ILogger<WebSocketTransport> logger = null)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            _logger = logger;
            _options = options ?? new WebSocketTransportOptions();
            _statistics = new ChannelStatistics();
            _cancellationTokenSource = new CancellationTokenSource();
            
            RemoteEndPoint = uri.ToString();
            LocalEndPoint = "客户端";
            
            _inputStream = new WebSocketMessageStream(this, isInput: true);
            _outputStream = new WebSocketMessageStream(this, isInput: false);
        }

        /// <summary>
        /// 构造函数（服务器模式）
        /// </summary>
        /// <param name="webSocket">已连接的WebSocket</param>
        /// <param name="options">传输选项</param>
        /// <param name="logger">日志记录器</param>
        public WebSocketTransport(WebSocket webSocket, WebSocketTransportOptions options = null, ILogger<WebSocketTransport> logger = null)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _logger = logger;
            _options = options ?? new WebSocketTransportOptions();
            _statistics = new ChannelStatistics();
            _cancellationTokenSource = new CancellationTokenSource();

            if (webSocket.State == WebSocketState.Open)
            {
                _statistics.ConnectedAt = DateTime.UtcNow;
                RemoteEndPoint = "WebSocket客户端";
                LocalEndPoint = "WebSocket服务器";
            }
            else
            {
                RemoteEndPoint = "未连接";
                LocalEndPoint = "未连接";
            }

            _inputStream = new WebSocketMessageStream(this, isInput: true);
            _outputStream = new WebSocketMessageStream(this, isInput: false);
        }

        #region ITwoWayChannel 实现

        public Stream InputStream => _inputStream;
        public Stream OutputStream => _outputStream;
        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public TransportType TransportType => TransportType.WebSocket;
        public string RemoteEndPoint { get; private set; }
        public string LocalEndPoint { get; private set; }

        public event EventHandler<ChannelEventArgs> Connected;
        public event EventHandler<ChannelEventArgs> Disconnected;
        public event EventHandler<ChannelErrorEventArgs> Error;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WebSocketTransport));

            if (IsConnected)
                return;

            try
            {
                _logger?.LogInformation("正在连接到 {RemoteEndPoint}", RemoteEndPoint);

                if (_webSocket == null)
                {
                    var clientWebSocket = new ClientWebSocket();
                    ConfigureClientWebSocket(clientWebSocket);
                    _webSocket = clientWebSocket;
                }

                if (_webSocket is ClientWebSocket client)
                {
                    using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    
                    await client.ConnectAsync(new Uri(RemoteEndPoint), combinedCts.Token).ConfigureAwait(false);
                }

                _statistics.ConnectedAt = DateTime.UtcNow;
                _statistics.LastActivity = DateTime.UtcNow;

                _logger?.LogInformation("成功连接到 {RemoteEndPoint}", RemoteEndPoint);

                // 启动消息处理循环
                _ = Task.Run(async () => await MessageProcessingLoop(_cancellationTokenSource.Token));

                // 触发连接事件
                Connected?.Invoke(this, new ChannelEventArgs(this, "WebSocket连接已建立"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "连接到 {RemoteEndPoint} 失败", RemoteEndPoint);
                _statistics.ErrorCount++;
                
                Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "WebSocket连接失败", 0, true));
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return;

            try
            {
                _logger?.LogInformation("正在断开WebSocket连接");

                _cancellationTokenSource.Cancel();

                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", cancellationToken).ConfigureAwait(false);
                }

                _logger?.LogInformation("WebSocket连接已断开");

                // 触发断开连接事件
                Disconnected?.Invoke(this, new ChannelEventArgs(this, "WebSocket连接已断开"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "断开WebSocket连接时发生错误");
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
                var pingMessage = Encoding.UTF8.GetBytes("ping");
                await _webSocket.SendAsync(new ArraySegment<byte>(pingMessage), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                
                _statistics.LastActivity = DateTime.UtcNow;
                _statistics.MessagesSent++;
                
                _logger?.LogDebug("发送心跳包");
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

        #region WebSocket特有方法

        /// <summary>
        /// 发送WebSocket消息
        /// </summary>
        /// <param name="data">消息数据</param>
        /// <param name="messageType">消息类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送任务</returns>
        public async Task SendMessageAsync(byte[] data, WebSocketMessageType messageType = WebSocketMessageType.Text, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket未连接");

            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(data), messageType, true, cancellationToken).ConfigureAwait(false);
                
                _statistics.BytesSent += data.Length;
                _statistics.MessagesSent++;
                _statistics.LastActivity = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _statistics.ErrorCount++;
                _logger?.LogError(ex, "发送WebSocket消息失败");
                throw;
            }
        }

        /// <summary>
        /// 接收WebSocket消息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收到的消息</returns>
        public async Task<WebSocketMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket未连接");

            try
            {
                var buffer = new byte[_options.ReceiveBufferSize];
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                
                _statistics.BytesReceived += result.Count;
                _statistics.MessagesReceived++;
                _statistics.LastActivity = DateTime.UtcNow;

                var messageData = new byte[result.Count];
                Array.Copy(buffer, 0, messageData, 0, result.Count);
                
                return new WebSocketMessage
                {
                    Data = messageData,
                    MessageType = result.MessageType,
                    EndOfMessage = result.EndOfMessage,
                    CloseStatus = result.CloseStatus,
                    CloseStatusDescription = result.CloseStatusDescription
                };
            }
            catch (Exception ex)
            {
                _statistics.ErrorCount++;
                _logger?.LogError(ex, "接收WebSocket消息失败");
                throw;
            }
        }

        #endregion

        #region 私有方法

        private void ConfigureClientWebSocket(ClientWebSocket clientWebSocket)
        {
            // 配置WebSocket客户端选项
            clientWebSocket.Options.KeepAliveInterval = _options.KeepAliveInterval;
            
            // 添加请求头
            if (_options.RequestHeaders != null)
            {
                foreach (var header in _options.RequestHeaders)
                {
                    clientWebSocket.Options.SetRequestHeader(header.Key, header.Value);
                }
            }

            // 设置缓冲区大小
            clientWebSocket.Options.SetBuffer(_options.ReceiveBufferSize, _options.SendBufferSize);
        }

        private async Task MessageProcessingLoop(CancellationToken cancellationToken)
        {
            while (IsConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
                    
                    if (message.MessageType == WebSocketMessageType.Close)
                    {
                        _logger?.LogInformation("收到WebSocket关闭消息");
                        break;
                    }

                    // 处理心跳响应
                    if (message.MessageType == WebSocketMessageType.Text)
                    {
                        var messageText = Encoding.UTF8.GetString(message.Data);
                        if (messageText == "pong")
                        {
                            _logger?.LogDebug("收到心跳响应");
                            continue;
                        }
                    }

                    // 将消息传递给输入流
                    _inputStream.EnqueueMessage(message);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "处理WebSocket消息时发生错误");
                    Error?.Invoke(this, new ChannelErrorEventArgs(this, ex, "消息处理失败", 0, true));
                }
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
                _cancellationTokenSource.Cancel();
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放WebSocket资源时发生错误");
            }

            _webSocket?.Dispose();
            _cancellationTokenSource?.Dispose();
            _inputStream?.Dispose();
            _outputStream?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// WebSocket消息
    /// </summary>
    public class WebSocketMessage
    {
        public byte[] Data { get; set; }
        public WebSocketMessageType MessageType { get; set; }
        public bool EndOfMessage { get; set; }
        public WebSocketCloseStatus? CloseStatus { get; set; }
        public string CloseStatusDescription { get; set; }
    }

    /// <summary>
    /// WebSocket传输选项
    /// </summary>
    public class WebSocketTransportOptions
    {
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Keep-Alive间隔
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// 发送缓冲区大小
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// 请求头
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// WebSocket消息流
    /// </summary>
    internal class WebSocketMessageStream : Stream
    {
        private readonly WebSocketTransport _transport;
        private readonly bool _isInput;
        private readonly Queue<WebSocketMessage> _messageQueue = new Queue<WebSocketMessage>();
        private readonly object _lock = new object();
        private byte[] _currentMessageData;
        private int _currentPosition;

        public WebSocketMessageStream(WebSocketTransport transport, bool isInput)
        {
            _transport = transport;
            _isInput = isInput;
        }

        public override bool CanRead => _isInput;
        public override bool CanSeek => false;
        public override bool CanWrite => !_isInput;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_isInput)
                throw new NotSupportedException();

            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_isInput)
                throw new NotSupportedException();

            // 如果当前消息数据已读完，获取下一条消息
            if (_currentMessageData == null || _currentPosition >= _currentMessageData.Length)
            {
                var message = await DequeueMessageAsync(cancellationToken);
                if (message == null)
                    return 0;

                _currentMessageData = message.Data;
                _currentPosition = 0;
            }

            // 读取数据
            var bytesToRead = Math.Min(count, _currentMessageData.Length - _currentPosition);
            Array.Copy(_currentMessageData, _currentPosition, buffer, offset, bytesToRead);
            _currentPosition += bytesToRead;

            return bytesToRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_isInput)
                throw new NotSupportedException();

            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_isInput)
                throw new NotSupportedException();

            var data = new byte[count];
            Array.Copy(buffer, offset, data, 0, count);
            
            await _transport.SendMessageAsync(data, WebSocketMessageType.Text, cancellationToken);
        }

        public override void Flush()
        {
            // WebSocket自动刷新
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        internal void EnqueueMessage(WebSocketMessage message)
        {
            lock (_lock)
            {
                _messageQueue.Enqueue(message);
            }
        }

        private async Task<WebSocketMessage> DequeueMessageAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_lock)
                {
                    if (_messageQueue.Count > 0)
                    {
                        return _messageQueue.Dequeue();
                    }
                }

                await Task.Delay(10, cancellationToken);
            }

            return null;
        }
    }
} 