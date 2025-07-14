using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpcTestCommon
{
    /// <summary>
    /// 计算器RPC接口
    /// </summary>
    [RpcService("Calculator")]
    public interface ICalculator
    {
        /// <summary>
        /// 加法运算
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        [RpcMethod("Calculator.Add")]
        Task<int> Add(int a, int b);

        /// <summary>
        /// 减法运算
        /// </summary>
        /// <param name="a">被减数</param>
        /// <param name="b">减数</param>
        /// <returns>两数之差</returns>
        [RpcMethod("Calculator.Subtract")]
        Task<int> Subtract(int a, int b);

        /// <summary>
        /// 乘法运算
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之积</returns>
        [RpcMethod("Calculator.Multiply")]
        Task<int> Multiply(int a, int b);

        /// <summary>
        /// 除法运算
        /// </summary>
        /// <param name="a">被除数</param>
        /// <param name="b">除数</param>
        /// <returns>两数之商</returns>
        [RpcMethod("Calculator.Divide")]
        Task<double> Divide(int a, int b);

        /// <summary>
        /// 获取计算器信息
        /// </summary>
        /// <returns>计算器信息</returns>
        [RpcMethod("Calculator.GetInfo")]
        Task<string> GetInfo();
    }
} 