using System;
using System.Diagnostics;
using System.Reflection;

namespace Wombat.Extensions.JsonRpc.Monitoring.Telemetry
{
    /// <summary>
    /// RPC活动源
    /// 提供统一的OpenTelemetry分布式追踪支持
    /// </summary>
    public static class RpcActivitySource
    {
        /// <summary>
        /// 活动源名称
        /// </summary>
        public const string ActivitySourceName = "Wombat.Extensions.JsonRpc";

        /// <summary>
        /// 活动源版本
        /// </summary>
        public static readonly string ActivitySourceVersion = GetVersion();

        /// <summary>
        /// 活动源实例
        /// </summary>
        public static readonly ActivitySource Instance = new ActivitySource(ActivitySourceName, ActivitySourceVersion);

        /// <summary>
        /// 活动标签常量
        /// </summary>
        public static class Tags
        {
            // 基本标签
            public const string RpcSystem = "rpc.system";
            public const string RpcService = "rpc.service";
            public const string RpcMethod = "rpc.method";
            public const string RpcClientId = "rpc.client_id";
            public const string RpcRequestId = "rpc.request_id";
            public const string RpcServerName = "rpc.server_name";
            public const string RpcServerVersion = "rpc.server_version";

            // 网络标签
            public const string RpcTransportType = "rpc.transport.type";
            public const string RpcClientAddress = "rpc.client.address";
            public const string RpcClientPort = "rpc.client.port";
            public const string RpcServerAddress = "rpc.server.address";
            public const string RpcServerPort = "rpc.server.port";

            // 请求/响应标签
            public const string RpcParameters = "rpc.parameters";
            public const string RpcResponse = "rpc.response";
            public const string RpcResponseTimeMs = "rpc.response_time_ms";
            public const string RpcSuccess = "rpc.success";
            public const string RpcSlowRequest = "rpc.slow_request";

            // 错误标签
            public const string ErrorType = "error.type";
            public const string ErrorMessage = "error.message";
            public const string ErrorStackTrace = "error.stack_trace";

            // 性能标签
            public const string RpcRequestSize = "rpc.request_size";
            public const string RpcResponseSize = "rpc.response_size";
            public const string RpcCompressionRatio = "rpc.compression_ratio";
            public const string RpcBatchSize = "rpc.batch_size";

            // 系统标签
            public const string SystemMemoryUsage = "system.memory_usage";
            public const string SystemCpuUsage = "system.cpu_usage";
            public const string SystemThreadCount = "system.thread_count";
            public const string SystemProcessId = "system.process_id";
            public const string SystemManagedThreadId = "system.managed_thread_id";

            // 中间件标签
            public const string MiddlewareName = "middleware.name";
            public const string MiddlewareOrder = "middleware.order";
            public const string MiddlewareDuration = "middleware.duration_ms";

            // 认证标签
            public const string AuthUserId = "auth.user_id";
            public const string AuthUserName = "auth.user_name";
            public const string AuthUserRoles = "auth.user_roles";
            public const string AuthScheme = "auth.scheme";

            // 业务标签
            public const string BusinessContext = "business.context";
            public const string BusinessCorrelationId = "business.correlation_id";
            public const string BusinessTenantId = "business.tenant_id";
        }

        /// <summary>
        /// 活动名称常量
        /// </summary>
        public static class Activities
        {
            public const string RpcRequest = "RPC Request";
            public const string RpcResponse = "RPC Response";
            public const string RpcBatch = "RPC Batch";
            public const string RpcConnection = "RPC Connection";
            public const string RpcSerialization = "RPC Serialization";
            public const string RpcDeserialization = "RPC Deserialization";
            public const string RpcValidation = "RPC Validation";
            public const string RpcAuthentication = "RPC Authentication";
            public const string RpcAuthorization = "RPC Authorization";
            public const string RpcMiddleware = "RPC Middleware";
        }

        /// <summary>
        /// 创建RPC请求活动
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="clientId">客户端ID</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcRequestActivity(string serviceName, string methodName, string clientId = null)
        {
            var activity = Instance.StartActivity($"{Activities.RpcRequest}: {serviceName}.{methodName}");
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.RpcService, serviceName);
                activity.SetTag(Tags.RpcMethod, methodName);
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
                activity.SetTag(Tags.SystemProcessId, Environment.ProcessId);
                activity.SetTag(Tags.SystemManagedThreadId, Environment.CurrentManagedThreadId);
                
