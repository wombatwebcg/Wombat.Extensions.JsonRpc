using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Extensions.JsonRpc.Resilience.Failover
{
    /// <summary>
    /// 故障转移管理器接口
    /// </summary>
    public interface IFailoverManager
    {
        /// <summary>
        /// 获取当前活跃的服务端点
        /// </summary>
        /// <returns>服务端点</returns>
        Task<ServiceEndpoint> GetActiveEndpointAsync();

        /// <summary>
        /// 获取所有可用的服务端点
        /// </summary>
        /// <returns>可用端点列表</returns>
        Task<IEnumerable<ServiceEndpoint>> GetAvailableEndpointsAsync();

        /// <summary>
        /// 标记端点为不可用
        /// </summary>
        /// <param name="endpoint">端点</param>
        /// <param name="reason">原因</param>
        Task MarkEndpointAsUnavailableAsync(ServiceEndpoint endpoint, string reason = null);

        /// <summary>
        /// 标记端点为可用
        /// </summary>
        /// <param name="endpoint">端点</param>
        Task MarkEndpointAsAvailableAsync(ServiceEndpoint endpoint);

        /// <summary>
        /// 触发故障转移
        /// </summary>
        /// <param name="failedEndpoint">失败的端点</param>
        /// <param name="reason">故障原因</param>
        /// <returns>新的活跃端点</returns>
        Task<ServiceEndpoint> TriggerFailoverAsync(ServiceEndpoint failedEndpoint, string reason = null);

        /// <summary>
        /// 执行健康检查
        /// </summary>
        Task PerformHealthCheckAsync();

        /// <summary>
        /// 获取故障转移统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        FailoverStatistics GetStatistics();

        /// <summary>
        /// 故障转移事件
        /// </summary>
        event EventHandler<FailoverEventArgs> FailoverOccurred;

        /// <summary>
        /// 端点状态变更事件
        /// </summary>
        event EventHandler<EndpointStatusChangedEventArgs> EndpointStatusChanged;
    }

    /// <summary>
    /// 故障转移管理器实现
    /// </summary>
    public class FailoverManager : IFailoverManager, IDisposable
    {
        private readonly ILogger<FailoverManager> _logger;
        private readonly FailoverOptions _options;
        private readonly ConcurrentDictionary<string, ServiceEndpoint> _endpoints;
        private readonly ConcurrentDictionary<string, EndpointHealthInfo> _endpointHealth;
        private readonly Timer _healthCheckTimer;
        private ServiceEndpoint _currentActiveEndpoint;
        private long _totalFailovers = 0;
        private long _totalHealthChecks = 0;
        private readonly object _failoverLock = new object();
        private volatile bool _disposed = false;

        public event EventHandler<FailoverEventArgs> FailoverOccurred;
        public event EventHandler<EndpointStatusChangedEventArgs> EndpointStatusChanged;

        public FailoverManager(FailoverOptions options, ILogger<FailoverManager> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FailoverManager>.Instance;
            _endpoints = new ConcurrentDictionary<string, ServiceEndpoint>();
            _endpointHealth = new ConcurrentDictionary<string, EndpointHealthInfo>();

            // 初始化端点
            InitializeEndpoints();

            // 启动健康检查定时器
            if (_options.EnableHealthCheck)
            {
                _healthCheckTimer = new Timer(async _ => await PerformHealthCheckAsync(),
                    null, _options.HealthCheckInterval, _options.HealthCheckInterval);
            }

            _logger.LogInformation("故障转移管理器已初始化，端点数量: {EndpointCount}", _endpoints.Count);
        }

        public async Task<ServiceEndpoint> GetActiveEndpointAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FailoverManager));

            // 如果当前活跃端点不可用，触发故障转移
            if (_currentActiveEndpoint != null && !IsEndpointAvailable(_currentActiveEndpoint))
            {
                _logger.LogWarning("当前活跃端点 {Endpoint} 不可用，触发故障转移", _currentActiveEndpoint.Address);
                return await TriggerFailoverAsync(_currentActiveEndpoint, "当前端点不可用");
            }

            // 如果没有活跃端点，选择一个可用的
            if (_currentActiveEndpoint == null)
            {
                var availableEndpoints = await GetAvailableEndpointsAsync();
                _currentActiveEndpoint = SelectBestEndpoint(availableEndpoints);
                
                if (_currentActiveEndpoint == null)
                {
                    _logger.LogError("没有可用的服务端点");
                    throw new NoAvailableEndpointException("没有可用的服务端点");
                }

                _logger.LogInformation("选择端点 {Endpoint} 作为活跃端点", _currentActiveEndpoint.Address);
            }

            return _currentActiveEndpoint;
        }

        public async Task<IEnumerable<ServiceEndpoint>> GetAvailableEndpointsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FailoverManager));

            var availableEndpoints = new List<ServiceEndpoint>();

            foreach (var endpoint in _endpoints.Values)
            {
                if (IsEndpointAvailable(endpoint))
                {
                    availableEndpoints.Add(endpoint);
                }
            }

            // 如果启用了健康检查，过滤不健康的端点
            if (_options.EnableHealthCheck)
            {
                availableEndpoints = availableEndpoints.Where(e =>
                {
                    if (_endpointHealth.TryGetValue(e.Id, out var health))
                    {
                        return health.IsHealthy;
                    }
                    return true; // 如果没有健康信息，假设是健康的
                }).ToList();
            }

            return availableEndpoints;
        }

        public async Task MarkEndpointAsUnavailableAsync(ServiceEndpoint endpoint, string reason = null)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            if (_endpoints.TryGetValue(endpoint.Id, out var existingEndpoint))
            {
                existingEndpoint.Status = EndpointStatus.Unavailable;
                existingEndpoint.LastFailureTime = DateTime.UtcNow;
                existingEndpoint.FailureReason = reason;

                _logger.LogWarning("端点 {Endpoint} 被标记为不可用，原因: {Reason}", endpoint.Address, reason);

                // 更新健康信息
                if (_endpointHealth.TryGetValue(endpoint.Id, out var health))
                {
                    health.IsHealthy = false;
                    health.LastFailureTime = DateTime.UtcNow;
                    health.FailureReason = reason;
                    health.ConsecutiveFailures++;
                }

                // 触发状态变更事件
                EndpointStatusChanged?.Invoke(this, new EndpointStatusChangedEventArgs
                {
                    Endpoint = existingEndpoint,
                    PreviousStatus = EndpointStatus.Available,
                    CurrentStatus = EndpointStatus.Unavailable,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow
                });

                // 如果这是当前活跃端点，触发故障转移
                if (_currentActiveEndpoint?.Id == endpoint.Id)
                {
                    await TriggerFailoverAsync(endpoint, reason);
                }
            }
        }

        public async Task MarkEndpointAsAvailableAsync(ServiceEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            if (_endpoints.TryGetValue(endpoint.Id, out var existingEndpoint))
            {
                var previousStatus = existingEndpoint.Status;
                existingEndpoint.Status = EndpointStatus.Available;
                existingEndpoint.LastSuccessTime = DateTime.UtcNow;
                existingEndpoint.FailureReason = null;

                _logger.LogInformation("端点 {Endpoint} 被标记为可用", endpoint.Address);

                // 更新健康信息
                if (_endpointHealth.TryGetValue(endpoint.Id, out var health))
                {
                    health.IsHealthy = true;
                    health.LastSuccessTime = DateTime.UtcNow;
                    health.FailureReason = null;
                    health.ConsecutiveFailures = 0;
                }

                // 触发状态变更事件
                EndpointStatusChanged?.Invoke(this, new EndpointStatusChangedEventArgs
                {
                    Endpoint = existingEndpoint,
                    PreviousStatus = previousStatus,
                    CurrentStatus = EndpointStatus.Available,
                    Reason = "端点恢复可用",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public async Task<ServiceEndpoint> TriggerFailoverAsync(ServiceEndpoint failedEndpoint, string reason = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FailoverManager));

            lock (_failoverLock)
            {
                _logger.LogWarning("开始故障转移，失败端点: {FailedEndpoint}, 原因: {Reason}", 
                    failedEndpoint?.Address, reason);

                // 标记失败端点为不可用
                if (failedEndpoint != null)
                {
                    _ = MarkEndpointAsUnavailableAsync(failedEndpoint, reason);
                }

                // 获取可用端点
                var availableEndpoints = GetAvailableEndpointsAsync().Result;
                var newActiveEndpoint = SelectBestEndpoint(availableEndpoints);

                if (newActiveEndpoint == null)
                {
                    _logger.LogError("故障转移失败：没有可用的备用端点");
                    
                    // 如果没有可用端点，根据策略决定行为
                    if (_options.FailurePolicy == FailurePolicy.KeepTrying)
                    {
                        // 保持当前端点，稍后重试
                        _logger.LogInformation("根据故障策略，保持当前端点并稍后重试");
                        return _currentActiveEndpoint;
                    }
                    else
                    {
                        _currentActiveEndpoint = null;
                        throw new NoAvailableEndpointException("故障转移失败：没有可用的备用端点");
                    }
                }

                var previousEndpoint = _currentActiveEndpoint;
                _currentActiveEndpoint = newActiveEndpoint;
                Interlocked.Increment(ref _totalFailovers);

                _logger.LogInformation("故障转移完成：{PreviousEndpoint} -> {NewEndpoint}", 
                    previousEndpoint?.Address ?? "None", newActiveEndpoint.Address);

                // 触发故障转移事件
                FailoverOccurred?.Invoke(this, new FailoverEventArgs
                {
                    FailedEndpoint = failedEndpoint,
                    NewActiveEndpoint = newActiveEndpoint,
                    PreviousActiveEndpoint = previousEndpoint,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow,
                    FailoverCount = _totalFailovers
                });

                return newActiveEndpoint;
            }
        }

        public async Task PerformHealthCheckAsync()
        {
            if (_disposed) return;

            Interlocked.Increment(ref _totalHealthChecks);
            _logger.LogDebug("开始健康检查，端点数量: {EndpointCount}", _endpoints.Count);

            var healthCheckTasks = _endpoints.Values.Select(async endpoint =>
            {
                try
                {
                    var isHealthy = await CheckEndpointHealthAsync(endpoint);
                    UpdateEndpointHealth(endpoint, isHealthy, null);
                }
                catch (Exception ex)
                {
                    UpdateEndpointHealth(endpoint, false, ex.Message);
                    _logger.LogWarning(ex, "端点 {Endpoint} 健康检查失败", endpoint.Address);
                }
            });

            await Task.WhenAll(healthCheckTasks);

            _logger.LogDebug("健康检查完成");
        }

        public FailoverStatistics GetStatistics()
        {
            var healthyEndpoints = _endpointHealth.Values.Count(h => h.IsHealthy);
            var unhealthyEndpoints = _endpointHealth.Values.Count(h => !h.IsHealthy);

            return new FailoverStatistics
            {
                TotalEndpoints = _endpoints.Count,
                HealthyEndpoints = healthyEndpoints,
                UnhealthyEndpoints = unhealthyEndpoints,
                CurrentActiveEndpoint = _currentActiveEndpoint?.Address,
                TotalFailovers = _totalFailovers,
                TotalHealthChecks = _totalHealthChecks,
                LastHealthCheckTime = _endpointHealth.Values.Any() ? 
                    _endpointHealth.Values.Max(h => h.LastCheckTime) : (DateTime?)null,
                EndpointDetails = _endpoints.Values.Select(e => new EndpointStatistics
                {
                    Id = e.Id,
                    Address = e.Address,
                    Status = e.Status,
                    Priority = e.Priority,
                    Weight = e.Weight,
                    LastSuccessTime = e.LastSuccessTime,
                    LastFailureTime = e.LastFailureTime,
                    FailureReason = e.FailureReason,
                    IsHealthy = _endpointHealth.TryGetValue(e.Id, out var health) ? health.IsHealthy : true,
                    ConsecutiveFailures = _endpointHealth.TryGetValue(e.Id, out var health2) ? health2.ConsecutiveFailures : 0
                }).ToList()
            };
        }

        private void InitializeEndpoints()
        {
            foreach (var endpoint in _options.Endpoints)
            {
                endpoint.Id = endpoint.Id ?? Guid.NewGuid().ToString();
                endpoint.Status = EndpointStatus.Available;
                _endpoints[endpoint.Id] = endpoint;

                // 初始化健康信息
                _endpointHealth[endpoint.Id] = new EndpointHealthInfo
                {
                    EndpointId = endpoint.Id,
                    IsHealthy = true,
                    LastCheckTime = DateTime.UtcNow,
                    ConsecutiveFailures = 0
                };
            }

            // 设置初始活跃端点
            if (_endpoints.Any())
            {
                _currentActiveEndpoint = SelectBestEndpoint(_endpoints.Values);
                _logger.LogInformation("初始活跃端点: {Endpoint}", _currentActiveEndpoint?.Address);
            }
        }

        private bool IsEndpointAvailable(ServiceEndpoint endpoint)
        {
            if (endpoint == null) return false;
            
            // 检查基本状态
            if (endpoint.Status != EndpointStatus.Available) return false;

            // 检查是否在冷却期内
            if (endpoint.LastFailureTime.HasValue && _options.CooldownPeriod > TimeSpan.Zero)
            {
                var timeSinceFailure = DateTime.UtcNow - endpoint.LastFailureTime.Value;
                if (timeSinceFailure < _options.CooldownPeriod)
                {
                    return false;
                }
            }

            return true;
        }

        private ServiceEndpoint SelectBestEndpoint(IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            var endpoints = availableEndpoints.ToList();
            if (!endpoints.Any()) return null;

            switch (_options.SelectionStrategy)
            {
                case EndpointSelectionStrategy.Priority:
                    return endpoints.OrderBy(e => e.Priority).FirstOrDefault();

                case EndpointSelectionStrategy.Weight:
                    return SelectByWeight(endpoints);

                case EndpointSelectionStrategy.RoundRobin:
                    return SelectByRoundRobin(endpoints);

                case EndpointSelectionStrategy.Random:
                    return endpoints[new Random().Next(endpoints.Count)];

                case EndpointSelectionStrategy.HealthScore:
                    return SelectByHealthScore(endpoints);

                default:
                    return endpoints.FirstOrDefault();
            }
        }

        private ServiceEndpoint SelectByWeight(List<ServiceEndpoint> endpoints)
        {
            var totalWeight = endpoints.Sum(e => e.Weight);
            if (totalWeight <= 0) return endpoints.FirstOrDefault();

            var random = new Random().Next(totalWeight);
            var currentWeight = 0;

            foreach (var endpoint in endpoints)
            {
                currentWeight += endpoint.Weight;
                if (random < currentWeight)
                {
                    return endpoint;
                }
            }

            return endpoints.LastOrDefault();
        }

        private ServiceEndpoint SelectByRoundRobin(List<ServiceEndpoint> endpoints)
        {
            // 简单的轮询实现
            var sortedEndpoints = endpoints.OrderBy(e => e.Id).ToList();
            var currentIndex = 0;
            
            if (_currentActiveEndpoint != null)
            {
                var currentIdx = sortedEndpoints.FindIndex(e => e.Id == _currentActiveEndpoint.Id);
                if (currentIdx >= 0)
                {
                    currentIndex = (currentIdx + 1) % sortedEndpoints.Count;
                }
            }

            return sortedEndpoints[currentIndex];
        }

        private ServiceEndpoint SelectByHealthScore(List<ServiceEndpoint> endpoints)
        {
            return endpoints.OrderByDescending(endpoint =>
            {
                if (_endpointHealth.TryGetValue(endpoint.Id, out var health))
                {
                    var score = 100.0; // 基础分数
                    
                    // 根据连续失败次数减分
                    score -= health.ConsecutiveFailures * 10;
                    
                    // 根据最近成功时间加分
                    if (health.LastSuccessTime.HasValue)
                    {
                        var timeSinceSuccess = DateTime.UtcNow - health.LastSuccessTime.Value;
                        if (timeSinceSuccess < TimeSpan.FromMinutes(5))
                        {
                            score += 20;
                        }
                    }
                    
                    // 根据健康状态
                    if (!health.IsHealthy) score -= 50;
                    
                    return Math.Max(0, score);
                }
                return 50; // 默认分数
            }).FirstOrDefault();
        }

        private async Task<bool> CheckEndpointHealthAsync(ServiceEndpoint endpoint)
        {
            if (_options.HealthCheckFunction != null)
            {
                return await _options.HealthCheckFunction(endpoint);
            }

            // 默认健康检查逻辑（简单的连接测试）
            try
            {
                // 这里可以实现具体的健康检查逻辑
                // 比如发送健康检查请求、检查网络连接等
                await Task.Delay(100); // 模拟健康检查
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateEndpointHealth(ServiceEndpoint endpoint, bool isHealthy, string errorMessage)
        {
            if (_endpointHealth.TryGetValue(endpoint.Id, out var health))
            {
                var wasHealthy = health.IsHealthy;
                health.IsHealthy = isHealthy;
                health.LastCheckTime = DateTime.UtcNow;

                if (isHealthy)
                {
                    health.LastSuccessTime = DateTime.UtcNow;
                    health.ConsecutiveFailures = 0;
                    health.FailureReason = null;

                    // 如果从不健康变为健康，标记端点为可用
                    if (!wasHealthy)
                    {
                        _ = MarkEndpointAsAvailableAsync(endpoint);
                    }
                }
                else
                {
                    health.LastFailureTime = DateTime.UtcNow;
                    health.ConsecutiveFailures++;
                    health.FailureReason = errorMessage;

                    // 如果连续失败超过阈值，标记端点为不可用
                    if (health.ConsecutiveFailures >= _options.MaxConsecutiveFailures)
                    {
                        _ = MarkEndpointAsUnavailableAsync(endpoint, $"连续失败 {health.ConsecutiveFailures} 次");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _healthCheckTimer?.Dispose();

            _logger.LogInformation("故障转移管理器已释放");
        }
    }

    /// <summary>
    /// 服务端点
    /// </summary>
    public class ServiceEndpoint
    {
        /// <summary>
        /// 端点ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 端点地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 端点端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 优先级（数值越小优先级越高）
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// 权重（用于负载均衡）
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// 当前状态
        /// </summary>
        public EndpointStatus Status { get; set; } = EndpointStatus.Available;

        /// <summary>
        /// 最后成功时间
        /// </summary>
        public DateTime? LastSuccessTime { get; set; }

        /// <summary>
        /// 最后失败时间
        /// </summary>
        public DateTime? LastFailureTime { get; set; }

        /// <summary>
        /// 失败原因
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// 自定义属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            return $"{Address}:{Port} (Priority: {Priority}, Weight: {Weight}, Status: {Status})";
        }
    }

    /// <summary>
    /// 端点状态
    /// </summary>
    public enum EndpointStatus
    {
        /// <summary>
        /// 可用
        /// </summary>
        Available,

        /// <summary>
        /// 不可用
        /// </summary>
        Unavailable,

        /// <summary>
        /// 维护中
        /// </summary>
        Maintenance
    }

    /// <summary>
    /// 端点选择策略
    /// </summary>
    public enum EndpointSelectionStrategy
    {
        /// <summary>
        /// 按优先级选择
        /// </summary>
        Priority,

        /// <summary>
        /// 按权重选择
        /// </summary>
        Weight,

        /// <summary>
        /// 轮询选择
        /// </summary>
        RoundRobin,

        /// <summary>
        /// 随机选择
        /// </summary>
        Random,

        /// <summary>
        /// 按健康评分选择
        /// </summary>
        HealthScore
    }

    /// <summary>
    /// 故障策略
    /// </summary>
    public enum FailurePolicy
    {
        /// <summary>
        /// 立即失败
        /// </summary>
        FailFast,

        /// <summary>
        /// 继续尝试
        /// </summary>
        KeepTrying
    }

    /// <summary>
    /// 故障转移配置选项
    /// </summary>
    public class FailoverOptions
    {
        /// <summary>
        /// 服务端点列表
        /// </summary>
        public List<ServiceEndpoint> Endpoints { get; set; } = new List<ServiceEndpoint>();

        /// <summary>
        /// 端点选择策略
        /// </summary>
        public EndpointSelectionStrategy SelectionStrategy { get; set; } = EndpointSelectionStrategy.Priority;

        /// <summary>
        /// 故障策略
        /// </summary>
        public FailurePolicy FailurePolicy { get; set; } = FailurePolicy.FailFast;

        /// <summary>
        /// 是否启用健康检查
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// 健康检查间隔
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 冷却期（端点失败后多长时间才能重新尝试）
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 最大连续失败次数
        /// </summary>
        public int MaxConsecutiveFailures { get; set; } = 3;

        /// <summary>
        /// 自定义健康检查函数
        /// </summary>
        public Func<ServiceEndpoint, Task<bool>> HealthCheckFunction { get; set; }
    }

    /// <summary>
    /// 端点健康信息
    /// </summary>
    internal class EndpointHealthInfo
    {
        public string EndpointId { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastCheckTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public string FailureReason { get; set; }
        public int ConsecutiveFailures { get; set; } = 0;
    }

    /// <summary>
    /// 故障转移统计信息
    /// </summary>
    public class FailoverStatistics
    {
        public int TotalEndpoints { get; set; }
        public int HealthyEndpoints { get; set; }
        public int UnhealthyEndpoints { get; set; }
        public string CurrentActiveEndpoint { get; set; }
        public long TotalFailovers { get; set; }
        public long TotalHealthChecks { get; set; }
        public DateTime? LastHealthCheckTime { get; set; }
        public List<EndpointStatistics> EndpointDetails { get; set; }
    }

    /// <summary>
    /// 端点统计信息
    /// </summary>
    public class EndpointStatistics
    {
        public string Id { get; set; }
        public string Address { get; set; }
        public EndpointStatus Status { get; set; }
        public int Priority { get; set; }
        public int Weight { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public string FailureReason { get; set; }
        public bool IsHealthy { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    /// <summary>
    /// 故障转移事件参数
    /// </summary>
    public class FailoverEventArgs : EventArgs
    {
        public ServiceEndpoint FailedEndpoint { get; set; }
        public ServiceEndpoint NewActiveEndpoint { get; set; }
        public ServiceEndpoint PreviousActiveEndpoint { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
        public long FailoverCount { get; set; }
    }

    /// <summary>
    /// 端点状态变更事件参数
    /// </summary>
    public class EndpointStatusChangedEventArgs : EventArgs
    {
        public ServiceEndpoint Endpoint { get; set; }
        public EndpointStatus PreviousStatus { get; set; }
        public EndpointStatus CurrentStatus { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 无可用端点异常
    /// </summary>
    public class NoAvailableEndpointException : Exception
    {
        public NoAvailableEndpointException(string message) : base(message) { }
        public NoAvailableEndpointException(string message, Exception innerException) : base(message, innerException) { }
    }
} 