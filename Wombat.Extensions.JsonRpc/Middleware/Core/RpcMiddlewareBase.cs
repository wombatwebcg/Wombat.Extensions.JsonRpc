using System;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Middleware.Core
{
    /// <summary>
    /// RPC中间件基类
    /// </summary>
    public abstract class RpcMiddlewareBase : IRpcMiddleware, IDisposable
    {
        /// <summary>
        /// 中间件名称
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 中间件描述
        /// </summary>
        public virtual string Description => string.Empty;

        /// <summary>
        /// 中间件顺序
        /// </summary>
        public virtual int Order => 0;

        /// <summary>
        /// 是否启用
        /// </summary>
        public virtual bool Enabled => true;

        /// <summary>
        /// 执行中间件
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        /// <returns>任务</returns>
        public abstract Task InvokeAsync(RpcMiddlewareContext context, Func<Task> next);

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            // 子类可以重写此方法以释放资源
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
} 