using System;
using System.Threading.Tasks;
using Wombat.Extensions.JsonRpc.Core.Contracts;

namespace Wombat.Extensions.JsonRpc.Examples
{
    /// <summary>
    /// 计算器服务接口示例
    /// </summary>
    [RpcService("Calculator", Description = "提供基本数学计算功能", Version = "1.0")]
    public interface ICalculatorService
    {
        /// <summary>
        /// 加法运算
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>计算结果</returns>
        [RpcMethod("Add", Description = "两数相加")]
        Task<int> AddAsync([RpcParamNotNull] int a, [RpcParamNotNull] int b);

        /// <summary>
        /// 减法运算
        /// </summary>
        /// <param name="a">被减数</param>
        /// <param name="b">减数</param>
        /// <returns>计算结果</returns>
        [RpcMethod("Subtract", Description = "两数相减")]
        Task<int> SubtractAsync(int a, int b);

        /// <summary>
        /// 乘法运算
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>计算结果</returns>
        [RpcMethod("Multiply", Description = "两数相乘")]
        Task<double> MultiplyAsync(double a, double b);

        /// <summary>
        /// 除法运算
        /// </summary>
        /// <param name="numerator">被除数</param>
        /// <param name="denominator">除数</param>
        /// <returns>计算结果</returns>
        [RpcMethod("Divide", RequireAuthentication = true, Description = "两数相除")]
        Task<double> DivideAsync(
            double numerator, 
            [RpcParamRange(0.001, double.MaxValue)] double denominator);

        /// <summary>
        /// 幂运算
        /// </summary>
        /// <param name="baseValue">底数</param>
        /// <param name="exponent">指数</param>
        /// <returns>计算结果</returns>
        [RpcMethod("Power", EnableCaching = true, CacheDurationSeconds = 600)]
        Task<double> PowerAsync(double baseValue, [RpcParamRange(-100, 100)] double exponent);

        /// <summary>
        /// 记录计算历史
        /// </summary>
        /// <param name="operation">操作类型</param>
        /// <param name="result">计算结果</param>
        [RpcMethod("LogCalculation", IsNotification = true, Description = "记录计算历史")]
        Task LogCalculationAsync(
            [RpcParamNotNull] [RpcParamStringLength(1, 50)] string operation, 
            double result);

        /// <summary>
        /// 获取计算统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        [RpcMethod("GetStatistics", EnableCaching = true, CacheDurationSeconds = 30)]
        Task<CalculationStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// 计算器服务实现
    /// </summary>
    public class CalculatorService : ICalculatorService
    {
        private int _calculationCount = 0;
        private DateTime _lastCalculation = DateTime.UtcNow;

        /// <summary>
        /// 加法运算
        /// </summary>
        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Delay(1); // 模拟异步操作
            var result = a + b;
            _calculationCount++;
            _lastCalculation = DateTime.UtcNow;
            
            // 记录计算历史
            await LogCalculationAsync($"Add({a}, {b})", result);
            
            return result;
        }

        /// <summary>
        /// 减法运算
        /// </summary>
        public async Task<int> SubtractAsync(int a, int b)
        {
            await Task.Delay(1);
            var result = a - b;
            _calculationCount++;
            _lastCalculation = DateTime.UtcNow;
            
            await LogCalculationAsync($"Subtract({a}, {b})", result);
            
            return result;
        }

        /// <summary>
        /// 乘法运算
        /// </summary>
        public async Task<double> MultiplyAsync(double a, double b)
        {
            await Task.Delay(1);
            var result = a * b;
            _calculationCount++;
            _lastCalculation = DateTime.UtcNow;
            
            await LogCalculationAsync($"Multiply({a}, {b})", result);
            
            return result;
        }