                if (!string.IsNullOrEmpty(clientId))
                {
                    activity.SetTag(Tags.RpcClientId, clientId);
                }
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC批处理活动
        /// </summary>
        /// <param name="batchSize">批处理大小</param>
        /// <param name="serviceName">服务名称</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcBatchActivity(int batchSize, string serviceName = null)
        {
            var activity = Instance.StartActivity($"{Activities.RpcBatch}: {batchSize} requests");
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.RpcBatchSize, batchSize);
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
                
                if (!string.IsNullOrEmpty(serviceName))
                {
                    activity.SetTag(Tags.RpcService, serviceName);
                }
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC连接活动
        /// </summary>
        /// <param name="transportType">传输类型</param>
        /// <param name="clientAddress">客户端地址</param>
        /// <param name="clientPort">客户端端口</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcConnectionActivity(string transportType, string clientAddress = null, int? clientPort = null)
        {
            var activity = Instance.StartActivity($"{Activities.RpcConnection}: {transportType}");
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.RpcTransportType, transportType);
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
                
                if (!string.IsNullOrEmpty(clientAddress))
                {
                    activity.SetTag(Tags.RpcClientAddress, clientAddress);
                }
                
                if (clientPort.HasValue)
                {
                    activity.SetTag(Tags.RpcClientPort, clientPort.Value);
                }
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC中间件活动
        /// </summary>
        /// <param name="middlewareName">中间件名称</param>
        /// <param name="order">执行顺序</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcMiddlewareActivity(string middlewareName, int order = 0)
        {
            var activity = Instance.StartActivity($"{Activities.RpcMiddleware}: {middlewareName}");
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.MiddlewareName, middlewareName);
                activity.SetTag(Tags.MiddlewareOrder, order);
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC认证活动
        /// </summary>
        /// <param name="scheme">认证方案</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcAuthenticationActivity(string scheme)
        {
            var activity = Instance.StartActivity($"{Activities.RpcAuthentication}: {scheme}");
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.AuthScheme, scheme);
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC授权活动
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="userName">用户名</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcAuthorizationActivity(string userId = null, string userName = null)
        {
            var activity = Instance.StartActivity(Activities.RpcAuthorization);
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
                
                if (!string.IsNullOrEmpty(userId))
                {
                    activity.SetTag(Tags.AuthUserId, userId);
                }
                
                if (!string.IsNullOrEmpty(userName))
                {
                    activity.SetTag(Tags.AuthUserName, userName);
                }
            }

            return activity;
        }

        /// <summary>
        /// 创建RPC序列化活动
        /// </summary>
        /// <param name="type">序列化类型（serialization/deserialization）</param>
        /// <param name="dataSize">数据大小</param>
        /// <returns>活动实例</returns>
        public static Activity StartRpcSerializationActivity(string type, long dataSize = 0)
        {
            var activityName = type == "serialization" ? Activities.RpcSerialization : Activities.RpcDeserialization;
            var activity = Instance.StartActivity(activityName);
            
            if (activity != null)
            {
                activity.SetTag(Tags.RpcSystem, "jsonrpc");
                activity.SetTag(Tags.RpcServerName, Environment.MachineName);
                activity.SetTag(Tags.RpcServerVersion, ActivitySourceVersion);
                
                if (dataSize > 0)
                {
                    if (type == "serialization")
                    {
                        activity.SetTag(Tags.RpcResponseSize, dataSize);
                    }
                    else
                    {
                        activity.SetTag(Tags.RpcRequestSize, dataSize);
                    }
                }
            }

            return activity;
        }

