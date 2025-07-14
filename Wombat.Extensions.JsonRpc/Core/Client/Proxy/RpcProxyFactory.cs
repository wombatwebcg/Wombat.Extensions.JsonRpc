using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Core.Contracts;

namespace Wombat.Extensions.JsonRpc.Core.Client.Proxy
{
    /// <summary>
    /// RPC代理工厂实现
    /// </summary>
    public class RpcProxyFactory : IRpcProxyFactory
    {
        private readonly IRpcProxyGenerator _proxyGenerator;
        private readonly ILogger<RpcProxyFactory> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="proxyGenerator">代理生成器</param>
        /// <param name="logger">日志记录器</param>
        public RpcProxyFactory(
            IRpcProxyGenerator proxyGenerator,
            ILogger<RpcProxyFactory> logger = null)
        {
            _proxyGenerator = proxyGenerator ?? throw new ArgumentNullException(nameof(proxyGenerator));
            _logger = logger;
        }

        /// <summary>
        /// 创建强类型RPC代理
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>强类型代理实例</returns>
        public T CreateProxy<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {
                _logger?.LogDebug("创建RPC代理: {ServiceType}", typeof(T).Name);
                
                var proxy = _proxyGenerator.CreateProxy<T>(client, options);
                
                _logger?.LogDebug("RPC代理创建成功: {ServiceType}", typeof(T).Name);
                
                return proxy;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建RPC代理失败: {ServiceType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// 异步创建强类型RPC代理
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>强类型代理实例</returns>
        public async Task<T> CreateProxyAsync<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {
                _logger?.LogDebug("异步创建RPC代理: {ServiceType}", typeof(T).Name);
                
                var proxy = await _proxyGenerator.CreateProxyAsync<T>(client, options);
                
                _logger?.LogDebug("RPC代理异步创建成功: {ServiceType}", typeof(T).Name);
                
                return proxy;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "异步创建RPC代理失败: {ServiceType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// 创建代理实例（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        public object CreateProxy(Type serviceType, IRpcClient client, ProxyGenerationOptions options = null)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {
                _logger?.LogDebug("创建RPC代理: {ServiceType}", serviceType.Name);
                
                var proxy = _proxyGenerator.CreateProxy(serviceType, client, options);
                
                _logger?.LogDebug("RPC代理创建成功: {ServiceType}", serviceType.Name);
                
                return proxy;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建RPC代理失败: {ServiceType}", serviceType.Name);
                throw;
            }
        }

        /// <summary>
        /// 检查类型是否可以创建代理
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>是否可以创建代理</returns>
        public bool CanCreateProxy(Type serviceType)
        {
            return _proxyGenerator.CanCreateProxy(serviceType);
        }

        /// <summary>
        /// 获取服务的元数据信息
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <returns>服务元数据</returns>
        public async Task<ServiceMetadata> GetServiceMetadataAsync<T>() where T : class
        {
            try
            {
                _logger?.LogDebug("获取服务元数据: {ServiceType}", typeof(T).Name);
                
                var metadata = await _proxyGenerator.GetServiceMetadataAsync<T>();
                
                _logger?.LogDebug("服务元数据获取成功: {ServiceType}, 方法数: {MethodCount}", 
                    typeof(T).Name, metadata.Methods?.Length ?? 0);
                
                return metadata;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取服务元数据失败: {ServiceType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// 获取服务的元数据信息（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        public async Task<ServiceMetadata> GetServiceMetadataAsync(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            try
            {
                _logger?.LogDebug("获取服务元数据: {ServiceType}", serviceType.Name);
                
                var metadata = await _proxyGenerator.GetServiceMetadataAsync(serviceType);
                
                _logger?.LogDebug("服务元数据获取成功: {ServiceType}, 方法数: {MethodCount}", 
                    serviceType.Name, metadata.Methods?.Length ?? 0);
                
                return metadata;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取服务元数据失败: {ServiceType}", serviceType.Name);
                throw;
            }
        }
    }
} 