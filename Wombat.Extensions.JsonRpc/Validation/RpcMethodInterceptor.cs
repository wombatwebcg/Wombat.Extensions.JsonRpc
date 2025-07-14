using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.Validation
{
    /// <summary>
    /// RPC方法拦截器 - 自动验证参数并处理异常
    /// </summary>
    public class RpcMethodInterceptor
    {
        private readonly ParameterValidator _parameterValidator;
        private readonly ILogger<RpcMethodInterceptor> _logger;

        public RpcMethodInterceptor(ParameterValidator parameterValidator, ILogger<RpcMethodInterceptor> logger = null)
        {
            _parameterValidator = parameterValidator ?? throw new ArgumentNullException(nameof(parameterValidator));
            _logger = logger;
        }

        /// <summary>
        /// 拦截方法调用并执行参数验证
        /// </summary>
        /// <param name="method">被调用的方法</param>
        /// <param name="target">目标对象</param>
        /// <param name="args">方法参数</param>
        /// <returns>方法执行结果</returns>
        public async Task<object> InterceptAsync(MethodInfo method, object target, object[] args)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var methodName = $"{target.GetType().Name}.{method.Name}";
            
            _logger?.LogDebug("开始拦截方法调用: {Method}", methodName);

            try
            {
                // 1. 执行参数验证
                var validationResult = await _parameterValidator.ValidateParametersAsync(method, args);
                
                if (!validationResult.IsValid)
                {
                    _logger?.LogWarning("方法 {Method} 参数验证失败", methodName);
                    
                    // 抛出包含详细验证错误的异常
                    var validationResults = validationResult.Errors.Select(e => 
                        new System.ComponentModel.DataAnnotations.ValidationResult(
                            e.ErrorMessage, 
                            new[] { e.ParameterName }
                        )
                    );
                    var validationException = new RpcValidationException(validationResults);
                    
                    throw validationException;
                }

                // 2. 执行原方法
                _logger?.LogDebug("参数验证通过，开始执行方法: {Method}", methodName);
                
                var result = method.Invoke(target, args);

                // 3. 处理异步方法的返回值
                if (result is Task taskResult)
                {
                    await taskResult;
                    
                    // 检查是否有返回值的异步方法
                    if (taskResult.GetType().IsGenericType)
                    {
                        var property = taskResult.GetType().GetProperty("Result");
                        result = property?.GetValue(taskResult);
                    }
                    else
                    {
                        result = null; // void Task
                    }
                }

                _logger?.LogDebug("方法 {Method} 执行成功", methodName);
                return result;
            }
            catch (RpcValidationException)
            {
                // 重新抛出验证异常
                throw;
            }
            catch (TargetInvocationException ex)
            {
                // 解包反射调用异常
                _logger?.LogError(ex.InnerException, "方法 {Method} 执行时发生异常", methodName);
                throw ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "拦截方法 {Method} 时发生异常", methodName);
                throw;
            }
        }

        /// <summary>
        /// 检查方法是否需要参数验证
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>是否需要验证</returns>
        public bool ShouldValidate(MethodInfo method)
        {
            if (method == null)
                return false;

            // 检查方法参数是否有任何验证特性
            var parameters = method.GetParameters();
            return parameters.Any(p => p.GetCustomAttributes<System.ComponentModel.DataAnnotations.ValidationAttribute>().Any());
        }

        /// <summary>
        /// 创建方法代理委托
        /// </summary>
        /// <param name="method">原方法</param>
        /// <param name="target">目标对象</param>
        /// <returns>代理委托</returns>
        public Func<object[], Task<object>> CreateProxy(MethodInfo method, object target)
        {
            return async (args) => await InterceptAsync(method, target, args);
        }
    }

    /// <summary>
    /// RPC方法拦截器工厂
    /// </summary>
    public class RpcMethodInterceptorFactory
    {
        private readonly ParameterValidator _parameterValidator;
        private readonly ILogger<RpcMethodInterceptor> _logger;

        public RpcMethodInterceptorFactory(ParameterValidator parameterValidator, ILogger<RpcMethodInterceptor> logger = null)
        {
            _parameterValidator = parameterValidator ?? throw new ArgumentNullException(nameof(parameterValidator));
            _logger = logger;
        }

        /// <summary>
        /// 创建拦截器实例
        /// </summary>
        /// <returns>拦截器实例</returns>
        public RpcMethodInterceptor CreateInterceptor()
        {
            return new RpcMethodInterceptor(_parameterValidator, _logger);
        }
    }

    /// <summary>
    /// RPC方法调用上下文
    /// </summary>
    public class RpcInvocationContext
    {
        public MethodInfo Method { get; set; }
        public object Target { get; set; }
        public object[] Arguments { get; set; }
        public string ServiceName { get; set; }
        public string MethodName { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; }
        public Exception Exception { get; set; }
        public object Result { get; set; }
    }

    /// <summary>
    /// RPC方法调用事件参数
    /// </summary>
    public class RpcInvocationEventArgs : EventArgs
    {
        public RpcInvocationContext Context { get; set; }

        public RpcInvocationEventArgs(RpcInvocationContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }
} 