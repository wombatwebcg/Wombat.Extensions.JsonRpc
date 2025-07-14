using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Middleware.Core;

namespace Wombat.Extensions.JsonRpc.Middleware.Authorization
{
    /// <summary>
    /// 角色授权中间件
    /// </summary>
    [RpcMiddleware("Role Authorization", MiddlewareOrder.Security)]
    public class RoleAuthorizationMiddleware : RpcMiddlewareBase
    {
        private readonly RoleAuthorizationOptions _options;
        private readonly ILogger<RoleAuthorizationMiddleware> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">角色授权选项</param>
        /// <param name="logger">日志记录器</param>
        public RoleAuthorizationMiddleware(RoleAuthorizationOptions options, ILogger<RoleAuthorizationMiddleware> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// 处理授权
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        /// <returns>任务</returns>
        public override async Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next)
        {
            try
            {
                // 检查是否需要授权
                if (!RequiresAuthorization(context))
                {
                    await next();
                    return;
                }

                // 检查用户是否已认证
                if (!context.IsAuthenticated)
                {
                    _logger?.LogWarning("用户未认证，无法进行授权检查，请求: {RequestId}", context.RequestId);
                    throw new UnauthorizedAccessException("用户未认证");
                }

                // 获取所需角色
                var requiredRoles = GetRequiredRoles(context);
                if (requiredRoles == null || requiredRoles.Length == 0)
                {
                    // 没有指定角色要求，允许通过
                    await next();
                    return;
                }

                // 检查用户角色
                var userRoles = GetUserRoles(context);
                if (!HasRequiredRole(userRoles, requiredRoles))
                {
                    _logger?.LogWarning("用户角色不足，用户: {User}, 需要角色: {RequiredRoles}, 用户角色: {UserRoles}, 请求: {RequestId}", 
                        context.GetUserName(), string.Join(",", requiredRoles), string.Join(",", userRoles), context.RequestId);
                    throw new UnauthorizedAccessException("用户角色不足");
                }

                _logger?.LogDebug("角色授权成功，用户: {User}, 角色: {Roles}, 请求: {RequestId}", 
                    context.GetUserName(), string.Join(",", userRoles), context.RequestId);

                await next();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "角色授权失败，请求: {RequestId}", context.RequestId);
                context.Exception = ex;
                context.ExceptionHandled = false;
                throw;
            }
        }

        /// <summary>
        /// 检查是否需要授权
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>是否需要授权</returns>
        private bool RequiresAuthorization(RpcMiddlewareContext context)
        {
            // 检查全局授权要求
            if (_options.RequireAuthorizationForAllMethods)
            {
                return true;
            }

            // 检查方法级别的授权要求
            if (context.MethodMetadata?.RequireAuthorization == true)
            {
                return true;
            }

            // 检查服务级别的授权要求
            if (context.ServiceMetadata?.RequireAuthorization == true)
            {
                return true;
            }

            // 检查是否有角色要求
            var requiredRoles = GetRequiredRoles(context);
            return requiredRoles != null && requiredRoles.Length > 0;
        }

        /// <summary>
        /// 获取所需角色
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>所需角色</returns>
        private string[] GetRequiredRoles(RpcMiddlewareContext context)
        {
            var roles = new List<string>();

            // 从方法元数据获取角色
            if (context.MethodMetadata?.RequiredRoles != null)
            {
                roles.AddRange(context.MethodMetadata.RequiredRoles);
            }

            // 从服务元数据获取角色
            if (context.ServiceMetadata?.RequiredRoles != null)
            {
                roles.AddRange(context.ServiceMetadata.RequiredRoles);
            }

            // 从配置中获取方法角色
            if (_options.MethodRoles.TryGetValue(context.MethodName, out var methodRoles))
            {
                roles.AddRange(methodRoles);
            }

            // 从配置中获取服务角色
            if (_options.ServiceRoles.TryGetValue(context.ServiceMetadata?.ServiceName, out var serviceRoles))
            {
                roles.AddRange(serviceRoles);
            }

            return roles.Distinct().ToArray();
        }

        /// <summary>
        /// 获取用户角色
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>用户角色</returns>
        private string[] GetUserRoles(RpcMiddlewareContext context)
        {
            var roles = new List<string>();

            // 从用户声明获取角色
            if (context.User?.Claims != null)
            {
                var roleClaims = context.User.Claims.Where(c => c.Type == "role" || c.Type == "roles");
                roles.AddRange(roleClaims.Select(c => c.Value));
            }

            // 使用自定义角色提供程序
            if (_options.CustomRoleProvider != null)
            {
                try
                {
                    var customRoles = _options.CustomRoleProvider(context.GetUserId());
                    if (customRoles != null)
                    {
                        roles.AddRange(customRoles);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "自定义角色提供程序异常");
                }
            }

            return roles.Distinct().ToArray();
        }

