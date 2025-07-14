using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Core.Contracts
{
    /// <summary>
    /// RPC元数据提供程序接口
    /// </summary>
    public interface IRpcMetadataProvider
    {
        /// <summary>
        /// 从类型提取服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        ServiceMetadata ExtractServiceMetadata(Type serviceType);

        /// <summary>
        /// 从类型提取服务元数据（异步）
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        Task<ServiceMetadata> ExtractServiceMetadataAsync(Type serviceType);

        /// <summary>
        /// 从程序集提取所有服务元数据
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <param name="filter">类型过滤器</param>
        /// <returns>服务元数据集合</returns>
        IEnumerable<ServiceMetadata> ExtractServicesFromAssembly(Assembly assembly, Func<Type, bool>? filter = null);

        /// <summary>
        /// 从程序集提取所有服务元数据（异步）
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <param name="filter">类型过滤器</param>
        /// <returns>服务元数据集合</returns>
        Task<IEnumerable<ServiceMetadata>> ExtractServicesFromAssemblyAsync(Assembly assembly, Func<Type, bool>? filter = null);

        /// <summary>
        /// 验证服务类型是否有效
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>验证结果</returns>
        bool ValidateServiceType(Type serviceType);

        /// <summary>
        /// 获取服务的RPC方法
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>RPC方法集合</returns>
        IEnumerable<MethodMetadata> GetRpcMethods(Type serviceType);

        /// <summary>
        /// 检查方法是否为RPC方法
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否为RPC方法</returns>
        bool IsRpcMethod(MethodInfo method);

        /// <summary>
        /// 获取方法的RPC名称
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>RPC方法名称</returns>
        string GetRpcMethodName(MethodInfo method);

        /// <summary>
        /// 获取服务的RPC名称
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>RPC服务名称</returns>
        string GetRpcServiceName(Type serviceType);
    }

    /// <summary>
    /// RPC元数据缓存接口
    /// </summary>
    public interface IRpcMetadataCache
    {
        /// <summary>
        /// 获取服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务元数据</returns>
        ServiceMetadata? GetServiceMetadata(Type serviceType);

        /// <summary>
        /// 设置服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="metadata">服务元数据</param>
        void SetServiceMetadata(Type serviceType, ServiceMetadata metadata);

        /// <summary>
        /// 移除服务元数据
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>是否移除成功</returns>
        bool RemoveServiceMetadata(Type serviceType);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取所有缓存的服务类型
        /// </summary>
        /// <returns>服务类型集合</returns>
        IEnumerable<Type> GetCachedServiceTypes();

        /// <summary>
        /// 获取所有缓存的服务元数据
        /// </summary>
        /// <returns>服务元数据集合</returns>
        IEnumerable<ServiceMetadata> GetAllServiceMetadata();
    }

    /// <summary>
    /// RPC服务注册信息
    /// </summary>
    public class RpcServiceRegistration
    {
        /// <summary>
        /// 服务类型
        /// </summary>
        public Type ServiceType { get; set; } = typeof(object);

        /// <summary>
        /// 服务接口类型
        /// </summary>
        public Type? InterfaceType { get; set; }

        /// <summary>
        /// 服务实例
        /// </summary>
        public object? ServiceInstance { get; set; }

        /// <summary>
        /// 服务工厂
        /// </summary>
        public Func<IServiceProvider, object>? ServiceFactory { get; set; }

        /// <summary>
        /// 服务生命周期
        /// </summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;

        /// <summary>
        /// 服务元数据
        /// </summary>
        public ServiceMetadata? Metadata { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 注册选项
        /// </summary>
        public ServiceRegistrationOptions? Options { get; set; }
    }

    /// <summary>
    /// 服务生命周期
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// 单例
        /// </summary>
        Singleton,

        /// <summary>
        /// 作用域
        /// </summary>
        Scoped,

        /// <summary>
        /// 临时
        /// </summary>
        Transient
    }

    /// <summary>
    /// 服务注册选项
    /// </summary>
    public class ServiceRegistrationOptions
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// 服务描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 服务版本
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCaching { get; set; } = false;

        /// <summary>
        /// 默认缓存时间（秒）
        /// </summary>
        public int DefaultCacheDurationSeconds { get; set; } = 300;

        /// <summary>
        /// 是否需要身份验证
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// 默认超时时间（毫秒）
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 服务标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 方法过滤器
        /// </summary>
        public Func<MethodInfo, bool>? MethodFilter { get; set; }

        /// <summary>
        /// 是否忽略异常
        /// </summary>
        public bool IgnoreExceptions { get; set; } = false;
    }
} 