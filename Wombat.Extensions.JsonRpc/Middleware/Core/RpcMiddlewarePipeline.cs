using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Middleware.Core
{
    /// <summary>
    /// RPC中间件管道
    /// </summary>
    public class RpcMiddlewarePipeline
    {
        private readonly List<MiddlewareRegistration> _middlewares;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RpcMiddlewarePipeline> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="logger">日志记录器</param>
        public RpcMiddlewarePipeline(IServiceProvider serviceProvider, ILogger<RpcMiddlewarePipeline> logger = null)
        {
            _middlewares = new List<MiddlewareRegistration>();
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <typeparam name="T">中间件类型</typeparam>
        /// <param name="order">执行顺序</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Use<T>(MiddlewareOrder order = MiddlewareOrder.Business, bool enabled = true)
            where T : class, IRpcMiddleware
        {
            var middlewareType = typeof(T);
            var attribute = middlewareType.GetCustomAttributes(typeof(RpcMiddlewareAttribute), false)
                .FirstOrDefault() as RpcMiddlewareAttribute;

            var registration = new MiddlewareRegistration
            {
                MiddlewareType = middlewareType,
                Order = attribute?.Order ?? order,
                Enabled = enabled && (attribute?.EnabledByDefault ?? true),
                Name = attribute?.Name ?? middlewareType.Name,
                Description = attribute?.Description ?? string.Empty
            };

            _middlewares.Add(registration);
            _logger?.LogDebug("添加中间件: {Name} (Order: {Order})", registration.Name, registration.Order);

            return this;
        }

        /// <summary>
        /// 添加中间件实例
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        /// <param name="order">执行顺序</param>
        /// <param name="enabled">是否启用</param>
        /// <param name="name">中间件名称</param>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Use(IRpcMiddleware middleware, MiddlewareOrder order = MiddlewareOrder.Business, bool enabled = true, string name = null)
        {
            var registration = new MiddlewareRegistration
            {
                MiddlewareType = middleware.GetType(),
                MiddlewareInstance = middleware,
                Order = order,
                Enabled = enabled,
                Name = name ?? middleware.GetType().Name,
                Description = string.Empty
            };

            _middlewares.Add(registration);
            _logger?.LogDebug("添加中间件实例: {Name} (Order: {Order})", registration.Name, registration.Order);

            return this;
        }

        /// <summary>
        /// 启用中间件
        /// </summary>
        /// <typeparam name="T">中间件类型</typeparam>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Enable<T>() where T : class, IRpcMiddleware
        {
            var middlewareType = typeof(T);
            var registration = _middlewares.FirstOrDefault(m => m.MiddlewareType == middlewareType);
            if (registration != null)
            {
                registration.Enabled = true;
                _logger?.LogDebug("启用中间件: {Name}", registration.Name);
            }

            return this;
        }

        /// <summary>
        /// 禁用中间件
        /// </summary>
        /// <typeparam name="T">中间件类型</typeparam>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Disable<T>() where T : class, IRpcMiddleware
        {
            var middlewareType = typeof(T);
            var registration = _middlewares.FirstOrDefault(m => m.MiddlewareType == middlewareType);
            if (registration != null)
            {
                registration.Enabled = false;
                _logger?.LogDebug("禁用中间件: {Name}", registration.Name);
            }

            return this;
        }

        /// <summary>
        /// 移除中间件
        /// </summary>
        /// <typeparam name="T">中间件类型</typeparam>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Remove<T>() where T : class, IRpcMiddleware
        {
            var middlewareType = typeof(T);
            var registration = _middlewares.FirstOrDefault(m => m.MiddlewareType == middlewareType);
            if (registration != null)
            {
                _middlewares.Remove(registration);
                _logger?.LogDebug("移除中间件: {Name}", registration.Name);
            }

            return this;
        }

        /// <summary>
        /// 清空所有中间件
        /// </summary>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Clear()
        {
            _middlewares.Clear();
            _logger?.LogDebug("清空所有中间件");
            return this;
        }

        /// <summary>
        /// 执行中间件管道
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <returns>执行结果</returns>
        public async Task ExecuteAsync(RpcMiddlewareContext context)
        {
            var enabledMiddlewares = _middlewares
                .Where(m => m.Enabled)
                .OrderBy(m => m.Order)
                .ToList();

            _logger?.LogDebug("开始执行中间件管道，共 {Count} 个中间件", enabledMiddlewares.Count);

            await ExecuteMiddlewareChain(enabledMiddlewares, 0, context);

            _logger?.LogDebug("中间件管道执行完成");
        }

        /// <summary>
        /// 递归执行中间件链
        /// </summary>
        /// <param name="middlewares">中间件列表</param>
        /// <param name="index">当前索引</param>
        /// <param name="context">中间件上下文</param>
        /// <returns>执行结果</returns>
        private async Task ExecuteMiddlewareChain(List<MiddlewareRegistration> middlewares, int index, RpcMiddlewareContext context)
        {
            if (index >= middlewares.Count)
            {
                // 已执行完所有中间件
                return;
            }

            var registration = middlewares[index];
            var middleware = GetMiddlewareInstance(registration);

            if (middleware == null)
            {
                _logger?.LogWarning("无法创建中间件实例: {Name}", registration.Name);
                await ExecuteMiddlewareChain(middlewares, index + 1, context);
                return;
            }

            try
            {
                _logger?.LogDebug("执行中间件: {Name}", registration.Name);

                await middleware.InvokeAsync(context, async () =>
                {
                    await ExecuteMiddlewareChain(middlewares, index + 1, context);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "中间件执行异常: {Name}", registration.Name);
                context.Exception = ex;
                throw;
            }
        }

        /// <summary>
        /// 获取中间件实例
        /// </summary>
        /// <param name="registration">中间件注册信息</param>
        /// <returns>中间件实例</returns>
        private IRpcMiddleware GetMiddlewareInstance(MiddlewareRegistration registration)
        {
            if (registration.MiddlewareInstance != null)
            {
                return registration.MiddlewareInstance;
            }

            try
            {
                return _serviceProvider.GetService(registration.MiddlewareType) as IRpcMiddleware ??
                       Activator.CreateInstance(registration.MiddlewareType) as IRpcMiddleware;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建中间件实例失败: {Type}", registration.MiddlewareType.Name);
                return null;
            }
        }

        /// <summary>
        /// 获取所有中间件信息
        /// </summary>
        /// <returns>中间件信息列表</returns>
        public List<MiddlewareInfo> GetMiddlewareInfo()
        {
            return _middlewares.Select(m => new MiddlewareInfo
            {
                Name = m.Name,
                Type = m.MiddlewareType.Name,
                Order = m.Order,
                Enabled = m.Enabled,
                Description = m.Description
            }).ToList();
        }

        /// <summary>
        /// 构建中间件管道
        /// </summary>
        /// <returns>管道构建器</returns>
        public static RpcMiddlewarePipelineBuilder Create()
        {
            return new RpcMiddlewarePipelineBuilder();
        }
    }

    /// <summary>
    /// 中间件注册信息
    /// </summary>
    internal class MiddlewareRegistration
    {
        /// <summary>
        /// 中间件类型
        /// </summary>
        public Type MiddlewareType { get; set; }

        /// <summary>
        /// 中间件实例
        /// </summary>
        public IRpcMiddleware MiddlewareInstance { get; set; }

        /// <summary>
        /// 执行顺序
        /// </summary>
        public MiddlewareOrder Order { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 中间件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 中间件描述
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 中间件信息
    /// </summary>
    public class MiddlewareInfo
    {
        /// <summary>
        /// 中间件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 中间件类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 执行顺序
        /// </summary>
        public MiddlewareOrder Order { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 中间件描述
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 中间件管道构建器
    /// </summary>
    public class RpcMiddlewarePipelineBuilder
    {
        private readonly List<MiddlewareRegistration> _middlewares = new List<MiddlewareRegistration>();

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <typeparam name="T">中间件类型</typeparam>
        /// <param name="order">执行顺序</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>构建器实例</returns>
        public RpcMiddlewarePipelineBuilder Use<T>(MiddlewareOrder order = MiddlewareOrder.Business, bool enabled = true)
            where T : class, IRpcMiddleware
        {
            var middlewareType = typeof(T);
            var attribute = middlewareType.GetCustomAttributes(typeof(RpcMiddlewareAttribute), false)
                .FirstOrDefault() as RpcMiddlewareAttribute;

            var registration = new MiddlewareRegistration
            {
                MiddlewareType = middlewareType,
                Order = attribute?.Order ?? order,
                Enabled = enabled && (attribute?.EnabledByDefault ?? true),
                Name = attribute?.Name ?? middlewareType.Name,
                Description = attribute?.Description ?? string.Empty
            };

            _middlewares.Add(registration);
            return this;
        }

        /// <summary>
        /// 构建管道
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>管道实例</returns>
        public RpcMiddlewarePipeline Build(IServiceProvider serviceProvider, ILogger<RpcMiddlewarePipeline> logger = null)
        {
            var pipeline = new RpcMiddlewarePipeline(serviceProvider, logger);
            
            foreach (var registration in _middlewares)
            {
                pipeline.Use(registration.MiddlewareInstance ?? 
                    (IRpcMiddleware)Activator.CreateInstance(registration.MiddlewareType), 
                    registration.Order, 
                    registration.Enabled, 
                    registration.Name);
            }

            return pipeline;
        }
    }
} 