using System;
using System.Threading.Tasks;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.Client.Proxy
{
    /// <summary>
    /// RPC代理工厂接口
    /// 负责创建强类型的RPC客户端代理
    /// </summary>
    public interface IRpcProxyFactory
    {
        /// <summary>
        /// 创建强类型RPC代理
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>强类型代理实例</returns>
        T CreateProxy<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class;

        /// <summary>
        /// 异步创建强类型RPC代理
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>强类型代理实例</returns>
        Task<T> CreateProxyAsync<T>(IRpcClient client, ProxyGenerationOptions options = null) where T : class;

        /// <summary>
        /// 创建代理实例（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <param name="client">RPC客户端实例</param>
        /// <param name="options">代理生成选项</param>
        /// <returns>代理实例</returns>
        object CreateProxy(Type serviceType, IRpcClient client, ProxyGenerationOptions options = null);

        /// <summary>
        /// 检查类型是否可以创建代理
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>是否可以创建代理</returns>
        bool CanCreateProxy(Type serviceType);

        /// <summary>
        /// 获取服务的元数据信息
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <returns>服务元数据</returns>
        Task<ServiceMetadata> GetServiceMetadataAsync<T>() where T : class;

        /// <summary>
        /// 获取服务的元数据信息（非泛型版本）
        /// </summary>
        /// <param name="serviceType">服务接口类型</param>
        /// <returns>服务元数据</returns>
        Task<ServiceMetadata> GetServiceMetadataAsync(Type serviceType);
    }

    /// <summary>
    /// 基础RPC客户端接口
    /// </summary>
    public interface IRpcClient : IDisposable
    {
        /// <summary>
        /// 异步调用RPC方法
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="methodName">方法名</param>
        /// <param name="args">参数</param>
        /// <returns>调用结果</returns>
        Task<TResult> InvokeAsync<TResult>(string methodName, params object[] args);

        /// <summary>
        /// 异步调用RPC方法（无返回值）
        /// </summary>
        /// <param name="methodName">方法名</param>
        /// <param name="args">参数</param>
        /// <returns>任务</returns>
        Task InvokeAsync(string methodName, params object[] args);

        /// <summary>
        /// 发送通知（不等待响应）
        /// </summary>
        /// <param name="methodName">方法名</param>
        /// <param name="args">参数</param>
        /// <returns>任务</returns>
        Task NotifyAsync(string methodName, params object[] args);

        /// <summary>
        /// 检查连接状态
        /// </summary>
        /// <returns>是否已连接</returns>
        bool IsConnected { get; }

        /// <summary>
        /// 连接到服务端
        /// </summary>
        /// <returns>连接任务</returns>
        Task ConnectAsync();

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开任务</returns>
        Task DisconnectAsync();
    }
} 