        /// <summary>
        /// 检查用户是否有所需角色
        /// </summary>
        /// <param name="userRoles">用户角色</param>
        /// <param name="requiredRoles">所需角色</param>
        /// <returns>是否有所需角色</returns>
        private bool HasRequiredRole(string[] userRoles, string[] requiredRoles)
        {
            if (userRoles == null || userRoles.Length == 0)
            {
                return false;
            }

            if (requiredRoles == null || requiredRoles.Length == 0)
            {
                return true;
            }

            // 检查是否有超级管理员角色
            if (_options.SuperAdminRoles != null && _options.SuperAdminRoles.Any(role => userRoles.Contains(role)))
            {
                return true;
            }

            // 检查是否有任一所需角色
            switch (_options.RoleMatchMode)
            {
                case RoleMatchMode.Any:
                    return requiredRoles.Any(role => userRoles.Contains(role));
                case RoleMatchMode.All:
                    return requiredRoles.All(role => userRoles.Contains(role));
                default:
                    return requiredRoles.Any(role => userRoles.Contains(role));
            }
        }
    }

    /// <summary>
    /// 角色授权选项
    /// </summary>
    public class RoleAuthorizationOptions
    {
        /// <summary>
        /// 是否要求所有方法都进行授权
        /// </summary>
        public bool RequireAuthorizationForAllMethods { get; set; } = false;

        /// <summary>
        /// 方法角色映射
        /// </summary>
        public Dictionary<string, string[]> MethodRoles { get; set; } = new Dictionary<string, string[]>();

        /// <summary>
        /// 服务角色映射
        /// </summary>
        public Dictionary<string, string[]> ServiceRoles { get; set; } = new Dictionary<string, string[]>();

        /// <summary>
        /// 超级管理员角色
        /// </summary>
        public string[] SuperAdminRoles { get; set; } = new[] { "SuperAdmin", "Administrator" };

        /// <summary>
        /// 角色匹配模式
        /// </summary>
        public RoleMatchMode RoleMatchMode { get; set; } = RoleMatchMode.Any;

        /// <summary>
        /// 自定义角色提供程序
        /// </summary>
        public Func<string, string[]> CustomRoleProvider { get; set; }

        /// <summary>
        /// 添加方法角色
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="roles">角色</param>
        /// <returns>当前实例</returns>
        public RoleAuthorizationOptions AddMethodRoles(string methodName, params string[] roles)
        {
            MethodRoles[methodName] = roles;
            return this;
        }

        /// <summary>
        /// 添加服务角色
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="roles">角色</param>
        /// <returns>当前实例</returns>
        public RoleAuthorizationOptions AddServiceRoles(string serviceName, params string[] roles)
        {
            ServiceRoles[serviceName] = roles;
            return this;
        }

        /// <summary>
        /// 设置超级管理员角色
        /// </summary>
        /// <param name="roles">角色</param>
        /// <returns>当前实例</returns>
        public RoleAuthorizationOptions SetSuperAdminRoles(params string[] roles)
        {
            SuperAdminRoles = roles;
            return this;
        }

        /// <summary>
        /// 设置角色匹配模式
        /// </summary>
        /// <param name="mode">匹配模式</param>
        /// <returns>当前实例</returns>
        public RoleAuthorizationOptions SetRoleMatchMode(RoleMatchMode mode)
        {
            RoleMatchMode = mode;
            return this;
        }

        /// <summary>
        /// 设置自定义角色提供程序
        /// </summary>
        /// <param name="roleProvider">角色提供程序</param>
        /// <returns>当前实例</returns>
        public RoleAuthorizationOptions SetCustomRoleProvider(Func<string, string[]> roleProvider)
        {
            CustomRoleProvider = roleProvider;
            return this;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (SuperAdminRoles == null || SuperAdminRoles.Length == 0)
            {
                return (false, "SuperAdminRoles不能为空");
            }

            return (true, null);
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>配置实例</returns>
        public static RoleAuthorizationOptions CreateDefault()
        {
            return new RoleAuthorizationOptions
            {
                RequireAuthorizationForAllMethods = false,
                RoleMatchMode = RoleMatchMode.Any,
                SuperAdminRoles = new[] { "SuperAdmin", "Administrator" }
            };
        }
    }

    /// <summary>
    /// 角色匹配模式
    /// </summary>
    public enum RoleMatchMode
    {
        /// <summary>
        /// 任一角色匹配
        /// </summary>
        Any,

        /// <summary>
        /// 所有角色匹配
        /// </summary>
        All
    }

    /// <summary>
    /// 角色授权特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
    public class RpcRoleRequiredAttribute : Attribute
    {
        /// <summary>
        /// 所需角色
        /// </summary>
        public string[] RequiredRoles { get; }

        /// <summary>
        /// 角色匹配模式
        /// </summary>
        public RoleMatchMode MatchMode { get; set; } = RoleMatchMode.Any;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="roles">所需角色</param>
        public RpcRoleRequiredAttribute(params string[] roles)
        {
            RequiredRoles = roles ?? throw new ArgumentNullException(nameof(roles));
        }
    }
} 