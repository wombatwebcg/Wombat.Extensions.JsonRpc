using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Core.Contracts;
using Wombat.Extensions.JsonRpc.Core.Validation;

namespace Wombat.Extensions.JsonRpc.Core.Builder
{
    /// <summary>
    /// RPC目标构建器工厂
    /// </summary>
    public class RpcTargetBuilderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RpcTargetBuilderFactory> _logger;

        public RpcTargetBuilderFactory(IServiceProvider serviceProvider = null, ILogger<RpcTargetBuilderFactory> logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// 创建标准的RPC目标构建器
        /// </summary>
        /// <returns>RPC目标构建器</returns>
        public RpcTargetBuilder CreateStandard()
        {
            var metadataProvider = _serviceProvider?.GetService<IRpcMetadataProvider>() ?? new DefaultRpcMetadataProvider();
            var parameterValidator = _serviceProvider?.GetService<ParameterValidator>() ?? new ParameterValidator();
            var methodInterceptor = _serviceProvider?.GetService<RpcMethodInterceptor>() ?? new RpcMethodInterceptor(parameterValidator);
            var builderLogger = _serviceProvider?.GetService<ILogger<RpcTargetBuilder>>();

            return new RpcTargetBuilder(builderLogger, metadataProvider, methodInterceptor, _serviceProvider);
        }

        /// <summary>
        /// 创建带有自定义配置的RPC目标构建器
        /// </summary>
        /// <param name="configuration">配置选项</param>
        /// <returns>RPC目标构建器</returns>
        public RpcTargetBuilder CreateWithConfiguration(RpcTargetBuilderConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var metadataProvider = configuration.MetadataProvider ?? 
                                   _serviceProvider?.GetService<IRpcMetadataProvider>() ?? 
                                   new DefaultRpcMetadataProvider();

            RpcMethodInterceptor methodInterceptor = null;
            if (configuration.EnableParameterValidation)
            {
                var parameterValidator = configuration.ParameterValidator ?? 
                                         _serviceProvider?.GetService<ParameterValidator>() ?? 
                                         new ParameterValidator();
                
                methodInterceptor = configuration.MethodInterceptor ?? 
                                    _serviceProvider?.GetService<RpcMethodInterceptor>() ?? 
                                    new RpcMethodInterceptor(parameterValidator);
            }

            var builderLogger = _serviceProvider?.GetService<ILogger<RpcTargetBuilder>>();

            return new RpcTargetBuilder(builderLogger, metadataProvider, methodInterceptor, _serviceProvider);
        }

        /// <summary>
        /// 创建最小化的RPC目标构建器（无验证）
        /// </summary>
        /// <returns>RPC目标构建器</returns>
        public RpcTargetBuilder CreateMinimal()
        {
            var metadataProvider = new DefaultRpcMetadataProvider();
            var builderLogger = _serviceProvider?.GetService<ILogger<RpcTargetBuilder>>();

            return new RpcTargetBuilder(builderLogger, metadataProvider, null, _serviceProvider);
        }

        /// <summary>
        /// 创建高性能的RPC目标构建器
        /// </summary>
        /// <returns>RPC目标构建器</returns>
        public RpcTargetBuilder CreateHighPerformance()
        {
            var configuration = new RpcTargetBuilderConfiguration
            {
                EnableParameterValidation = false, // 禁用参数验证以获得最高性能
                MetadataProvider = new DefaultRpcMetadataProvider()
            };

            return CreateWithConfiguration(configuration);
        }
    }

    /// <summary>
    /// RPC目标构建器配置
    /// </summary>
    public class RpcTargetBuilderConfiguration
    {
        /// <summary>
        /// 是否启用参数验证
        /// </summary>
        public bool EnableParameterValidation { get; set; } = true;

        /// <summary>
        /// 元数据提供程序
        /// </summary>
        public IRpcMetadataProvider MetadataProvider { get; set; }

        /// <summary>
        /// 参数验证器
        /// </summary>
        public ParameterValidator ParameterValidator { get; set; }

        /// <summary>
        /// 方法拦截器
        /// </summary>
        public RpcMethodInterceptor MethodInterceptor { get; set; }

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// 缓存过期时间（分钟）
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        public static RpcTargetBuilderConfiguration CreateDefault()
        {
            return new RpcTargetBuilderConfiguration
            {
                EnableParameterValidation = true,
                EnableCaching = true,
                CacheExpirationMinutes = 60,
                EnableVerboseLogging = false
            };
        }

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        /// <returns>高性能配置</returns>
        public static RpcTargetBuilderConfiguration CreateHighPerformance()
        {
            return new RpcTargetBuilderConfiguration
            {
                EnableParameterValidation = false,
                EnableCaching = true,
                CacheExpirationMinutes = 120,
                EnableVerboseLogging = false
            };
        }

        /// <summary>
        /// 创建调试配置
        /// </summary>
        /// <returns>调试配置</returns>
        public static RpcTargetBuilderConfiguration CreateDebug()
        {
            return new RpcTargetBuilderConfiguration
            {
                EnableParameterValidation = true,
                EnableCaching = false,
                CacheExpirationMinutes = 5,
                EnableVerboseLogging = true
            };
        }
    }
} 