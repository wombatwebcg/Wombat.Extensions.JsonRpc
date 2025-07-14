using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Contracts
{
    /// <summary>
    /// 默认RPC元数据提供程序实现
    /// </summary>
    public class DefaultRpcMetadataProvider : IRpcMetadataProvider
    {
        private readonly IRpcMetadataCache _cache;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cache">元数据缓存</param>
        public DefaultRpcMetadataProvider(IRpcMetadataCache cache = null)
        {
            _cache = cache ?? new DefaultRpcMetadataCache();
        }

        /// <summary>
        /// 从类型提取服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        public ServiceMetadata ExtractServiceMetadata(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            // 首先尝试从缓存获取
            var cached = _cache.GetServiceMetadata(serviceType);
            if (cached != null)
                return cached;

            // 验证服务类型
            if (!ValidateServiceType(serviceType))
                throw new InvalidOperationException($"类型 {serviceType.Name} 不是有效的RPC服务类型");

            var metadata = new ServiceMetadata
            {
                ServiceType = serviceType,
                ServiceName = GetRpcServiceName(serviceType),
                Methods = GetRpcMethods(serviceType).ToArray(),
                CreatedAt = DateTime.UtcNow
            };

            // 从RpcServiceAttribute提取服务信息
            var serviceAttr = serviceType.GetCustomAttribute<RpcServiceAttribute>();
            if (serviceAttr != null)
            {
                metadata.Description = serviceAttr.Description;
                metadata.Version = serviceAttr.Version;
                metadata.IsSingleton = serviceAttr.IsSingleton;
            }

            // 检查是否实现了接口
            var interfaces = serviceType.GetInterfaces();
            var rpcInterface = interfaces.FirstOrDefault(i => i.GetCustomAttribute<RpcServiceAttribute>() != null);
            if (rpcInterface != null)
            {
                metadata.InterfaceType = rpcInterface;
            }

            // 缓存结果
            _cache.SetServiceMetadata(serviceType, metadata);

            return metadata;
        }

        /// <summary>
        /// 从类型提取服务元数据（异步）
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        public Task<ServiceMetadata> ExtractServiceMetadataAsync(Type serviceType)
        {
            return Task.FromResult(ExtractServiceMetadata(serviceType));
        }

        /// <summary>
        /// 从程序集提取所有服务元数据
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <param name="filter">类型过滤器</param>
        /// <returns>服务元数据集合</returns>
        public IEnumerable<ServiceMetadata> ExtractServicesFromAssembly(Assembly assembly, Func<Type, bool> filter = null)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var types = assembly.GetTypes();
            var results = new List<ServiceMetadata>();
            
            foreach (var type in types)
            {
                try
                {
                    // 应用过滤器
                    if (filter != null && !filter(type))
                        continue;

                    // 检查是否为RPC服务
                    if (!ValidateServiceType(type))
                        continue;

                    results.Add(ExtractServiceMetadata(type));
                }
                catch
                {
                    // 忽略无法处理的类型
                    continue;
                }
            }
            
            return results;
        }

        /// <summary>
        /// 从程序集提取所有服务元数据（异步）
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <param name="filter">类型过滤器</param>
        /// <returns>服务元数据集合</returns>
        public async Task<IEnumerable<ServiceMetadata>> ExtractServicesFromAssemblyAsync(Assembly assembly, Func<Type, bool> filter = null)
        {
            return await Task.Run(() => ExtractServicesFromAssembly(assembly, filter).ToArray());
        }

        /// <summary>
        /// 验证服务类型是否有效
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>验证结果</returns>
        public bool ValidateServiceType(Type serviceType)
        {
            if (serviceType == null)
                return false;

            // 检查是否为抽象类或接口
            if (serviceType.IsAbstract || serviceType.IsInterface)
            {
                // 接口必须标记RpcServiceAttribute
                return serviceType.GetCustomAttribute<RpcServiceAttribute>() != null ||
                       serviceType.GetCustomAttribute<RpcMethodAttribute>() != null;
            }

            // 类必须是公共的、非抽象的
            if (!serviceType.IsPublic && !serviceType.IsNestedPublic)
                return false;

            // 检查是否有RPC方法或服务特性
            var hasRpcAttribute = serviceType.GetCustomAttribute<RpcServiceAttribute>() != null ||
                                  serviceType.GetCustomAttribute<RpcMethodAttribute>() != null;

            var hasRpcMethods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => IsRpcMethod(m));

            // 检查实现的接口是否有RPC特性
            var hasRpcInterface = serviceType.GetInterfaces()
                .Any(i => i.GetCustomAttribute<RpcServiceAttribute>() != null ||
                         i.GetCustomAttribute<RpcMethodAttribute>() != null);

            return hasRpcAttribute || hasRpcMethods || hasRpcInterface;
        }

        /// <summary>
        /// 获取服务的RPC方法
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>RPC方法集合</returns>
        public IEnumerable<MethodMetadata> GetRpcMethods(Type serviceType)
        {
            if (serviceType == null)
                yield break;

            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            // 如果是接口，也获取接口方法
            if (serviceType.IsInterface)
            {
                var interfaceMethods = serviceType.GetMethods();
                methods = methods.Concat(interfaceMethods).ToArray();
            }

            // 检查实现的接口方法
            foreach (var interfaceType in serviceType.GetInterfaces())
            {
                if (interfaceType.GetCustomAttribute<RpcServiceAttribute>() != null ||
                    interfaceType.GetCustomAttribute<RpcMethodAttribute>() != null)
                {
                    methods = methods.Concat(interfaceType.GetMethods()).ToArray();
                }
            }

            foreach (var method in methods)
            {
                if (IsRpcMethod(method))
                {
                    yield return CreateMethodMetadata(method);
                }
            }
        }

        /// <summary>
        /// 检查方法是否为RPC方法
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否为RPC方法</returns>
        public bool IsRpcMethod(MethodInfo method)
        {
            if (method == null)
                return false;

            // 检查方法特性
            var methodAttr = method.GetCustomAttribute<RpcMethodAttribute>();
            if (methodAttr != null)
                return true;

            // 检查类特性
            var classAttr = method.DeclaringType?.GetCustomAttribute<RpcMethodAttribute>();
            if (classAttr != null)
                return true;

            // 检查接口特性
            var interfaceAttr = method.DeclaringType?.GetInterfaces()
                .FirstOrDefault(i => i.GetCustomAttribute<RpcMethodAttribute>() != null);
            if (interfaceAttr != null)
                return true;

            return false;
        }

        /// <summary>
        /// 获取方法的RPC名称
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>RPC方法名称</returns>
        public string GetRpcMethodName(MethodInfo method)
        {
            if (method == null)
                return string.Empty;

            var methodAttr = method.GetCustomAttribute<RpcMethodAttribute>();
            if (methodAttr != null && !string.IsNullOrEmpty(methodAttr.MethodName))
                return methodAttr.MethodName;

            return method.Name;
        }

        /// <summary>
        /// 获取服务的RPC名称
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>RPC服务名称</returns>
        public string GetRpcServiceName(Type serviceType)
        {
            if (serviceType == null)
                return string.Empty;

            var serviceAttr = serviceType.GetCustomAttribute<RpcServiceAttribute>();
            if (serviceAttr != null && !string.IsNullOrEmpty(serviceAttr.ServiceName))
                return serviceAttr.ServiceName;

            // 检查接口特性
            var interfaceAttr = serviceType.GetInterfaces()
                .FirstOrDefault(i => i.GetCustomAttribute<RpcServiceAttribute>() != null)
                ?.GetCustomAttribute<RpcServiceAttribute>();
            if (interfaceAttr != null && !string.IsNullOrEmpty(interfaceAttr.ServiceName))
                return interfaceAttr.ServiceName;

            return serviceType.Name;
        }

        /// <summary>
        /// 创建方法元数据
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>方法元数据</returns>
        private MethodMetadata CreateMethodMetadata(MethodInfo method)
        {
            var methodAttr = method.GetCustomAttribute<RpcMethodAttribute>();
            
            var metadata = new MethodMetadata
            {
                MethodName = GetRpcMethodName(method),
                DisplayName = method.Name,
                MethodInfo = method,
                ReturnType = method.ReturnType,
                Parameters = CreateParameterMetadata(method).ToArray()
            };

            if (methodAttr != null)
            {
                metadata.IsNotification = methodAttr.IsNotification;
                metadata.RequireAuthentication = methodAttr.RequireAuthentication;
                metadata.TimeoutMs = methodAttr.TimeoutMs;
                metadata.Description = methodAttr.Description;
                metadata.Version = methodAttr.Version;
                metadata.EnableParameterValidation = methodAttr.EnableParameterValidation;
                metadata.EnableCaching = methodAttr.EnableCaching;
                metadata.CacheDurationSeconds = methodAttr.CacheDurationSeconds;
            }

            return metadata;
        }

        /// <summary>
        /// 创建参数元数据
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>参数元数据集合</returns>
        private IEnumerable<ParameterMetadata> CreateParameterMetadata(MethodInfo method)
        {
            var parameters = method.GetParameters();
            
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var metadata = new ParameterMetadata
                {
                    Name = param.Name ?? $"param{i}",
                    Type = param.ParameterType,
                    Position = i,
                    ParameterInfo = param,
                    IsRequired = !param.HasDefaultValue,
                    DefaultValue = param.HasDefaultValue ? param.DefaultValue : null,
                    Validations = param.GetCustomAttributes<ValidationAttribute>().ToArray()
                };

                yield return metadata;
            }
        }
    }

    /// <summary>
    /// 默认RPC元数据缓存实现
    /// </summary>
    public class DefaultRpcMetadataCache : IRpcMetadataCache
    {
        private readonly ConcurrentDictionary<Type, ServiceMetadata> _cache = new ConcurrentDictionary<Type, ServiceMetadata>();

        /// <summary>
        /// 获取服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        public ServiceMetadata GetServiceMetadata(Type serviceType)
        {
            return _cache.TryGetValue(serviceType, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// 设置服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="metadata">服务元数据</param>
        public void SetServiceMetadata(Type serviceType, ServiceMetadata metadata)
        {
            _cache.TryAdd(serviceType, metadata);
        }

        /// <summary>
        /// 移除服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveServiceMetadata(Type serviceType)
        {
            return _cache.TryRemove(serviceType, out _);
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 获取所有缓存的服务类型
        /// </summary>
        /// <returns>服务类型集合</returns>
        public IEnumerable<Type> GetCachedServiceTypes()
        {
            return _cache.Keys;
        }

        /// <summary>
        /// 获取所有缓存的服务元数据
        /// </summary>
        /// <returns>服务元数据集合</returns>
        public IEnumerable<ServiceMetadata> GetAllServiceMetadata()
        {
            return _cache.Values;
        }
    }
} 