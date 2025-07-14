using System;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Middleware.Core
{
    /// <summary>
    /// RPC中间件接口
    /// </summary>
    public interface IRpcMiddleware
    {
        /// <summary>
        /// 中间件名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 中间件描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 中间件顺序
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// 执行中间件
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        /// <returns>任务</returns>
        Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next);
    }



    /// <summary>
    /// 中间件执行顺序
    /// </summary>
    public enum MiddlewareOrder
    {
        /// <summary>
        /// 安全相关中间件（认证、授权）
        /// </summary>
        Security = 100,

        /// <summary>
        /// 请求限流中间件
        /// </summary>
        RateLimiting = 200,

        /// <summary>
        /// 缓存中间件
        /// </summary>
        Caching = 300,

        /// <summary>
        /// 日志和监控中间件
        /// </summary>
        Monitoring = 400,

        /// <summary>
        /// 参数验证中间件
        /// </summary>
        Validation = 500,

        /// <summary>
        /// 业务逻辑中间件
        /// </summary>
        Business = 1000
    }

    /// <summary>
    /// 中间件描述特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcMiddlewareAttribute : Attribute
    {
        /// <summary>
        /// 中间件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 中间件描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 执行顺序
        /// </summary>
        public MiddlewareOrder Order { get; set; }

        /// <summary>
        /// 是否默认启用
        /// </summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">中间件名称</param>
        /// <param name="order">执行顺序</param>
        public RpcMiddlewareAttribute(string name, MiddlewareOrder order = MiddlewareOrder.Business)
        {
            Name = name;
            Order = order;
        }
    }
} 