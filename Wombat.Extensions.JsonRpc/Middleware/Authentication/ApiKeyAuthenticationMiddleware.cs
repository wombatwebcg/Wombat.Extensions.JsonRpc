using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Middleware.Core;

namespace Wombat.Extensions.JsonRpc.Middleware.Authentication
{
    /// <summary>
    /// API Key认证中间件
    /// </summary>
    [RpcMiddleware("API Key Authentication", MiddlewareOrder.Security)]
    public class ApiKeyAuthenticationMiddleware : RpcMiddlewareBase
    {
        private readonly ApiKeyAuthenticationOptions _options;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">API Key认证选项</param>
        /// <param name="logger">日志记录器</param>
        public ApiKeyAuthenticationMiddleware(ApiKeyAuthenticationOptions options, ILogger<ApiKeyAuthenticationMiddleware> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// 处理认证
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        /// <returns>任务</returns>
        public override async Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next)
        {
            try
            {
                // 检查是否需要认证
                if (!RequiresAuthentication(context))
                {
                    await next();
                    return;
                }

                // 提取API Key
                var apiKey = ExtractApiKey(context);
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger?.LogWarning("API Key缺失，请求: {RequestId}", context.RequestId);
                    throw new UnauthorizedAccessException("API Key缺失");
                }

                // 验证API Key
                var keyInfo = ValidateApiKey(apiKey);
                if (keyInfo == null)
                {
                    _logger?.LogWarning("API Key无效，请求: {RequestId}", context.RequestId);
                    throw new UnauthorizedAccessException("API Key无效");
                }

                // 设置用户身份信息
                context.User = CreatePrincipal(keyInfo);
                context.AuthenticationScheme = "ApiKey";

                _logger?.LogDebug("API Key认证成功，客户端: {ClientId}, 请求: {RequestId}", 
                    keyInfo.ClientId, context.RequestId);

                await next();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "API Key认证失败，请求: {RequestId}", context.RequestId);
                context.Exception = ex;
                context.ExceptionHandled = false;
                throw;
            }
        }

        /// <summary>
        /// 检查是否需要认证
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>是否需要认证</returns>
        private bool RequiresAuthentication(RpcMiddlewareContext context)
        {
            // 检查全局认证要求
            if (_options.RequireAuthenticationForAllMethods)
            {
                return true;
            }

            // 检查方法级别的认证要求
            if (context.MethodMetadata?.RequireAuthentication == true)
            {
                return true;
            }

            // 检查服务级别的认证要求
            if (context.ServiceMetadata?.RequireAuthentication == true)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 提取API Key
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>API Key</returns>
        private string ExtractApiKey(RpcMiddlewareContext context)
        {
            // 从属性中提取API Key
            var apiKey = context.GetProperty<string>(_options.HeaderName);
            if (!string.IsNullOrEmpty(apiKey))
            {
                return apiKey;
            }

            // 从客户端属性中提取API Key
            if (context.ClientInfo?.Properties != null)
            {
                if (context.ClientInfo.Properties.TryGetValue(_options.HeaderName, out var headerValue))
                {
                    return headerValue;
                }

                if (context.ClientInfo.Properties.TryGetValue("ApiKey", out var keyValue))
                {
                    return keyValue;
                }

                if (context.ClientInfo.Properties.TryGetValue("X-API-KEY", out var xApiKeyValue))
                {
                    return xApiKeyValue;
                }
            }

            return null;
        }

        /// <summary>
        /// 验证API Key
        /// </summary>
        /// <param name="apiKey">API Key</param>
        /// <returns>Key信息</returns>
        private ApiKeyInfo ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return null;
            }

            // 使用自定义验证器
            if (_options.CustomValidator != null)
            {
                try
                {
                    return _options.CustomValidator(apiKey);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "自定义API Key验证器异常");
                    return null;
                }
            }

            // 使用配置的API Key列表
            if (_options.ValidApiKeys.TryGetValue(apiKey, out var keyInfo))
            {
                // 检查是否过期
                if (keyInfo.ExpirationTime.HasValue && keyInfo.ExpirationTime.Value < DateTime.UtcNow)
                {
                    _logger?.LogWarning("API Key已过期: {ClientId}", keyInfo.ClientId);
                    return null;
                }

                // 检查是否启用
                if (!keyInfo.IsEnabled)
                {
                    _logger?.LogWarning("API Key已禁用: {ClientId}", keyInfo.ClientId);
                    return null;
                }

                return keyInfo;
            }

            return null;
        }

        /// <summary>
        /// 创建用户身份信息
        /// </summary>
        /// <param name="keyInfo">API Key信息</param>
        /// <returns>用户身份信息</returns>
        private ClaimsPrincipal CreatePrincipal(ApiKeyInfo keyInfo)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, keyInfo.ClientId),
                new Claim(ClaimTypes.Name, keyInfo.ClientName),
                new Claim("ApiKey", keyInfo.ApiKey),
                new Claim("ClientId", keyInfo.ClientId)
            };

            // 添加角色声明
            if (keyInfo.Roles != null)
            {
                foreach (var role in keyInfo.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // 添加权限声明
            if (keyInfo.Permissions != null)
            {
                foreach (var permission in keyInfo.Permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }
            }

            // 添加自定义声明
            if (keyInfo.CustomClaims != null)
            {
                foreach (var customClaim in keyInfo.CustomClaims)
                {
                    claims.Add(new Claim(customClaim.Key, customClaim.Value));
                }
            }

            var identity = new ClaimsIdentity(claims, "ApiKey");
            return new ClaimsPrincipal(identity);
        }
    }

    /// <summary>
    /// API Key认证选项
    /// </summary>
    public class ApiKeyAuthenticationOptions
    {
        /// <summary>
        /// API Key头名称
        /// </summary>
        public string HeaderName { get; set; } = "X-API-Key";

        /// <summary>
        /// 有效的API Key集合
        /// </summary>
        public Dictionary<string, ApiKeyInfo> ValidApiKeys { get; set; } = new Dictionary<string, ApiKeyInfo>();

        /// <summary>
        /// 是否要求所有方法都进行认证
        /// </summary>
        public bool RequireAuthenticationForAllMethods { get; set; } = false;

        /// <summary>
        /// 自定义API Key验证器
        /// </summary>
        public Func<string, ApiKeyInfo> CustomValidator { get; set; }

        /// <summary>
        /// 添加API Key
        /// </summary>
        /// <param name="apiKey">API Key</param>
        /// <param name="clientId">客户端ID</param>
        /// <param name="clientName">客户端名称</param>
        /// <param name="roles">角色</param>
        /// <param name="permissions">权限</param>
        /// <param name="expirationTime">过期时间</param>
        /// <returns>当前实例</returns>
        public ApiKeyAuthenticationOptions AddApiKey(string apiKey, string clientId, string clientName, 
            string[] roles = null, string[] permissions = null, DateTime? expirationTime = null)
        {
            var keyInfo = new ApiKeyInfo
            {
                ApiKey = apiKey,
                ClientId = clientId,
                ClientName = clientName,
                Roles = roles,
                Permissions = permissions,
                ExpirationTime = expirationTime,
                IsEnabled = true,
                CreatedTime = DateTime.UtcNow
            };

            ValidApiKeys[apiKey] = keyInfo;
            return this;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (string.IsNullOrEmpty(HeaderName))
            {
                return (false, "HeaderName不能为空");
            }

            if (ValidApiKeys.Count == 0 && CustomValidator == null)
            {
                return (false, "必须配置ValidApiKeys或CustomValidator");
            }

            return (true, null);
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <param name="headerName">头名称</param>
        /// <returns>配置实例</returns>
        public static ApiKeyAuthenticationOptions CreateDefault(string headerName = "X-API-Key")
        {
            return new ApiKeyAuthenticationOptions
            {
                HeaderName = headerName,
                RequireAuthenticationForAllMethods = false
            };
        }
    }

    /// <summary>
    /// API Key信息
    /// </summary>
    public class ApiKeyInfo
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 客户端名称
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// 角色
        /// </summary>
        public string[] Roles { get; set; }

        /// <summary>
        /// 权限
        /// </summary>
        public string[] Permissions { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime? ExpirationTime { get; set; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime? LastUsedTime { get; set; }

        /// <summary>
        /// 使用次数
        /// </summary>
        public long UsageCount { get; set; }

        /// <summary>
        /// 自定义声明
        /// </summary>
        public Dictionary<string, string> CustomClaims { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 添加自定义声明
        /// </summary>
        /// <param name="key">声明键</param>
        /// <param name="value">声明值</param>
        /// <returns>当前实例</returns>
        public ApiKeyInfo AddClaim(string key, string value)
        {
            CustomClaims[key] = value;
            return this;
        }
    }

    /// <summary>
    /// API Key生成器
    /// </summary>
    public static class ApiKeyGenerator
    {
        /// <summary>
        /// 生成随机API Key
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns>API Key</returns>
        public static string GenerateApiKey(int length = 32)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        /// <summary>
        /// 生成带前缀的API Key
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <param name="length">长度</param>
        /// <returns>API Key</returns>
        public static string GenerateApiKeyWithPrefix(string prefix, int length = 32)
        {
            return $"{prefix}_{GenerateApiKey(length)}";
        }

        /// <summary>
        /// 生成GUID格式的API Key
        /// </summary>
        /// <returns>API Key</returns>
        public static string GenerateGuidApiKey()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
} 