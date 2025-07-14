using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Transport
{
    /// <summary>
    /// 传输层工厂
    /// </summary>
    public class TransportFactory : ITransportFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TransportFactory> _logger;
        private readonly TransportFactoryOptions _options;

        public TransportFactory(IServiceProvider serviceProvider = null, ILogger<TransportFactory> logger = null, TransportFactoryOptions options = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options ?? new TransportFactoryOptions();
        }

        public ITwoWayChannel CreateTcpClient(string host, int port, TcpTransportOptions options = null)
        {
            try
            {
                var tcpOptions = options ?? _options.DefaultTcpOptions;
                var logger = _serviceProvider?.GetService<ILogger<TcpTransport>>();
                
                var transport = new TcpTransport(host, port, tcpOptions, logger);
                
                _logger?.LogInformation("创建TCP客户端传输: {Host}:{Port}", host, port);
                
                return transport;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建TCP客户端传输失败: {Host}:{Port}", host, port);
                throw;
            }
        }

        public TcpServerTransport CreateTcpServer(int port, TcpTransportOptions options = null)
        {
            try
            {
                var tcpOptions = options ?? _options.DefaultTcpOptions;
                var logger = _serviceProvider?.GetService<ILogger<TcpServerTransport>>();
                
                var serverTransport = new TcpServerTransport(port, tcpOptions, logger);
                
                _logger?.LogInformation("创建TCP服务器传输: Port {Port}", port);
                
                return serverTransport;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建TCP服务器传输失败: Port {Port}", port);
                throw;
            }
        }

        public ITwoWayChannel CreateNamedPipeClient(string serverName, string pipeName, NamedPipeTransportOptions options = null)
        {
            try
            {
                var pipeOptions = options ?? _options.DefaultNamedPipeOptions;
                var logger = _serviceProvider?.GetService<ILogger<NamedPipeTransport>>();
                
                var transport = new NamedPipeTransport(serverName, pipeName, pipeOptions, logger);
                
                _logger?.LogInformation("创建Named Pipe客户端传输: {ServerName}\\{PipeName}", serverName, pipeName);
                
                return transport;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建Named Pipe客户端传输失败: {ServerName}\\{PipeName}", serverName, pipeName);
                throw;
            }
        }

        public NamedPipeServerTransport CreateNamedPipeServer(string pipeName, NamedPipeTransportOptions options = null)
        {
            try
            {
                var pipeOptions = options ?? _options.DefaultNamedPipeOptions;
                var logger = _serviceProvider?.GetService<ILogger<NamedPipeServerTransport>>();
                
                var serverTransport = new NamedPipeServerTransport(pipeName, pipeOptions, logger);
                
                _logger?.LogInformation("创建Named Pipe服务器传输: {PipeName}", pipeName);
                
                return serverTransport;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建Named Pipe服务器传输失败: {PipeName}", pipeName);
                throw;
            }
        }

        public ITwoWayChannel CreateTransport(TransportConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                switch (config.TransportType)
                {
                    case TransportType.Tcp:
                        return CreateTcpClient(config.Host, config.Port, config.TcpOptions);
                    case TransportType.NamedPipe:
                        return CreateNamedPipeClient(config.ServerName, config.PipeName, config.NamedPipeOptions);
                    default:
                        throw new NotSupportedException($"不支持的传输类型: {config.TransportType}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "根据配置创建传输通道失败: {TransportType}", config.TransportType);
                throw;
            }
        }

        public async Task<List<ITwoWayChannel>> CreateTransportsAsync(IEnumerable<TransportConfiguration> configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            var transports = new List<ITwoWayChannel>();
            var tasks = new List<Task<ITwoWayChannel>>();

            foreach (var config in configs)
            {
                tasks.Add(Task.Run(() => CreateTransport(config)));
            }

            var results = await Task.WhenAll(tasks);
            transports.AddRange(results);

            _logger?.LogInformation("批量创建了 {Count} 个传输通道", transports.Count);

            return transports;
        }
    }

    public interface ITransportFactory
    {
        ITwoWayChannel CreateTcpClient(string host, int port, TcpTransportOptions options = null);
        TcpServerTransport CreateTcpServer(int port, TcpTransportOptions options = null);
        ITwoWayChannel CreateNamedPipeClient(string serverName, string pipeName, NamedPipeTransportOptions options = null);
        NamedPipeServerTransport CreateNamedPipeServer(string pipeName, NamedPipeTransportOptions options = null);
        ITwoWayChannel CreateTransport(TransportConfiguration config);
        Task<List<ITwoWayChannel>> CreateTransportsAsync(IEnumerable<TransportConfiguration> configs);
    }

    public class TransportFactoryOptions
    {
        public TcpTransportOptions DefaultTcpOptions { get; set; } = new TcpTransportOptions();
        public NamedPipeTransportOptions DefaultNamedPipeOptions { get; set; } = new NamedPipeTransportOptions();
        public bool EnableConnectionPooling { get; set; } = true;
        public int DefaultPoolSize { get; set; } = 10;
        public TimeSpan DefaultConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public bool EnableAutoReconnect { get; set; } = true;
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
        public int MaxReconnectAttempts { get; set; } = 3;
    }

    public class TransportConfiguration
    {
        public TransportType TransportType { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string ServerName { get; set; }
        public string PipeName { get; set; }
        public TcpTransportOptions TcpOptions { get; set; }
        public NamedPipeTransportOptions NamedPipeOptions { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public static TransportConfiguration CreateTcp(string host, int port, TcpTransportOptions options = null)
        {
            return new TransportConfiguration
            {
                TransportType = TransportType.Tcp,
                Host = host,
                Port = port,
                TcpOptions = options
            };
        }

        public static TransportConfiguration CreateNamedPipe(string serverName, string pipeName, NamedPipeTransportOptions options = null)
        {
            return new TransportConfiguration
            {
                TransportType = TransportType.NamedPipe,
                ServerName = serverName,
                PipeName = pipeName,
                NamedPipeOptions = options
            };
        }
    }
} 