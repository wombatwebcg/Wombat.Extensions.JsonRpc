using Wombat.Extensions.JsonRpc.Client;
using Wombat.Extensions.JsonRpcTestCommon;

namespace Wombat.Extensions.JsonRpcClientTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 启动 JsonRpc 客户端测试...");
            
            // 创建 RPC 客户端
            var client = new RpcClient();
            
            try
            {
                // 连接到服务器
                Console.WriteLine("🔗 正在连接到 JsonRpc 服务器 (localhost:50051)...");
                await client.ConnectTcpAsync("localhost", 50051);
                
                if (client.IsConnected)
                {
                    Console.WriteLine("✅ 成功连接到 JsonRpc 服务器");
                    Console.WriteLine($"📊 连接信息:");
                    Console.WriteLine($"   - 远程端点: {client.RemoteEndPoint}");
                    Console.WriteLine($"   - 本地端点: {client.LocalEndPoint}");
                    
                    // 获取计算器代理（保留用于后续改进）
                    var calculator = client.CreateProxy<ICalculator>();
                    Console.WriteLine("🎯 成功创建计算器代理");
                    
                    // 测试各种计算方法（当前使用直接调用）
                    await TestCalculatorMethodsAsync(client);
                    
                    // 测试异常情况（当前使用直接调用）
                    await TestExceptionHandlingAsync(client);
                    
                    // 测试边界情况
                    await TestBoundaryConditionsAsync(client);
                    
                    // 显示客户端统计信息
                    DisplayClientStatistics(client);
                }
                else
                {
                    Console.WriteLine("❌ 连接失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 客户端测试过程中发生错误: {ex.Message}");
                Console.WriteLine($"   异常详情: {ex}");
            }
            finally
            {
                // 断开连接
                if (client.IsConnected)
                {
                    await client.DisconnectAsync();
                    Console.WriteLine("🔌 已断开与服务器的连接");
                }
                
                client.Dispose();
                Console.WriteLine("🧹 客户端资源已释放");
            }
            
            Console.WriteLine("⏳ 按任意键退出...");
            Console.ReadKey();
        }
        
        private static async Task TestCalculatorMethodsAsync(RpcClient client)
        {
            Console.WriteLine("\n🧮 开始测试计算器方法...");
            
            try
            {
                // 测试加法 - 使用直接调用
                var addResult = await client.InvokeAsync<int>("Calculator.Add", 10, 5);
                Console.WriteLine($"✅ 加法测试: 10 + 5 = {addResult}");
                
                // 测试减法 - 使用直接调用
                var subtractResult = await client.InvokeAsync<int>("Calculator.Subtract", 10, 5);
                Console.WriteLine($"✅ 减法测试: 10 - 5 = {subtractResult}");
                
                // 测试乘法 - 使用直接调用
                var multiplyResult = await client.InvokeAsync<int>("Calculator.Multiply", 10, 5);
                Console.WriteLine($"✅ 乘法测试: 10 * 5 = {multiplyResult}");
                
                // 测试除法 - 使用直接调用
                var divideResult = await client.InvokeAsync<double>("Calculator.Divide", 10, 5);
                Console.WriteLine($"✅ 除法测试: 10 / 5 = {divideResult}");
                
                // 测试获取信息 - 使用直接调用
                var info = await client.InvokeAsync<string>("Calculator.GetInfo");
                Console.WriteLine($"✅ 信息获取测试: {info}");
                
                Console.WriteLine("🎉 所有计算方法测试通过！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 计算方法测试失败: {ex.Message}");
            }
        }
        
        private static async Task TestExceptionHandlingAsync(RpcClient client)
        {
            Console.WriteLine("\n⚠️ 开始测试异常处理...");
            
            try
            {
                // 测试除零异常 - 使用直接调用
                var result = await client.InvokeAsync<double>("Calculator.Divide", 10, 0);
                Console.WriteLine($"❌ 除零测试意外成功: {result}");
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine($"✅ 除零异常处理正确: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 除零测试捕获到其他异常: {ex.Message}");
            }
            
            Console.WriteLine("🎉 异常处理测试完成！");
        }
        
        private static async Task TestBoundaryConditionsAsync(RpcClient client)
        {
            Console.WriteLine("\n🔍 开始测试边界条件...");
            
            try
            {
                // 测试调用不存在的方法
                Console.WriteLine("🔍 测试调用不存在的方法...");
                try
                {
                    var result = await client.InvokeAsync<string>("Calculator.NonExistentMethod");
                    Console.WriteLine($"❌ 调用不存在方法意外成功: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✅ 调用不存在方法正确抛出异常: {ex.Message}");
                }
                
                // 测试传递错误参数类型
                Console.WriteLine("🔍 测试传递错误参数类型...");
                try
                {
                    var result = await client.InvokeAsync<int>("Calculator.Add", "not_a_number", 5);
                    Console.WriteLine($"❌ 传递错误参数类型意外成功: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✅ 传递错误参数类型正确抛出异常: {ex.Message}");
                }
                
                // 测试传递过多参数
                Console.WriteLine("🔍 测试传递过多参数...");
                try
                {
                    var result = await client.InvokeAsync<int>("Calculator.Add", 1, 2, 3, 4, 5);
                    Console.WriteLine($"❌ 传递过多参数意外成功: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✅ 传递过多参数正确抛出异常: {ex.Message}");
                }
                
                // 测试传递过少参数
                Console.WriteLine("🔍 测试传递过少参数...");
                try
                {
                    var result = await client.InvokeAsync<int>("Calculator.Add", 1);
                    Console.WriteLine($"❌ 传递过少参数意外成功: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✅ 传递过少参数正确抛出异常: {ex.Message}");
                }
                
                Console.WriteLine("🎉 边界条件测试完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 边界条件测试过程中发生错误: {ex.Message}");
            }
        }
        
        private static void DisplayClientStatistics(RpcClient client)
        {
            Console.WriteLine("\n📈 客户端统计信息:");
            var stats = client.Statistics;
            Console.WriteLine($"   - 连接尝试次数: {stats.ConnectionAttempts}");
            Console.WriteLine($"   - 失败连接次数: {stats.FailedConnections}");
            Console.WriteLine($"   - 重连尝试次数: {stats.ReconnectAttempts}");
            Console.WriteLine($"   - 总请求数: {stats.TotalRequests}");
            Console.WriteLine($"   - 成功请求数: {stats.SuccessfulRequests}");
            Console.WriteLine($"   - 失败请求数: {stats.FailedRequests}");
            Console.WriteLine($"   - 总通知数: {stats.TotalNotifications}");
            Console.WriteLine($"   - 心跳发送数: {stats.HeartbeatsSent}");
            Console.WriteLine($"   - 平均延迟: {stats.AverageLatency:F2}ms");
            Console.WriteLine($"   - 成功率: {stats.SuccessRate:F2}%");
            Console.WriteLine($"   - 请求/秒: {stats.RequestsPerSecond:F2}");
            
            if (stats.ConnectionDuration.HasValue)
            {
                Console.WriteLine($"   - 连接持续时间: {stats.ConnectionDuration.Value.TotalSeconds:F2}秒");
            }
        }
    }
}
