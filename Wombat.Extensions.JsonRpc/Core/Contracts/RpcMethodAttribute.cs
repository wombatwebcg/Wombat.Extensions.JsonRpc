using System;

namespace Wombat.Extensions.JsonRpc.Core.Contracts
{
    /// <summary>
    /// 用于标注RPC方法的特性，支持类级别和方法级别的标注
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
    public class RpcMethodAttribute : Attribute
    {
        /// <summary>
        /// RPC方法名称，如果为空则使用实际方法名
        /// </summary>
        public string? MethodName { get; }

        /// <summary>
        /// 是否为通知类型（不需要返回值）
        /// </summary>
        public bool IsNotification { get; set; }

        /// <summary>
        /// 方法超时时间（毫秒）
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 是否需要身份验证
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// 方法描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 方法版本，用于API版本控制
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 是否启用返回值缓存
        /// </summary>
        public bool EnableCaching { get; set; } = false;

        /// <summary>
        /// 缓存时间（秒），仅当EnableCaching=true时有效
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 300;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="methodName">RPC方法名称，如果为空则使用实际方法名</param>
        public RpcMethodAttribute(string? methodName = null)
        {
            MethodName = methodName;
        }
    }

    /// <summary>
    /// 用于标注RPC服务的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class RpcServiceAttribute : Attribute
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string? ServiceName { get; }

        /// <summary>
        /// 服务描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 服务版本
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 是否为单例服务
        /// </summary>
        public bool IsSingleton { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        public RpcServiceAttribute(string? serviceName = null)
        {
            ServiceName = serviceName;
        }
    }
} 