using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.Client.Proxy
{
    /// <summary>
    /// RPC代理生成器
    /// 使用Castle.DynamicProxy实现动态代理生成
    /// </summary>
    public class RpcProxyGenerator : IRpcProxyGenerator
    {
        private readonly ProxyGenerator _proxyGenerator;
        private readonly ILogger<RpcProxyGenerator> _logger;
        private readonly IRpcMetadataProvider _metadataProvider;
        private readonly ConcurrentDictionary<Type, ServiceMetadata> _metadataCache;

        public RpcProxyGenerator(
            IRpcMetadataProvider metadataProvider,
            ILogger<RpcProxyGenerator> logger = null)
        {
            _proxyGenerator = new ProxyGenerator();
            _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
            _logger = logger;
            _metadataCache = new ConcurrentDictionary<Type, ServiceMetadata>();
        }

        /// <summary>
        /// 创建代理实例
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        public T CreateProxy<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class
        {
            return (T)CreateProxy(typeof(T), client, options);
        }

        /// <summary>
        /// 异步创建代理实例
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        public async Task<T> CreateProxyAsync<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class
        {
            return await Task.FromResult(CreateProxy<T>(client, options));
        }

        /// <summary>
        /// 创建代理实例（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        public object CreateProxy(Type serviceType, IRpcClient client, ProxyGenerationOptions options = null)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            options = options ?? ProxyGenerationOptions.Default;

            // 验证类型是否可以创建代理
            if (!CanCreateProxy(serviceType))
            {
                throw new InvalidOperationException($"类型 {serviceType.Name} 不能创建代理：必须是接口且标记了RpcMethodAttribute");
            }

            try
            {
                // 获取服务元数据
                var metadata = GetServiceMetadata(serviceType);
                
                // 创建拦截器
                var interceptor = CreateInterceptor(client, options, metadata);
                
                // 生成代理
                var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget(serviceType, interceptor);
                
                _logger?.LogDebug("成功创建RPC代理: {ServiceType}", serviceType.Name);
                return proxy;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建RPC代理失败: {ServiceType}", serviceType.Name);
                throw new RpcProxyException($"创建RPC代理失败: {serviceType.Name}", ex);
            }
        }

        /// <summary>
        /// 检查类型是否可以创建代理
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>是否可以创建代理</returns>
        public bool CanCreateProxy(Type serviceType)
        {
            if (serviceType == null)
                return false;

            // 必须是接口
            if (!serviceType.IsInterface)
            {
                _logger?.LogWarning("类型 {Type} 不是接口，无法创建代理", serviceType.Name);
                return false;
            }

            // 检查是否有RpcMethodAttribute标记
            var hasRpcMethodAttribute = serviceType.GetCustomAttribute<RpcMethodAttribute>() != null ||
                                      serviceType.GetMethods().Any(m => m.GetCustomAttribute<RpcMethodAttribute>() != null);

            if (!hasRpcMethodAttribute)
            {
                _logger?.LogWarning("类型 {Type} 没有RpcMethodAttribute标记，无法创建代理", serviceType.Name);
                return false;
            }

            // 检查方法是否都支持代理
            var methods = serviceType.GetMethods();
            foreach (var method in methods)
            {
                if (!IsMethodSupported(method))
                {
                    _logger?.LogWarning("类型 {Type} 的方法 {Method} 不支持代理", serviceType.Name, method.Name);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取服务元数据
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        public ServiceMetadata GetServiceMetadata(Type serviceType)
        {
            return _metadataCache.GetOrAdd(serviceType, type =>
            {
                var metadata = _metadataProvider.ExtractServiceMetadata(type);
                _logger?.LogDebug("提取服务元数据: {ServiceType}, 方法数: {MethodCount}", 
                    type.Name, metadata.Methods?.Length ?? 0);
                return metadata;
            });
        }

        /// <summary>
        /// 异步获取服务元数据
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <returns>服务元数据</returns>
        public async Task<ServiceMetadata> GetServiceMetadataAsync<T>() where T : class
        {
            return await Task.FromResult(GetServiceMetadata(typeof(T)));
        }

        /// <summary>
        /// 异步获取服务元数据（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        public async Task<ServiceMetadata> GetServiceMetadataAsync(Type serviceType)
        {
            return await Task.FromResult(GetServiceMetadata(serviceType));
        }

        /// <summary>
        /// 创建拦截器
        /// </summary>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <param name="metadata">服务元数据</param>
        /// <returns>拦截器</returns>
        private RpcProxyInterceptor CreateInterceptor(IRpcClient client, ProxyGenerationOptions options, ServiceMetadata metadata)
        {
            return new RpcProxyInterceptor(client, options, metadata, null);
        }

        /// <summary>
        /// 检查方法是否支持代理
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否支持</returns>
        private bool IsMethodSupported(MethodInfo method)
        {
            // 检查返回类型
            var returnType = method.ReturnType;
            
            // 支持的返回类型：void, Task, Task<T>, 值类型, 引用类型
            if (returnType == typeof(void) || 
                returnType == typeof(Task) ||
                returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return true;
            }

            // 同步方法（非异步）
            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _metadataCache?.Clear();
        }
    }

    /// <summary>
    /// RPC代理生成器接口
    /// </summary>
    public interface IRpcProxyGenerator : IDisposable
    {
        /// <summary>
        /// 创建代理实例
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        T CreateProxy<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class;

        /// <summary>
        /// 异步创建代理实例
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        Task<T> CreateProxyAsync<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class;

        /// <summary>
        /// 创建代理实例（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <param name="client">RPC客户端</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        object CreateProxy(Type serviceType, IRpcClient client, ProxyGenerationOptions options = null);

        /// <summary>
        /// 检查类型是否可以创建代理
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>是否可以创建代理</returns>
        bool CanCreateProxy(Type serviceType);

        /// <summary>
        /// 获取服务元数据
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        ServiceMetadata GetServiceMetadata(Type serviceType);

        /// <summary>
        /// 异步获取服务元数据
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <returns>服务元数据</returns>
        Task<ServiceMetadata> GetServiceMetadataAsync<T>() where T : class;

        /// <summary>
        /// 异步获取服务元数据（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        Task<ServiceMetadata> GetServiceMetadataAsync(Type serviceType);
    }
} 