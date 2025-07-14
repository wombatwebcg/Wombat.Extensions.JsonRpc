using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wombat.Extensions.JsonRpc.Monitoring.Core
{
    /// <summary>
    /// RPC性能指标收集器接口
    /// </summary>
    public interface IRpcMetricsCollector
    {
        /// <summary>
        /// 指标报告事件
        /// </summary>
        event EventHandler<RpcMetricsReportEventArgs> MetricsReported;

        /// <summary>
        /// 记录请求开始
        /// </summary>
        /// <param name="methodName">方法名</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="clientId">客户端ID</param>
        /// <returns>请求ID</returns>
        string RecordRequestStart(string methodName, string serviceName, string clientId = null);

        /// <summary>
        /// 记录请求完成
        /// </summary>
        /// <param name="requestId">请求ID</param>
        /// <param name="success">是否成功</param>
        /// <param name="errorCode">错误代码</param>
        /// <param name="responseSize">响应大小</param>
        void RecordRequestComplete(string requestId, bool success, string errorCode = null, long responseSize = 0);

        /// <summary>
        /// 记录请求错误
        /// </summary>
        /// <param name="requestId">请求ID</param>
        /// <param name="exception">异常</param>
        /// <param name="errorCode">错误代码</param>
        void RecordRequestError(string requestId, Exception exception, string errorCode = null);

        /// <summary>
        /// 记录连接指标
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="connected">是否连接</param>
        /// <param name="transportType">传输类型</param>
        void RecordConnectionMetrics(string connectionId, bool connected, string transportType);

        /// <summary>
        /// 记录批处理指标
        /// </summary>
        /// <param name="batchSize">批处理大小</param>
        /// <param name="batchDuration">批处理持续时间</param>
        /// <param name="compressionRatio">压缩率</param>
        void RecordBatchMetrics(int batchSize, TimeSpan batchDuration, double compressionRatio);

        /// <summary>
        /// 记录资源使用情况
        /// </summary>
        /// <param name="memoryUsage">内存使用量</param>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="threadCount">线程数</param>
        void RecordResourceUsage(long memoryUsage, double cpuUsage, int threadCount);

        /// <summary>
        /// 获取指标快照
        /// </summary>
        /// <returns>指标快照</returns>
        Task<RpcMetricsSnapshot> GetMetricsSnapshotAsync();

        /// <summary>
        /// 获取指标历史
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>指标历史</returns>
        Task<IEnumerable<RpcMetricsSnapshot>> GetMetricsHistoryAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// 重置指标
        /// </summary>
        void ResetMetrics();

        /// <summary>
        /// 设置报告间隔
        /// </summary>
        /// <param name="interval">间隔</param>
        void SetReportingInterval(TimeSpan interval);
    }

    /// <summary>
    /// RPC指标报告事件参数
    /// </summary>
    public class RpcMetricsReportEventArgs : EventArgs
    {
        /// <summary>
        /// 指标快照
        /// </summary>
        public RpcMetricsSnapshot Snapshot { get; set; }

        /// <summary>
        /// 报告时间
        /// </summary>
        public DateTime ReportTime { get; set; }

        /// <summary>
        /// 异常列表
        /// </summary>
        public List<string> Anomalies { get; set; } = new List<string>();
    }

    /// <summary>
    /// RPC指标快照
    /// </summary>
    public class RpcMetricsSnapshot
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 总请求数
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// 成功请求数
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// 失败请求数
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// 成功率
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均响应时间
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// 当前QPS
        /// </summary>
        public double CurrentQps { get; set; }

        /// <summary>
        /// 活跃连接数
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// 方法级别指标
        /// </summary>
        public Dictionary<string, RpcMethodMetrics> MethodMetrics { get; set; } = new Dictionary<string, RpcMethodMetrics>();

        /// <summary>
        /// 传输层指标
        /// </summary>
        public Dictionary<string, RpcTransportMetrics> TransportMetrics { get; set; } = new Dictionary<string, RpcTransportMetrics>();

        /// <summary>
        /// 系统资源使用情况
        /// </summary>
        public SystemResourceUsage ResourceUsage { get; set; } = new SystemResourceUsage();
    }

    /// <summary>
    /// RPC方法指标
    /// </summary>
    public class RpcMethodMetrics
    {
        /// <summary>
        /// 方法名
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 服务名
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 总调用次数
        /// </summary>
        public long TotalCalls { get; set; }

        /// <summary>
        /// 成功调用次数
        /// </summary>
        public long SuccessfulCalls { get; set; }

        /// <summary>
        /// 失败调用次数
        /// </summary>
        public long FailedCalls { get; set; }

        /// <summary>
        /// 平均响应时间
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// 最小响应时间
        /// </summary>
        public double MinResponseTime { get; set; }

        /// <summary>
        /// 最大响应时间
        /// </summary>
        public double MaxResponseTime { get; set; }

        /// <summary>
        /// 95%分位响应时间
        /// </summary>
        public double P95ResponseTime { get; set; }

        /// <summary>
        /// 99%分位响应时间
        /// </summary>
        public double P99ResponseTime { get; set; }

        /// <summary>
        /// 当前QPS
        /// </summary>
        public double CurrentQps { get; set; }
    }

    /// <summary>
    /// RPC传输层指标
    /// </summary>
    public class RpcTransportMetrics
    {
        /// <summary>
        /// 传输类型
        /// </summary>
        public string TransportType { get; set; }

        /// <summary>
        /// 总连接数
        /// </summary>
        public long TotalConnections { get; set; }

        /// <summary>
        /// 活跃连接数
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// 连接失败次数
        /// </summary>
        public long ConnectionFailures { get; set; }

        /// <summary>
        /// 平均连接时间
        /// </summary>
        public double AverageConnectionTime { get; set; }
    }

    /// <summary>
    /// 系统资源使用情况
    /// </summary>
    public class SystemResourceUsage
    {
        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// CPU使用率（百分比）
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// 线程数
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// 句柄数
        /// </summary>
        public int HandleCount { get; set; }
    }
} 