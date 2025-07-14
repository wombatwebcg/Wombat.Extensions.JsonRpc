using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Wombat.Extensions.JsonRpc.Core.Contracts
{
    /// <summary>
    /// RPC服务元数据
    /// </summary>
    public class ServiceMetadata
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// 服务类型
        /// </summary>
        public Type ServiceType { get; set; } = typeof(object);

        /// <summary>
        /// 服务接口类型
        /// </summary>
        public Type? InterfaceType { get; set; }

        /// <summary>
        /// 服务方法集合
        /// </summary>
        public MethodMetadata[] Methods { get; set; } = Array.Empty<MethodMetadata>();

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
        /// 服务创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 服务标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取方法元数据
        /// </summary>
        /// <param name="methodName">方法名</param>
        /// <returns>方法元数据</returns>
        public MethodMetadata? GetMethod(string methodName)
        {
            foreach (var method in Methods)
            {
                if (method.MethodName == methodName || method.DisplayName == methodName)
                {
                    return method;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取所有需要认证的方法
        /// </summary>
        /// <returns>需要认证的方法集合</returns>
        public IEnumerable<MethodMetadata> GetAuthenticatedMethods()
        {
            foreach (var method in Methods)
            {
                if (method.RequireAuthentication)
                {
                    yield return method;
                }
            }
        }

        /// <summary>
        /// 获取所有通知方法
        /// </summary>
        /// <returns>通知方法集合</returns>
        public IEnumerable<MethodMetadata> GetNotificationMethods()
        {
            foreach (var method in Methods)
            {
                if (method.IsNotification)
                {
                    yield return method;
                }
            }
        }
    }

    /// <summary>
    /// RPC方法元数据
    /// </summary>
    public class MethodMetadata
    {
        /// <summary>
        /// 方法名称（用于RPC调用）
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（实际方法名）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为通知类型
        /// </summary>
        public bool IsNotification { get; set; }

        /// <summary>
        /// 是否需要身份验证
        /// </summary>
        public bool RequireAuthentication { get; set; }

        /// <summary>
        /// 方法超时时间（毫秒）
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 方法参数集合
        /// </summary>
        public ParameterMetadata[] Parameters { get; set; } = Array.Empty<ParameterMetadata>();

        /// <summary>
        /// 返回类型
        /// </summary>
        public Type ReturnType { get; set; } = typeof(void);

        /// <summary>
        /// 方法描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 方法版本
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCaching { get; set; }

        /// <summary>
        /// 缓存时间（秒）
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 300;

        /// <summary>
        /// 方法反射信息
        /// </summary>
        public MethodInfo MethodInfo { get; set; } = null!;

        /// <summary>
        /// 方法标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取参数元数据
        /// </summary>
        /// <param name="parameterName">参数名</param>
        /// <returns>参数元数据</returns>
        public ParameterMetadata? GetParameter(string parameterName)
        {
            foreach (var param in Parameters)
            {
                if (param.Name == parameterName)
                {
                    return param;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取所有必需参数
        /// </summary>
        /// <returns>必需参数集合</returns>
        public IEnumerable<ParameterMetadata> GetRequiredParameters()
        {
            foreach (var param in Parameters)
            {
                if (param.IsRequired)
                {
                    yield return param;
                }
            }
        }
    }

    /// <summary>
    /// RPC方法参数元数据
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type Type { get; set; } = typeof(object);

        /// <summary>
        /// 是否为必需参数
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// 默认值
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// 参数验证特性集合
        /// </summary>
        public ValidationAttribute[] Validations { get; set; } = Array.Empty<ValidationAttribute>();

        /// <summary>
        /// 参数描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 参数位置
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 参数反射信息
        /// </summary>
        public ParameterInfo ParameterInfo { get; set; } = null!;

        /// <summary>
        /// 参数标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 验证参数值
        /// </summary>
        /// <param name="value">参数值</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateValue(object? value)
        {
            var context = new ValidationContext(new object()) { MemberName = Name };
            
            foreach (var validation in Validations)
            {
                var result = validation.GetValidationResult(value, context);
                if (result != ValidationResult.Success)
                {
                    return result;
                }
            }

            return ValidationResult.Success!;
        }

        /// <summary>
        /// 获取参数的显示类型名称
        /// </summary>
        /// <returns>显示类型名称</returns>
        public string GetDisplayTypeName()
        {
            if (Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Type.GetGenericArguments()[0].Name + "?";
            }

            return Type.Name;
        }
    }

    /// <summary>
    /// RPC服务端点信息
    /// </summary>
    public class ServiceEndpoint
    {
        /// <summary>
        /// 端点地址
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 端点协议
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// 端点状态
        /// </summary>
        public ServiceEndpointStatus Status { get; set; } = ServiceEndpointStatus.Unknown;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 端点权重（用于负载均衡）
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// 端点标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 服务端点状态
    /// </summary>
    public enum ServiceEndpointStatus
    {
        /// <summary>
        /// 未知状态
        /// </summary>
        Unknown,

        /// <summary>
        /// 健康状态
        /// </summary>
        Healthy,

        /// <summary>
        /// 不健康状态
        /// </summary>
        Unhealthy,

        /// <summary>
        /// 离线状态
        /// </summary>
        Offline
    }
} 