using System;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Middleware.Core
{
    /// <summary>
    /// RPC中间件委托
    /// </summary>
    /// <param name="context">中间件上下文</param>
    /// <param name="next">下一个中间件</param>
    /// <returns>任务</returns>
    public delegate Task RpcMiddlewareDelegate(RpcMiddlewareContext context, Func<Task> next);
} 