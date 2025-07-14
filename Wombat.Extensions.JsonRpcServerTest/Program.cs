using Wombat.Extensions.JsonRpc.Server;
using Wombat.Extensions.JsonRpcTestCommon;

namespace Wombat.Extensions.JsonRpcServerTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            
            // 注册计算器服务
            builder.Services.AddSingleton<CalculatorService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseAuthorization();
            app.MapControllers();

            // 启动 JsonRpc 服务器
            await StartJsonRpcServerAsync(app.Services);

            // 启动 Web 应用
            app.Run();
        }

        private static async Task StartJsonRpcServerAsync(IServiceProvider serviceProvider)
        {
            try
            {
                // 创建 RPC 服务器
                var rpcServer = new RpcServer(serviceProvider: serviceProvider);
                
                // 获取计算器服务实例
                var calculatorService = serviceProvider.GetRequiredService<CalculatorService>();
                
                // 注册计算器服务到 RPC 服务器
                var registered = await rpcServer.RegisterServiceAsync(calculatorService);
                
                if (registered)
                {
                    Console.WriteLine("✅ 计算器服务注册成功");
                }
                else
                {
                    Console.WriteLine("❌ 计算器服务注册失败");
                    return;
                }

                // 启动 TCP 服务器，监听端口 50051
                await rpcServer.StartTcpAsync(50051);
                
                Console.WriteLine("🚀 JsonRpc 服务器已启动，监听端口: 50051");
                Console.WriteLine("📊 服务器统计信息:");
                Console.WriteLine($"   - 注册服务数: {rpcServer.Statistics.RegisteredServices}");
                Console.WriteLine($"   - 启动时间: {rpcServer.Statistics.StartTime:yyyy-MM-dd HH:mm:ss}");

                // 注册事件处理器
                rpcServer.ClientConnected += (sender, e) =>
                {
                    Console.WriteLine($"🔗 客户端已连接: {e.Connection.RemoteEndPoint} (ID: {e.Connection.Id})");
                };

                rpcServer.ClientDisconnected += (sender, e) =>
                {
                    Console.WriteLine($"🔌 客户端已断开: {e.Connection.RemoteEndPoint} (ID: {e.Connection.Id}) - 原因: {e.Reason}");
                };

                rpcServer.ServerError += (sender, e) =>
                {
                    Console.WriteLine($"❌ 服务器错误: {e.Message}");
                    if (e.Exception != null)
                    {
                        Console.WriteLine($"   异常详情: {e.Exception.Message}");
                    }
                };

                // 保持服务器运行
                Console.WriteLine("⏳ 按任意键停止 JsonRpc 服务器...");
                Console.ReadKey();

                // 停止服务器
                await rpcServer.StopAsync();
                Console.WriteLine("🛑 JsonRpc 服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动 JsonRpc 服务器时发生错误: {ex.Message}");
                Console.WriteLine($"   异常详情: {ex}");
            }
        }
    }
}