        /// <summary>
        /// 除法运算
        /// </summary>
        public async Task<double> DivideAsync(double numerator, double denominator)
        {
            await Task.Delay(1);
            
            if (Math.Abs(denominator) < 0.001)
            {
                throw new ArgumentException("除数不能为零或接近零");
            }
            
            var result = numerator / denominator;
            _calculationCount++;
            _lastCalculation = DateTime.UtcNow;
            
            await LogCalculationAsync($"Divide({numerator}, {denominator})", result);
            
            return result;
        }

        /// <summary>
        /// 幂运算
        /// </summary>
        public async Task<double> PowerAsync(double baseValue, double exponent)
        {
            await Task.Delay(1);
            var result = Math.Pow(baseValue, exponent);
            _calculationCount++;
            _lastCalculation = DateTime.UtcNow;
            
            await LogCalculationAsync($"Power({baseValue}, {exponent})", result);
            
            return result;
        }

        /// <summary>
        /// 记录计算历史
        /// </summary>
        public async Task LogCalculationAsync(string operation, double result)
        {
            await Task.Delay(1);
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {operation} = {result}");
        }

        /// <summary>
        /// 获取计算统计信息
        /// </summary>
        public async Task<CalculationStatistics> GetStatisticsAsync()
        {
            await Task.Delay(1);
            return new CalculationStatistics
            {
                TotalCalculations = _calculationCount,
                LastCalculationTime = _lastCalculation,
                ServiceStartTime = DateTime.UtcNow.AddMinutes(-10) // 假设服务已运行10分钟
            };
        }
    }

    /// <summary>
    /// 计算统计信息
    /// </summary>
    public class CalculationStatistics
    {
        /// <summary>
        /// 总计算次数
        /// </summary>
        public int TotalCalculations { get; set; }

        /// <summary>
        /// 最后计算时间
        /// </summary>
        public DateTime LastCalculationTime { get; set; }

        /// <summary>
        /// 服务启动时间
        /// </summary>
        public DateTime ServiceStartTime { get; set; }

        /// <summary>
        /// 计算频率（每分钟）
        /// </summary>
        public double CalculationsPerMinute => TotalCalculations / (DateTime.UtcNow - ServiceStartTime).TotalMinutes;
    }
}

// 使用示例
/*
namespace Wombat.Extensions.JsonRpc.Examples
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 创建元数据提供程序
            var metadataProvider = new DefaultRpcMetadataProvider();
            
            // 提取服务元数据
            var serviceMetadata = metadataProvider.ExtractServiceMetadata(typeof(CalculatorService));
            
            Console.WriteLine($"服务名称: {serviceMetadata.ServiceName}");
            Console.WriteLine($"服务描述: {serviceMetadata.Description}");
            Console.WriteLine($"服务版本: {serviceMetadata.Version}");
            Console.WriteLine($"方法数量: {serviceMetadata.Methods.Length}");
            
            // 列出所有方法
            foreach (var method in serviceMetadata.Methods)
            {
                Console.WriteLine($"- {method.MethodName}: {method.Description}");
                Console.WriteLine($"  需要认证: {method.RequireAuthentication}");
                Console.WriteLine($"  启用缓存: {method.EnableCaching}");
                Console.WriteLine($"  参数数量: {method.Parameters.Length}");
                
                foreach (var param in method.Parameters)
                {
                    Console.WriteLine($"    - {param.Name} ({param.GetDisplayTypeName()}): 必需={param.IsRequired}");
                }
            }
            
            // 创建服务实例
            var calculatorService = new CalculatorService();
            
            // 模拟RPC调用
            var addResult = await calculatorService.AddAsync(10, 20);
            Console.WriteLine($"10 + 20 = {addResult}");
            
            var divideResult = await calculatorService.DivideAsync(100, 3);
            Console.WriteLine($"100 / 3 = {divideResult}");
            
            var stats = await calculatorService.GetStatisticsAsync();
            Console.WriteLine($"总计算次数: {stats.TotalCalculations}");
        }
    }
}
*/ 