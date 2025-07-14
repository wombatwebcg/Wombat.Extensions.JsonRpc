using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Contracts;
using Wombat.Extensions.JsonRpc.Validation;

namespace Wombat.Extensions.JsonRpc.Builder
{
    /// <summary>
    /// RPC目标对象构建器 - 自动发现并注册RPC方法
    /// </summary>
    public class RpcTargetBuilder
    {
        private readonly ILogger<RpcTargetBuilder> _logger;
        private readonly IRpcMetadataProvider _metadataProvider;
        private readonly RpcMethodInterceptor _methodInterceptor;
        private readonly ConcurrentDictionary<string, RpcServiceRegistration> _registrations;
        private readonly ConcurrentDictionary<Type, object> _serviceInstances;
        private readonly IServiceProvider _serviceProvider;

        public RpcTargetBuilder(
            ILogger<RpcTargetBuilder> logger = null,
            IRpcMetadataProvider metadataProvider = null,
            RpcMethodInterceptor methodInterceptor = null,
            IServiceProvider serviceProvider = null)
        {
            _logger = logger;
            _metadataProvider = metadataProvider ?? new DefaultRpcMetadataProvider();
            _methodInterceptor = methodInterceptor;
            _registrations = new ConcurrentDictionary<string, RpcServiceRegistration>();
            _serviceInstances = new ConcurrentDictionary<Type, object>();
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 自动扫描并注册程序集中的RPC服务
        /// </summary>
        /// <param name="assemblies">要扫描的程序集</param>
        /// <returns>注册的服务数量</returns>
        public async Task<int> ScanAndRegisterAsync(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetCallingAssembly() };
            }

            var registeredCount = 0;
            
            _logger?.LogInformation("开始扫描程序集中的RPC服务，程序集数量: {Count}", assemblies.Length);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && HasRpcMethods(t))
                        .ToList();

                    _logger?.LogDebug("在程序集 {Assembly} 中发现 {Count} 个RPC服务类", assembly.FullName, types.Count);

