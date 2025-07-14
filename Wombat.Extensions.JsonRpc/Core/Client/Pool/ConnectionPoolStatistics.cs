using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Wombat.Extensions.JsonRpc.Core.Client.Pool
{
    /// <summary>
    /// 连接池统计信息
    /// </summary>
    public class ConnectionPoolStatistics
    {
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<string, EndpointStatistics> _endpointStats = 
            new ConcurrentDictionary<string, EndpointStatistics>();

        /// <summary>
        /// 总连接数
        /// </summary>
        public int TotalConnections { get; private set; }

        /// <summary>
        /// 活动连接数
        /// </summary>
        public int ActiveConnections { get; private set; }

        /// <summary>
        /// 空闲连接数
        /// </summary>
        public int IdleConnections { get; private set; }

        /// <summary>
        /// 等待连接的请求数
        /// </summary>
        public int WaitingRequests { get; private set; }

        /// <summary>
        /// 已创建连接总数
        /// </summary>
        public long TotalConnectionsCreated { get; private set; }

        /// <summary>
        /// 已关闭连接总数
        /// </summary>
        public long TotalConnectionsClosed { get; private set; }

        /// <summary>
        /// 连接获取总数
        /// </summary>
        public long TotalConnectionsAcquired { get; private set; }

        /// <summary>
        /// 连接释放总数
        /// </summary>
        public long TotalConnectionsReleased { get; private set; }

        /// <summary>
        /// 连接获取失败总数
        /// </summary>
        public long TotalConnectionsFailedToAcquire { get; private set; }

        /// <summary>
        /// 连接验证失败总数
        /// </summary>
        public long TotalConnectionsFailedToValidate { get; private set; }

        /// <summary>
        /// 连接超时总数
        /// </summary>
        public long TotalConnectionsTimedOut { get; private set; }

        /// <summary>
        /// 平均连接获取时间（毫秒）
        /// </summary>
        public double AverageConnectionAcquisitionTimeMs { get; private set; }

        /// <summary>
        /// 平均连接使用时间（毫秒）
        /// </summary>
        public double AverageConnectionUsageTimeMs { get; private set; }

        /// <summary>
        /// 最大连接获取时间（毫秒）
        /// </summary>
        public double MaxConnectionAcquisitionTimeMs { get; private set; }

        /// <summary>
        /// 最大连接使用时间（毫秒）
        /// </summary>
        public double MaxConnectionUsageTimeMs { get; private set; }

        /// <summary>
        /// 连接池启动时间
        /// </summary>
        public DateTime StartTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 连接池运行时间
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;

        /// <summary>
        /// 获取所有端点统计信息
        /// </summary>
        /// <returns>端点统计信息字典</returns>
        public IReadOnlyDictionary<string, EndpointStatistics> GetEndpointStatistics()
        {
            return _endpointStats;
        }

        /// <summary>
        /// 获取指定端点的统计信息
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <returns>端点统计信息</returns>
        public EndpointStatistics GetEndpointStatistics(ConnectionEndpoint endpoint)
        {
            var key = endpoint.GetEndpointKey();
            return _endpointStats.GetOrAdd(key, k => new EndpointStatistics { EndpointKey = k });
        }

        /// <summary>
        /// 增加连接数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">增量</param>
        public void IncrementConnections(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                TotalConnections += delta;
                TotalConnectionsCreated += delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.IncrementConnections(delta);
        }

        /// <summary>
        /// 减少连接数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">减量</param>
        public void DecrementConnections(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                TotalConnections -= delta;
                TotalConnectionsClosed += delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.DecrementConnections(delta);
        }

        /// <summary>
        /// 增加活动连接数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">增量</param>
        public void IncrementActiveConnections(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                ActiveConnections += delta;
                IdleConnections -= delta;
                TotalConnectionsAcquired += delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.IncrementActiveConnections(delta);
        }

        /// <summary>
        /// 减少活动连接数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">减量</param>
        public void DecrementActiveConnections(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                ActiveConnections -= delta;
                IdleConnections += delta;
                TotalConnectionsReleased += delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.DecrementActiveConnections(delta);
        }

        /// <summary>
        /// 增加等待请求数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">增量</param>
        public void IncrementWaitingRequests(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                WaitingRequests += delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.IncrementWaitingRequests(delta);
        }

        /// <summary>
        /// 减少等待请求数
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="delta">减量</param>
        public void DecrementWaitingRequests(ConnectionEndpoint endpoint, int delta = 1)
        {
            lock (_lock)
            {
                WaitingRequests -= delta;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.DecrementWaitingRequests(delta);
        }

        /// <summary>
        /// 记录连接获取失败
        /// </summary>
        /// <param name="endpoint">端点</param>
        public void RecordConnectionAcquisitionFailure(ConnectionEndpoint endpoint)
        {
            lock (_lock)
            {
                TotalConnectionsFailedToAcquire++;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.RecordConnectionAcquisitionFailure();
        }

        /// <summary>
        /// 记录连接验证失败
        /// </summary>
        /// <param name="endpoint">端点</param>
        public void RecordConnectionValidationFailure(ConnectionEndpoint endpoint)
        {
            lock (_lock)
            {
                TotalConnectionsFailedToValidate++;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.RecordConnectionValidationFailure();
        }

        /// <summary>
        /// 记录连接超时
        /// </summary>
        /// <param name="endpoint">端点</param>
        public void RecordConnectionTimeout(ConnectionEndpoint endpoint)
        {
            lock (_lock)
            {
                TotalConnectionsTimedOut++;
                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.RecordConnectionTimeout();
        }

        /// <summary>
        /// 更新连接获取时间
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="acquisitionTimeMs">获取时间（毫秒）</param>
        public void UpdateConnectionAcquisitionTime(ConnectionEndpoint endpoint, double acquisitionTimeMs)
        {
            lock (_lock)
            {
                // 使用指数移动平均计算平均时间
                AverageConnectionAcquisitionTimeMs = AverageConnectionAcquisitionTimeMs == 0 ? 
                    acquisitionTimeMs : 
                    (AverageConnectionAcquisitionTimeMs * 0.9 + acquisitionTimeMs * 0.1);

                if (acquisitionTimeMs > MaxConnectionAcquisitionTimeMs)
                {
                    MaxConnectionAcquisitionTimeMs = acquisitionTimeMs;
                }

                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.UpdateConnectionAcquisitionTime(acquisitionTimeMs);
        }

        /// <summary>
        /// 更新连接使用时间
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="usageTimeMs">使用时间（毫秒）</param>
        public void UpdateConnectionUsageTime(ConnectionEndpoint endpoint, double usageTimeMs)
        {
            lock (_lock)
            {
                // 使用指数移动平均计算平均时间
                AverageConnectionUsageTimeMs = AverageConnectionUsageTimeMs == 0 ? 
                    usageTimeMs : 
                    (AverageConnectionUsageTimeMs * 0.9 + usageTimeMs * 0.1);

                if (usageTimeMs > MaxConnectionUsageTimeMs)
                {
                    MaxConnectionUsageTimeMs = usageTimeMs;
                }

                LastUpdateTime = DateTime.UtcNow;
            }

            var endpointStats = GetEndpointStatistics(endpoint);
            endpointStats.UpdateConnectionUsageTime(usageTimeMs);
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                TotalConnections = 0;
                ActiveConnections = 0;
                IdleConnections = 0;
                WaitingRequests = 0;
                TotalConnectionsCreated = 0;
                TotalConnectionsClosed = 0;
                TotalConnectionsAcquired = 0;
                TotalConnectionsReleased = 0;
                TotalConnectionsFailedToAcquire = 0;
                TotalConnectionsFailedToValidate = 0;
                TotalConnectionsTimedOut = 0;
                AverageConnectionAcquisitionTimeMs = 0;
                AverageConnectionUsageTimeMs = 0;
                MaxConnectionAcquisitionTimeMs = 0;
                MaxConnectionUsageTimeMs = 0;
                StartTime = DateTime.UtcNow;
                LastUpdateTime = DateTime.UtcNow;
            }

            _endpointStats.Clear();
        }

        /// <summary>
        /// 获取统计信息摘要
        /// </summary>
        /// <returns>统计信息摘要</returns>
        public StatisticsSummary GetSummary()
        {
            lock (_lock)
            {
                return new StatisticsSummary
                {
                    TotalConnections = TotalConnections,
                    ActiveConnections = ActiveConnections,
                    IdleConnections = IdleConnections,
                    WaitingRequests = WaitingRequests,
                    TotalConnectionsCreated = TotalConnectionsCreated,
                    TotalConnectionsClosed = TotalConnectionsClosed,
                    TotalConnectionsAcquired = TotalConnectionsAcquired,
                    TotalConnectionsReleased = TotalConnectionsReleased,
                    TotalConnectionsFailedToAcquire = TotalConnectionsFailedToAcquire,
                    TotalConnectionsFailedToValidate = TotalConnectionsFailedToValidate,
                    TotalConnectionsTimedOut = TotalConnectionsTimedOut,
                    AverageConnectionAcquisitionTimeMs = AverageConnectionAcquisitionTimeMs,
                    AverageConnectionUsageTimeMs = AverageConnectionUsageTimeMs,
                    MaxConnectionAcquisitionTimeMs = MaxConnectionAcquisitionTimeMs,
                    MaxConnectionUsageTimeMs = MaxConnectionUsageTimeMs,
                    StartTime = StartTime,
                    LastUpdateTime = LastUpdateTime,
                    Uptime = Uptime,
                    EndpointCount = _endpointStats.Count
                };
            }
        }
    }

    /// <summary>
    /// 端点统计信息
    /// </summary>
    public class EndpointStatistics
    {
        private readonly object _lock = new object();

        /// <summary>
        /// 端点键
        /// </summary>
        public string EndpointKey { get; set; }

        /// <summary>
        /// 总连接数
        /// </summary>
        public int TotalConnections { get; private set; }

        /// <summary>
        /// 活动连接数
        /// </summary>
        public int ActiveConnections { get; private set; }

        /// <summary>
        /// 空闲连接数
        /// </summary>
        public int IdleConnections { get; private set; }

        /// <summary>
        /// 等待请求数
        /// </summary>
        public int WaitingRequests { get; private set; }

        /// <summary>
        /// 已创建连接数
        /// </summary>
        public long ConnectionsCreated { get; private set; }

        /// <summary>
        /// 已关闭连接数
        /// </summary>
        public long ConnectionsClosed { get; private set; }

        /// <summary>
        /// 连接获取总数
        /// </summary>
        public long ConnectionsAcquired { get; private set; }

        /// <summary>
        /// 连接释放总数
        /// </summary>
        public long ConnectionsReleased { get; private set; }

        /// <summary>
        /// 连接获取失败数
        /// </summary>
        public long ConnectionsFailedToAcquire { get; private set; }

        /// <summary>
        /// 连接验证失败数
        /// </summary>
        public long ConnectionsFailedToValidate { get; private set; }

        /// <summary>
        /// 连接超时数
        /// </summary>
        public long ConnectionsTimedOut { get; private set; }

        /// <summary>
        /// 平均连接获取时间（毫秒）
        /// </summary>
        public double AverageConnectionAcquisitionTimeMs { get; private set; }

        /// <summary>
        /// 平均连接使用时间（毫秒）
        /// </summary>
        public double AverageConnectionUsageTimeMs { get; private set; }

        /// <summary>
        /// 最大连接获取时间（毫秒）
        /// </summary>
        public double MaxConnectionAcquisitionTimeMs { get; private set; }

        /// <summary>
        /// 最大连接使用时间（毫秒）
        /// </summary>
        public double MaxConnectionUsageTimeMs { get; private set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// 增加连接数
        /// </summary>
        /// <param name="delta">增量</param>
        public void IncrementConnections(int delta = 1)
        {
            lock (_lock)
            {
                TotalConnections += delta;
                ConnectionsCreated += delta;
                IdleConnections += delta;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 减少连接数
        /// </summary>
        /// <param name="delta">减量</param>
        public void DecrementConnections(int delta = 1)
        {
            lock (_lock)
            {
                TotalConnections -= delta;
                ConnectionsClosed += delta;
                if (IdleConnections >= delta)
                {
                    IdleConnections -= delta;
                }
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 增加活动连接数
        /// </summary>
        /// <param name="delta">增量</param>
        public void IncrementActiveConnections(int delta = 1)
        {
            lock (_lock)
            {
                ActiveConnections += delta;
                IdleConnections -= delta;
                ConnectionsAcquired += delta;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 减少活动连接数
        /// </summary>
        /// <param name="delta">减量</param>
        public void DecrementActiveConnections(int delta = 1)
        {
            lock (_lock)
            {
                ActiveConnections -= delta;
                IdleConnections += delta;
                ConnectionsReleased += delta;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 增加等待请求数
        /// </summary>
        /// <param name="delta">增量</param>
        public void IncrementWaitingRequests(int delta = 1)
        {
            lock (_lock)
            {
                WaitingRequests += delta;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 减少等待请求数
        /// </summary>
        /// <param name="delta">减量</param>
        public void DecrementWaitingRequests(int delta = 1)
        {
            lock (_lock)
            {
                WaitingRequests -= delta;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 记录连接获取失败
        /// </summary>
        public void RecordConnectionAcquisitionFailure()
        {
            lock (_lock)
            {
                ConnectionsFailedToAcquire++;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 记录连接验证失败
        /// </summary>
        public void RecordConnectionValidationFailure()
        {
            lock (_lock)
            {
                ConnectionsFailedToValidate++;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 记录连接超时
        /// </summary>
        public void RecordConnectionTimeout()
        {
            lock (_lock)
            {
                ConnectionsTimedOut++;
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 更新连接获取时间
        /// </summary>
        /// <param name="acquisitionTimeMs">获取时间（毫秒）</param>
        public void UpdateConnectionAcquisitionTime(double acquisitionTimeMs)
        {
            lock (_lock)
            {
                AverageConnectionAcquisitionTimeMs = AverageConnectionAcquisitionTimeMs == 0 ? 
                    acquisitionTimeMs : 
                    (AverageConnectionAcquisitionTimeMs * 0.9 + acquisitionTimeMs * 0.1);

                if (acquisitionTimeMs > MaxConnectionAcquisitionTimeMs)
                {
                    MaxConnectionAcquisitionTimeMs = acquisitionTimeMs;
                }

                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 更新连接使用时间
        /// </summary>
        /// <param name="usageTimeMs">使用时间（毫秒）</param>
        public void UpdateConnectionUsageTime(double usageTimeMs)
        {
            lock (_lock)
            {
                AverageConnectionUsageTimeMs = AverageConnectionUsageTimeMs == 0 ? 
                    usageTimeMs : 
                    (AverageConnectionUsageTimeMs * 0.9 + usageTimeMs * 0.1);

                if (usageTimeMs > MaxConnectionUsageTimeMs)
                {
                    MaxConnectionUsageTimeMs = usageTimeMs;
                }

                LastUpdateTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 统计信息摘要
    /// </summary>
    public class StatisticsSummary
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int WaitingRequests { get; set; }
        public long TotalConnectionsCreated { get; set; }
        public long TotalConnectionsClosed { get; set; }
        public long TotalConnectionsAcquired { get; set; }
        public long TotalConnectionsReleased { get; set; }
        public long TotalConnectionsFailedToAcquire { get; set; }
        public long TotalConnectionsFailedToValidate { get; set; }
        public long TotalConnectionsTimedOut { get; set; }
        public double AverageConnectionAcquisitionTimeMs { get; set; }
        public double AverageConnectionUsageTimeMs { get; set; }
        public double MaxConnectionAcquisitionTimeMs { get; set; }
        public double MaxConnectionUsageTimeMs { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public int EndpointCount { get; set; }
    }
} 