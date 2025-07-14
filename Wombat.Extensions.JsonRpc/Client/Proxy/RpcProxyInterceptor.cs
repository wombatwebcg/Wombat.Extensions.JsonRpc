using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Contracts;
using Wombat.Extensions.JsonRpc.Validation;

namespace Wombat.Extensions.JsonRpc.Client.Proxy
{
    /// <summary>
    /// RPC代理拦截器
    /// 负责拦截代理方法调用并转发到RPC客户端
    /// </summary>
    public class RpcProxyInterceptor : IInterceptor
    {
        private readonly IRpcClient _rpcClient;
        private readonly ProxyGenerationOptions _options;
        private readonly ILogger<RpcProxyInterceptor> _logger;
        private readonly ParameterValidator _parameterValidator;
        private readonly ServiceMetadata _serviceMetadata;

        public RpcProxyInterceptor(
            IRpcClient rpcClient,
            ProxyGenerationOptions options,
            ServiceMetadata serviceMetadata,
            ILogger<RpcProxyInterceptor> logger = null)
        {
            _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _serviceMetadata = serviceMetadata ?? throw new ArgumentNullException(nameof(serviceMetadata));
            _logger = logger;
            _parameterValidator = new ParameterValidator();
        }

        /// <summary>
        /// 拦截方法调用
        /// </summary>
        /// <param name="invocation">方法调用上下文</param>
        public void Intercept(IInvocation invocation)
        {
            try
            {
                // 处理异步方法
                if (IsAsyncMethod(invocation.Method))
                {
                    InterceptAsyncMethod(invocation);
                }
                else
                {
                    InterceptSyncMethod(invocation);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RPC代理调用失败: {Method}", invocation.Method.Name);
                
                if (_options.EnableExceptionWrapping)
                {
                    throw new RpcProxyException($"RPC调用失败: {invocation.Method.Name}", ex);
                }
                throw;
            }
        }

        /// <summary>
        /// 拦截异步方法调用
        /// </summary>
        /// <param name="invocation">方法调用上下文</param>
        private void InterceptAsyncMethod(IInvocation invocation)
        {
            var method = invocation.Method;
            var returnType = method.ReturnType;

            // 获取方法元数据
            var methodMetadata = GetMethodMetadata(method);
            if (methodMetadata == null)
            {
                throw new InvalidOperationException($"方法 {method.Name} 没有找到对应的RPC元数据");
            }

            // 参数验证
            if (_options.EnableParameterValidation)
            {
                ValidateParameters(method, invocation.Arguments);
            }

            // 记录调用开始
            var startTime = DateTime.UtcNow;
            _logger?.LogDebug("开始RPC调用: {Method}", methodMetadata.MethodName);

            // 执行RPC调用
            if (returnType == typeof(Task))
            {
                // Task 返回类型（无返回值）
                invocation.ReturnValue = InvokeRpcMethodAsync(methodMetadata, invocation.Arguments);
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Task<T> 返回类型
                var resultType = returnType.GetGenericArguments()[0];
                var method_info = typeof(RpcProxyInterceptor)
                    .GetMethod(nameof(InvokeRpcMethodAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(resultType);
                
                invocation.ReturnValue = method_info.Invoke(this, new object[] { methodMetadata, invocation.Arguments });
            }
            else
            {
                throw new InvalidOperationException($"不支持的异步返回类型: {returnType}");
            }

            // 记录调用结束
            if (_options.EnablePerformanceMonitoring)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger?.LogInformation("RPC调用完成: {Method}, 耗时: {Duration}ms", 
                    methodMetadata.MethodName, duration.TotalMilliseconds);
            }
        }

        /// <summary>
        /// 拦截同步方法调用
        /// </summary>
        /// <param name="invocation">方法调用上下文</param>
        private void InterceptSyncMethod(IInvocation invocation)
        {
            var method = invocation.Method;
            var methodMetadata = GetMethodMetadata(method);
            
            if (methodMetadata == null)
            {
                throw new InvalidOperationException($"方法 {method.Name} 没有找到对应的RPC元数据");
            }

            // 参数验证
            if (_options.EnableParameterValidation)
            {
                ValidateParameters(method, invocation.Arguments);
            }

            // 同步调用转异步调用
            var task = InvokeRpcMethodAsync<object>(methodMetadata, invocation.Arguments);
            invocation.ReturnValue = task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 执行RPC方法调用（无返回值）
        /// </summary>
        /// <param name="methodMetadata">方法元数据</param>
        /// <param name="arguments">参数</param>
        /// <returns>任务</returns>
        private async Task InvokeRpcMethodAsync(MethodMetadata methodMetadata, object[] arguments)
        {
            var methodName = GetRpcMethodName(methodMetadata);
            
            if (methodMetadata.IsNotification)
            {
                await _rpcClient.NotifyAsync(methodName, arguments);
            }
            else
            {
                await _rpcClient.InvokeAsync(methodName, arguments);
            }
        }

        /// <summary>
        /// 执行RPC方法调用（有返回值）
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="methodMetadata">方法元数据</param>
        /// <param name="arguments">参数</param>
        /// <returns>调用结果</returns>
        private async Task<T> InvokeRpcMethodAsync<T>(MethodMetadata methodMetadata, object[] arguments)
        {
            var methodName = GetRpcMethodName(methodMetadata);
            
            if (methodMetadata.IsNotification)
            {
                await _rpcClient.NotifyAsync(methodName, arguments);
                return default;
            }
            else
            {
                return await _rpcClient.InvokeAsync<T>(methodName, arguments);
            }
        }

        /// <summary>
        /// 获取方法元数据
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>方法元数据</returns>
        private MethodMetadata GetMethodMetadata(MethodInfo method)
        {
            return _serviceMetadata.Methods?.FirstOrDefault(m => m.MethodName == method.Name);
        }

        /// <summary>
        /// 获取RPC方法名称
        /// </summary>
        /// <param name="methodMetadata">方法元数据</param>
        /// <returns>RPC方法名称</returns>
        private string GetRpcMethodName(MethodMetadata methodMetadata)
        {
            var methodName = methodMetadata.DisplayName ?? methodMetadata.MethodName;
            return $"{_options.ServiceNamePrefix}{methodName}{_options.ServiceNameSuffix}";
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <param name="arguments">参数值</param>
        private void ValidateParameters(MethodInfo method, object[] arguments)
        {
            var parameters = method.GetParameters();
            
            for (int i = 0; i < parameters.Length && i < arguments.Length; i++)
            {
                var parameter = parameters[i];
                var value = arguments[i];
                
                try
                {
                    _parameterValidator.ValidateParameter(parameter, value);
                }
                catch (RpcValidationException ex)
                {
                    _logger?.LogWarning("参数验证失败: {Parameter}, 错误: {Error}", 
                        parameter.Name, ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// 判断是否为异步方法
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否为异步方法</returns>
        private static bool IsAsyncMethod(MethodInfo method)
        {
            var returnType = method.ReturnType;
            return returnType == typeof(Task) || 
                   returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);
        }
    }

    /// <summary>
    /// RPC代理异常
    /// </summary>
    public class RpcProxyException : Exception
    {
        public RpcProxyException(string message) : base(message) { }
        public RpcProxyException(string message, Exception innerException) : base(message, innerException) { }
    }
} 