                    foreach (var type in types)
                    {
                        var registered = await RegisterServiceAsync(type);
                        if (registered)
                        {
                            registeredCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "扫描程序集 {Assembly} 时发生异常", assembly.FullName);
                }
            }

            _logger?.LogInformation("程序集扫描完成，成功注册 {Count} 个RPC服务", registeredCount);
            return registeredCount;
        }

        /// <summary>
        /// 注册单个RPC服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>是否注册成功</returns>
        public async Task<bool> RegisterServiceAsync(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            try
            {
                var serviceMetadata = await _metadataProvider.ExtractServiceMetadataAsync(serviceType);
                if (serviceMetadata == null || !serviceMetadata.Methods.Any())
                {
                    _logger?.LogWarning("服务类型 {Type} 没有找到RPC方法", serviceType.Name);
                    return false;
                }

                var serviceInstance = GetOrCreateServiceInstance(serviceType);
                var registration = new RpcServiceRegistration
                {
                    ServiceType = serviceType,
                    ServiceInstance = serviceInstance,
                    ServiceMetadata = serviceMetadata,
                    MethodTargets = new Dictionary<string, RpcMethodTarget>(),
                    RegisteredAt = DateTime.UtcNow
                };

                // 注册每个RPC方法
                foreach (var methodMetadata in serviceMetadata.Methods)
                {
                    var methodTarget = await CreateMethodTargetAsync(serviceInstance, methodMetadata);
                    registration.MethodTargets[methodMetadata.MethodName] = methodTarget;
                }

                _registrations[serviceMetadata.ServiceName] = registration;
                
                _logger?.LogInformation("成功注册RPC服务: {Service}, 方法数量: {Count}", serviceMetadata.ServiceName, serviceMetadata.Methods.Length);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注册服务 {Type} 时发生异常", serviceType.Name);
                return false;
            }
        }

        /// <summary>
        /// 注册服务实例
        /// </summary>
        /// <param name="serviceInstance">服务实例</param>
        /// <returns>是否注册成功</returns>
        public async Task<bool> RegisterServiceInstanceAsync(object serviceInstance)
        {
            if (serviceInstance == null)
                throw new ArgumentNullException(nameof(serviceInstance));

            return await RegisterServiceAsync(serviceInstance.GetType());
        }

        /// <summary>
        /// 获取RPC目标对象，用于StreamJsonRpc注册
        /// </summary>
        /// <returns>RPC目标对象字典</returns>
        public Dictionary<string, object> GetRpcTargets()
        {
            var targets = new Dictionary<string, object>();

            foreach (var registration in _registrations.Values)
            {
                foreach (var methodTarget in registration.MethodTargets.Values)
                {
                    targets[methodTarget.MethodMetadata.MethodName] = methodTarget.TargetDelegate;
                }
            }

            _logger?.LogDebug("生成RPC目标对象字典，方法数量: {Count}", targets.Count);
            return targets;
        }

        /// <summary>
        /// 应用到JsonRpc实例
        /// </summary>
        /// <param name="jsonRpc">JsonRpc实例</param>
        public void ApplyToJsonRpc(StreamJsonRpc.JsonRpc jsonRpc)
        {
            if (jsonRpc == null)
                throw new ArgumentNullException(nameof(jsonRpc));

            var targets = GetRpcTargets();
            
            foreach (var target in targets)
            {
                jsonRpc.AddLocalRpcMethod(target.Key, (Delegate)target.Value);
            }

            _logger?.LogInformation("已将 {Count} 个RPC方法应用到JsonRpc实例", targets.Count);
        }

        /// <summary>
        /// 获取服务注册信息
        /// </summary>
        /// <returns>服务注册信息列表</returns>
        public List<RpcServiceRegistration> GetServiceRegistrations()
        {
            return _registrations.Values.ToList();
        }

        /// <summary>
        /// 获取服务元数据
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>服务元数据</returns>
        public ServiceMetadata GetServiceMetadata(string serviceName)
        {
            return _registrations.TryGetValue(serviceName, out var registration) 
                ? registration.ServiceMetadata 
                : null;
        }

        /// <summary>
        /// 检查类型是否包含RPC方法
        /// </summary>
        private bool HasRpcMethods(Type type)
        {
            // 检查类是否有RpcServiceAttribute
            if (type.GetCustomAttribute<RpcServiceAttribute>() != null)
                return true;

            // 检查方法是否有RpcMethodAttribute
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            return methods.Any(m => m.GetCustomAttribute<RpcMethodAttribute>() != null);
        }

        /// <summary>
        /// 获取或创建服务实例
        /// </summary>
        private object GetOrCreateServiceInstance(Type serviceType)
        {
            return _serviceInstances.GetOrAdd(serviceType, type =>
            {
                // 优先使用依赖注入容器
                if (_serviceProvider != null)
                {
                    try
                    {
                        var service = _serviceProvider.GetService(type);
                        if (service != null)
                        {
                            _logger?.LogDebug("从依赖注入容器获取服务实例: {Type}", type.Name);
                            return service;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "从依赖注入容器获取服务 {Type} 失败，将使用默认构造函数", type.Name);
                    }
                }

                // 使用默认构造函数创建实例
                try
                {
                    var instance = Activator.CreateInstance(type);
                    _logger?.LogDebug("使用默认构造函数创建服务实例: {Type}", type.Name);
                    return instance;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "创建服务实例 {Type} 失败", type.Name);
                    throw;
                }
            });
        }

        /// <summary>
        /// 创建方法目标
        /// </summary>
        private async Task<RpcMethodTarget> CreateMethodTargetAsync(object serviceInstance, MethodMetadata methodMetadata)
        {
            var method = serviceInstance.GetType().GetMethod(methodMetadata.DisplayName);
            if (method == null)
            {
                throw new InvalidOperationException($"方法 {methodMetadata.DisplayName} 在服务实例中不存在");
            }

            Func<object[], Task<object>> targetDelegate;

            // 如果有拦截器且方法需要验证，则使用拦截器
            if (_methodInterceptor != null && _methodInterceptor.ShouldValidate(method))
            {
                targetDelegate = _methodInterceptor.CreateProxy(method, serviceInstance);
                _logger?.LogDebug("为方法 {Method} 创建了验证拦截器", methodMetadata.MethodName);
            }
            else
            {
                // 直接调用方法
                targetDelegate = async (args) =>
                {
                    var result = method.Invoke(serviceInstance, args);
                    
                    if (result is Task taskResult)
                    {
                        await taskResult;
                        
                        if (taskResult.GetType().IsGenericType)
                        {
                            var property = taskResult.GetType().GetProperty("Result");
                            return property?.GetValue(taskResult);
                        }
                        return null;
                    }
                    
                    return result;
                };
            }

            return new RpcMethodTarget
            {
                MethodName = methodMetadata.MethodName,
                MethodInfo = method,
                MethodMetadata = methodMetadata,
                TargetDelegate = targetDelegate,
                ServiceInstance = serviceInstance
            };
        }
    }

    /// <summary>
    /// RPC服务注册信息
    /// </summary>
    public class RpcServiceRegistration
    {
        public Type ServiceType { get; set; }
        public object ServiceInstance { get; set; }
        public ServiceMetadata ServiceMetadata { get; set; }
        public Dictionary<string, RpcMethodTarget> MethodTargets { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    /// <summary>
    /// RPC方法目标
    /// </summary>
    public class RpcMethodTarget
    {
        public string MethodName { get; set; }
        public MethodInfo MethodInfo { get; set; }
        public MethodMetadata MethodMetadata { get; set; }
        public Func<object[], Task<object>> TargetDelegate { get; set; }
        public object ServiceInstance { get; set; }
    }
} 