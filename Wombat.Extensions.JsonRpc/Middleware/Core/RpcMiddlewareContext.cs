using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.Middleware.Core
{
    /// <summary>
    /// RPC中间件上下文
    /// </summary>
    public class RpcMiddlewareContext
    {
        /// <summary>
        /// 请求ID
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 关联ID（用于分布式追踪）
        /// </summary>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 请求开始时间
        /// </summary>
        public DateTime RequestStartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// RPC方法名称
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 目标方法信息
        /// </summary>
        public MethodInfo TargetMethod { get; set; }

        /// <summary>
        /// 服务实例
        /// </summary>
        public object ServiceInstance { get; set; }

        /// <summary>
        /// 方法参数
        /// </summary>
        public object[] Arguments { get; set; }

        /// <summary>
        /// 方法返回值
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 是否已处理异常
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// 用户身份信息
        /// </summary>
        public ClaimsPrincipal User { get; set; }

        /// <summary>
        /// 是否已认证
        /// </summary>
        public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

        /// <summary>
        /// 认证方案
        /// </summary>
        public string AuthenticationScheme { get; set; }

        /// <summary>
        /// 客户端信息
        /// </summary>
        public ClientInfo ClientInfo { get; set; }

        /// <summary>
        /// 方法元数据
        /// </summary>
        public MethodMetadata MethodMetadata { get; set; }

        /// <summary>
        /// 服务元数据
        /// </summary>
        public ServiceMetadata ServiceMetadata { get; set; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 服务提供程序
        /// </summary>
        public IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 请求属性字典
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 设置属性值
        /// </summary>
        /// <param name="key">属性键</param>
        /// <param name="value">属性值</param>
        public void SetProperty(string key, object value)
        {
            Properties[key] = value;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="key">属性键</param>
        /// <returns>属性值</returns>
        public T GetProperty<T>(string key)
        {
            if (Properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }

        /// <summary>
        /// 检查是否有属性
        /// </summary>
        /// <param name="key">属性键</param>
        /// <returns>是否存在</returns>
        public bool HasProperty(string key)
        {
            return Properties.ContainsKey(key);
        }

        /// <summary>
        /// 获取请求持续时间
        /// </summary>
        /// <returns>持续时间</returns>
        public TimeSpan GetRequestDuration()
        {
            return DateTime.UtcNow - RequestStartTime;
        }

        /// <summary>
        /// 检查用户是否有指定角色
        /// </summary>
        /// <param name="role">角色名称</param>
        /// <returns>是否有角色</returns>
        public bool IsInRole(string role)
        {
            return User?.IsInRole(role) == true;
        }

        /// <summary>
        /// 检查用户是否有指定权限
        /// </summary>
        /// <param name="permission">权限名称</param>
        /// <returns>是否有权限</returns>
        public bool HasPermission(string permission)
        {
            return User?.HasClaim("permission", permission) == true;
        }

        /// <summary>
        /// 获取用户声明值
        /// </summary>
        /// <param name="claimType">声明类型</param>
        /// <returns>声明值</returns>
        public string GetClaimValue(string claimType)
        {
            return User?.FindFirst(claimType)?.Value;
        }

        /// <summary>
        /// 获取用户ID
        /// </summary>
        /// <returns>用户ID</returns>
        public string GetUserId()
        {
            return GetClaimValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// 获取用户名
        /// </summary>
        /// <returns>用户名</returns>
        public string GetUserName()
        {
            return GetClaimValue(ClaimTypes.Name);
        }

        /// <summary>
        /// 标记响应为已缓存
        /// </summary>
        public void MarkAsCached()
        {
            SetProperty("IsCached", true);
        }

        /// <summary>
        /// 检查响应是否已缓存
        /// </summary>
        /// <returns>是否已缓存</returns>
        public bool IsCached()
        {
            return GetProperty<bool>("IsCached");
        }

        /// <summary>
        /// 设置缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public void SetCacheKey(string cacheKey)
        {
            SetProperty("CacheKey", cacheKey);
        }

        /// <summary>
        /// 获取缓存键
        /// </summary>
        /// <returns>缓存键</returns>
        public string GetCacheKey()
        {
            return GetProperty<string>("CacheKey");
        }
    }

    /// <summary>
    /// 客户端信息
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// 客户端用户代理
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectTime { get; set; }

        /// <summary>
        /// 连接协议
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// 客户端版本
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 客户端属性
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
} 