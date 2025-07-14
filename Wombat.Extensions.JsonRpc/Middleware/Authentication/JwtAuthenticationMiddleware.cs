using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Wombat.Extensions.JsonRpc.Middleware.Core;

namespace Wombat.Extensions.JsonRpc.Middleware.Authentication
{
    /// <summary>
    /// JWT认证中间件
    /// </summary>
    [RpcMiddleware("JWT Authentication", MiddlewareOrder.Security)]
    public class JwtAuthenticationMiddleware : RpcMiddlewareBase
    {
        private readonly JwtAuthenticationOptions _options;
        private readonly ILogger<JwtAuthenticationMiddleware> _logger;
        private readonly TokenValidationParameters _validationParameters;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">JWT认证选项</param>
        /// <param name="logger">日志记录器</param>
        public JwtAuthenticationMiddleware(JwtAuthenticationOptions options, ILogger<JwtAuthenticationMiddleware> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _validationParameters = CreateValidationParameters();
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

                // 提取JWT令牌
                var token = ExtractToken(context);
                if (string.IsNullOrEmpty(token))
                {
                    _logger?.LogWarning("JWT令牌缺失，请求: {RequestId}", context.RequestId);
                    throw new UnauthorizedAccessException("JWT令牌缺失");
                }

                // 验证JWT令牌
                var principal = ValidateToken(token);
                if (principal == null)
                {
                    _logger?.LogWarning("JWT令牌无效，请求: {RequestId}", context.RequestId);
                    throw new UnauthorizedAccessException("JWT令牌无效");
                }

                // 设置用户身份信息
                context.User = principal;
                context.AuthenticationScheme = "JWT";

                _logger?.LogDebug("JWT认证成功，用户: {User}, 请求: {RequestId}", 
                    principal.Identity.Name, context.RequestId);

                await next();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "JWT认证失败，请求: {RequestId}", context.RequestId);
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
        /// 提取JWT令牌
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>JWT令牌</returns>
        private string ExtractToken(RpcMiddlewareContext context)
        {
            // 从属性中提取令牌
            var token = context.GetProperty<string>("Authorization");
            if (!string.IsNullOrEmpty(token))
            {
                return ExtractTokenFromAuthorizationHeader(token);
            }

            // 从客户端属性中提取令牌
            if (context.ClientInfo?.Properties != null)
            {
                if (context.ClientInfo.Properties.TryGetValue("Authorization", out var authHeader))
                {
                    return ExtractTokenFromAuthorizationHeader(authHeader);
                }

                if (context.ClientInfo.Properties.TryGetValue("Token", out var tokenValue))
                {
                    return tokenValue;
                }
            }

            return null;
        }

        /// <summary>
        /// 从Authorization头中提取令牌
        /// </summary>
        /// <param name="authorizationHeader">Authorization头</param>
        /// <returns>JWT令牌</returns>
        private string ExtractTokenFromAuthorizationHeader(string authorizationHeader)
        {
            if (string.IsNullOrEmpty(authorizationHeader))
            {
                return null;
            }

            const string bearer = "Bearer ";
            if (authorizationHeader.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return authorizationHeader.Substring(bearer.Length).Trim();
            }

            return authorizationHeader;
        }

        /// <summary>
        /// 验证JWT令牌
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <returns>用户身份信息</returns>
        private ClaimsPrincipal ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, _validationParameters, out var validatedToken);

                // 验证令牌类型
                if (!(validatedToken is JwtSecurityToken jwtToken) || 
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "JWT令牌验证失败");
                return null;
            }
        }

        /// <summary>
        /// 创建令牌验证参数
        /// </summary>
        /// <returns>令牌验证参数</returns>
        private TokenValidationParameters CreateValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
                ValidateIssuer = !string.IsNullOrEmpty(_options.Issuer),
                ValidIssuer = _options.Issuer,
                ValidateAudience = !string.IsNullOrEmpty(_options.Audience),
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(_options.ClockSkewMinutes),
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }
    }

    /// <summary>
    /// JWT认证选项
    /// </summary>
    public class JwtAuthenticationOptions
    {
        /// <summary>
        /// JWT密钥
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// 发行者
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// 受众
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// 时钟偏差（分钟）
        /// </summary>
        public int ClockSkewMinutes { get; set; } = 5;

        /// <summary>
        /// 是否要求所有方法都进行认证
        /// </summary>
        public bool RequireAuthenticationForAllMethods { get; set; } = false;

        /// <summary>
        /// 自定义令牌验证器
        /// </summary>
        public Func<string, ClaimsPrincipal> CustomTokenValidator { get; set; }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (string.IsNullOrEmpty(SecretKey))
            {
                return (false, "SecretKey不能为空");
            }

            if (SecretKey.Length < 32)
            {
                return (false, "SecretKey长度不能小于32个字符");
            }

            return (true, null);
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <param name="secretKey">密钥</param>
        /// <param name="issuer">发行者</param>
        /// <param name="audience">受众</param>
        /// <returns>配置实例</returns>
        public static JwtAuthenticationOptions CreateDefault(string secretKey, string issuer = null, string audience = null)
        {
            return new JwtAuthenticationOptions
            {
                SecretKey = secretKey,
                Issuer = issuer,
                Audience = audience,
                ClockSkewMinutes = 5,
                RequireAuthenticationForAllMethods = false
            };
        }
    }

    /// <summary>
    /// JWT令牌生成器
    /// </summary>
    public class JwtTokenGenerator
    {
        private readonly JwtAuthenticationOptions _options;
        private readonly ILogger<JwtTokenGenerator> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">JWT认证选项</param>
        /// <param name="logger">日志记录器</param>
        public JwtTokenGenerator(JwtAuthenticationOptions options, ILogger<JwtTokenGenerator> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// 生成JWT令牌
        /// </summary>
        /// <param name="claims">声明</param>
        /// <param name="expiration">过期时间</param>
        /// <returns>JWT令牌</returns>
        public string GenerateToken(ClaimsIdentity claims, DateTime? expiration = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_options.SecretKey);
            var expires = expiration ?? DateTime.UtcNow.AddHours(1);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = claims,
                Expires = expires,
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// 生成用户令牌
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="userName">用户名</param>
        /// <param name="roles">角色</param>
        /// <param name="expiration">过期时间</param>
        /// <returns>JWT令牌</returns>
        public string GenerateUserToken(string userId, string userName, string[] roles = null, DateTime? expiration = null)
        {
            var claims = new ClaimsIdentity();
            claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            claims.AddClaim(new Claim(ClaimTypes.Name, userName));

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    claims.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            return GenerateToken(claims, expiration);
        }
    }
} 