        /// <summary>
        /// 为活动设置成功状态
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="responseTimeMs">响应时间（毫秒）</param>
        public static void SetActivitySuccess(Activity activity, double responseTimeMs = 0)
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Ok);
            activity.SetTag(Tags.RpcSuccess, true);
            
            if (responseTimeMs > 0)
            {
                activity.SetTag(Tags.RpcResponseTimeMs, responseTimeMs);
            }
        }

        /// <summary>
        /// 为活动设置错误状态
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="exception">异常信息</param>
        /// <param name="includeStackTrace">是否包含堆栈跟踪</param>
        public static void SetActivityError(Activity activity, Exception exception, bool includeStackTrace = false)
        {
            if (activity == null || exception == null) return;

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(Tags.RpcSuccess, false);
            activity.SetTag(Tags.ErrorType, exception.GetType().Name);
            activity.SetTag(Tags.ErrorMessage, exception.Message);
            
            if (includeStackTrace)
            {
                activity.SetTag(Tags.ErrorStackTrace, exception.StackTrace);
            }
        }

        /// <summary>
        /// 为活动设置业务上下文
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="context">业务上下文</param>
        /// <param name="correlationId">关联ID</param>
        /// <param name="tenantId">租户ID</param>
        public static void SetActivityBusinessContext(Activity activity, string context = null, string correlationId = null, string tenantId = null)
        {
            if (activity == null) return;

            if (!string.IsNullOrEmpty(context))
            {
                activity.SetTag(Tags.BusinessContext, context);
            }
            
            if (!string.IsNullOrEmpty(correlationId))
            {
                activity.SetTag(Tags.BusinessCorrelationId, correlationId);
            }
            
            if (!string.IsNullOrEmpty(tenantId))
            {
                activity.SetTag(Tags.BusinessTenantId, tenantId);
            }
        }

        /// <summary>
        /// 为活动设置认证信息
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="userId">用户ID</param>
        /// <param name="userName">用户名</param>
        /// <param name="userRoles">用户角色</param>
        /// <param name="scheme">认证方案</param>
        public static void SetActivityAuthInfo(Activity activity, string userId = null, string userName = null, string userRoles = null, string scheme = null)
        {
            if (activity == null) return;

            if (!string.IsNullOrEmpty(userId))
            {
                activity.SetTag(Tags.AuthUserId, userId);
            }
            
            if (!string.IsNullOrEmpty(userName))
            {
                activity.SetTag(Tags.AuthUserName, userName);
            }
            
            if (!string.IsNullOrEmpty(userRoles))
            {
                activity.SetTag(Tags.AuthUserRoles, userRoles);
            }
            
            if (!string.IsNullOrEmpty(scheme))
            {
                activity.SetTag(Tags.AuthScheme, scheme);
            }
        }

        /// <summary>
        /// 为活动设置性能指标
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="requestSize">请求大小</param>
        /// <param name="responseSize">响应大小</param>
        /// <param name="compressionRatio">压缩率</param>
        public static void SetActivityPerformanceMetrics(Activity activity, long requestSize = 0, long responseSize = 0, double compressionRatio = 0)
        {
            if (activity == null) return;

            if (requestSize > 0)
            {
                activity.SetTag(Tags.RpcRequestSize, requestSize);
            }
            
            if (responseSize > 0)
            {
                activity.SetTag(Tags.RpcResponseSize, responseSize);
            }
            
            if (compressionRatio > 0)
            {
                activity.SetTag(Tags.RpcCompressionRatio, compressionRatio);
            }
        }

        /// <summary>
        /// 为活动设置系统资源信息
        /// </summary>
        /// <param name="activity">活动实例</param>
        /// <param name="memoryUsage">内存使用量</param>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="threadCount">线程数</param>
        public static void SetActivitySystemMetrics(Activity activity, long memoryUsage = 0, double cpuUsage = 0, int threadCount = 0)
        {
            if (activity == null) return;

            if (memoryUsage > 0)
            {
                activity.SetTag(Tags.SystemMemoryUsage, memoryUsage);
            }
            
            if (cpuUsage > 0)
            {
                activity.SetTag(Tags.SystemCpuUsage, cpuUsage);
            }
            
            if (threadCount > 0)
            {
                activity.SetTag(Tags.SystemThreadCount, threadCount);
            }
        }

        /// <summary>
        /// 获取版本号
        /// </summary>
        /// <returns>版本号</returns>
        private static string GetVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public static void Dispose()
        {
            Instance?.Dispose();
        }
    }
} 