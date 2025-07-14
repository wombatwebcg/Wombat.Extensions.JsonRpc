using Wombat.Extensions.JsonRpcTestCommon;

namespace Wombat.Extensions.JsonRpcServerTest
{
    /// <summary>
    /// 计算器服务实现
    /// </summary>
    public class CalculatorService : ICalculator
    {
        private readonly ILogger<CalculatorService> _logger;

        public CalculatorService(ILogger<CalculatorService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 加法运算
        /// </summary>
        public async Task<int> Add(int a, int b)
        {
            _logger?.LogInformation("执行加法运算: {A} + {B}", a, b);
            var result = a + b;
            _logger?.LogInformation("加法运算结果: {Result}", result);
            return await Task.FromResult(result);
        }

        /// <summary>
        /// 减法运算
        /// </summary>
        public async Task<int> Subtract(int a, int b)
        {
            _logger?.LogInformation("执行减法运算: {A} - {B}", a, b);
            var result = a - b;
            _logger?.LogInformation("减法运算结果: {Result}", result);
            return await Task.FromResult(result);
        }

        /// <summary>
        /// 乘法运算
        /// </summary>
        public async Task<int> Multiply(int a, int b)
        {
            _logger?.LogInformation("执行乘法运算: {A} * {B}", a, b);
            var result = a * b;
            _logger?.LogInformation("乘法运算结果: {Result}", result);
            return await Task.FromResult(result);
        }

        /// <summary>
        /// 除法运算
        /// </summary>
        public async Task<double> Divide(int a, int b)
        {
            _logger?.LogInformation("执行除法运算: {A} / {B}", a, b);
            
            if (b == 0)
            {
                _logger?.LogError("除法运算错误: 除数不能为零");
                throw new DivideByZeroException("除数不能为零");
            }

            var result = (double)a / b;
            _logger?.LogInformation("除法运算结果: {Result}", result);
            return await Task.FromResult(result);
        }

        /// <summary>
        /// 获取计算器信息
        /// </summary>
        public async Task<string> GetInfo()
        {
            _logger?.LogInformation("获取计算器信息");
            var info = $"Wombat JsonRpc Calculator Service - 版本 1.0.0 - 运行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            _logger?.LogInformation("计算器信息: {Info}", info);
            return await Task.FromResult(info);
        }
    }
} 