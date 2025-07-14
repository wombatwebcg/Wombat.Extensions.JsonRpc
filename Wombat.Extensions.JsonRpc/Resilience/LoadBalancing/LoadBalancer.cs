using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Resilience.Failover;

namespace Wombat.Extensions.JsonRpc.Resilience.LoadBalancing
{
    /// <summary>
    /// 负载均衡器接口
    /// </summary>
    public interface ILoadBalancer
    {
        /// <summary>
        /// 选择一个服务端点
        /// </summary>
        /// <param name="context">负载均衡上下文</param>
        /// <returns>选中的端点</returns>
        Task<ServiceEndpoint> SelectEndpointAsync(LoadBalancingContext context = null);

        /// <summary>
        /// 更新端点权重
        /// </summary>
        /// <param name="endpointId">端点ID</param>
        /// <param name="weight">新权重</param>
        Task UpdateEndpointWeightAsync(string endpointId, int weight);

        /// <summary>
        /// 记录请求结果
        /// </summary>
        /// <param name="endpointId">端点ID</param>
        /// <param name="responseTime">响应时间</param>
        /// <param name="success">是否成功</param>
        Task RecordRequestResultAsync(string endpointId, TimeSpan responseTime, bool success);

        /// <summary>
        /// 获取负载均衡统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        LoadBalancingStatistics GetStatistics();

        /// <summary>
        /// 获取所有端点
        /// </summary>
        /// <returns>端点列表</returns>
        IEnumerable<ServiceEndpoint> GetEndpoints();
    }

    /// <summary>
    /// 负载均衡器实现
    /// </summary>
    public class LoadBalancer : ILoadBalancer
    {
        private readonly ILogger<LoadBalancer> _logger;
        private readonly LoadBalancingOptions _options;
        private readonly ConcurrentDictionary<string, ServiceEndpoint> _endpoints;
        private readonly ConcurrentDictionary<string, EndpointMetrics> _endpointMetrics;
        private readonly ILoadBalancingAlgorithm _algorithm;
        private long _totalRequests = 0;
        private readonly object _lockObject = new object();

        public LoadBalancer(LoadBalancingOptions options, ILogger<LoadBalancer> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LoadBalancer>.Instance;
            _endpoints = new ConcurrentDictionary<string, ServiceEndpoint>();
            _endpointMetrics = new ConcurrentDictionary<string, EndpointMetrics>();

            // 初始化端点
            InitializeEndpoints();

            // 创建负载均衡算法
            _algorithm = CreateAlgorithm(_options.Algorithm);

            _logger.LogInformation("负载均衡器已初始化，算法: {Algorithm}, 端点数量: {EndpointCount}", 
                _options.Algorithm, _endpoints.Count);
        }

        public async Task<ServiceEndpoint> SelectEndpointAsync(LoadBalancingContext context = null)
        {
            Interlocked.Increment(ref _totalRequests);

            var availableEndpoints = GetAvailableEndpoints();
            if (!availableEndpoints.Any())
            {
                _logger.LogWarning("没有可用的服务端点");
                throw new NoAvailableEndpointException("没有可用的服务端点");
            }

            var selectedEndpoint = await _algorithm.SelectEndpointAsync(availableEndpoints, context);
            
            if (selectedEndpoint != null)
            {
                // 更新端点指标
                if (_endpointMetrics.TryGetValue(selectedEndpoint.Id, out var metrics))
                {
                    Interlocked.Increment(ref metrics.RequestCount);
                }

                _logger.LogDebug("选择端点: {Endpoint}, 算法: {Algorithm}", selectedEndpoint.Address, _options.Algorithm);
            }

            return selectedEndpoint;
        }

        public async Task UpdateEndpointWeightAsync(string endpointId, int weight)
        {
            if (_endpoints.TryGetValue(endpointId, out var endpoint))
            {
                endpoint.Weight = Math.Max(0, weight);
                _logger.LogInformation("更新端点 {EndpointId} 权重为 {Weight}", endpointId, weight);
            }
        }

        public async Task RecordRequestResultAsync(string endpointId, TimeSpan responseTime, bool success)
        {
            if (_endpointMetrics.TryGetValue(endpointId, out var metrics))
            {
                lock (metrics)
                {
                    metrics.TotalResponseTime = metrics.TotalResponseTime.Add(responseTime);
                    
                    if (success)
                    {
                        Interlocked.Increment(ref metrics.SuccessfulRequests);
                    }
                    else
                    {
                        Interlocked.Increment(ref metrics.FailedRequests);
                    }

                    // 更新平均响应时间
                    var totalRequests = metrics.SuccessfulRequests + metrics.FailedRequests;
                    if (totalRequests > 0)
                    {
                        metrics.AverageResponseTime = TimeSpan.FromMilliseconds(
                            metrics.TotalResponseTime.TotalMilliseconds / totalRequests);
                    }

                    // 更新最近响应时间（用于自适应算法）
                    metrics.RecentResponseTimes.Enqueue(responseTime.TotalMilliseconds);
                    while (metrics.RecentResponseTimes.Count > 100) // 保留最近100个
                    {
                        metrics.RecentResponseTimes.TryDequeue(out _);
                    }

                    // 如果启用了自适应权重调整
                    if (_options.EnableAdaptiveWeights)
                    {
                        UpdateAdaptiveWeight(endpointId, metrics);
                    }
                }

                _logger.LogDebug("记录端点 {EndpointId} 请求结果: 响应时间={ResponseTime}ms, 成功={Success}", 
                    endpointId, responseTime.TotalMilliseconds, success);
            }
        }

        public LoadBalancingStatistics GetStatistics()
        {
            var stats = new LoadBalancingStatistics
            {
                Algorithm = _options.Algorithm.ToString(),
                TotalRequests = _totalRequests,
                TotalEndpoints = _endpoints.Count,
                AvailableEndpoints = GetAvailableEndpoints().Count(),
                EndpointStatistics = new List<EndpointLoadStatistics>()
            };

            foreach (var endpoint in _endpoints.Values)
            {
                if (_endpointMetrics.TryGetValue(endpoint.Id, out var metrics))
                {
                    var successRate = metrics.RequestCount > 0 
                        ? (metrics.SuccessfulRequests / (double)metrics.RequestCount) * 100 
                        : 0;

                    stats.EndpointStatistics.Add(new EndpointLoadStatistics
                    {
                        EndpointId = endpoint.Id,
                        Address = endpoint.Address,
                        Weight = endpoint.Weight,
                        RequestCount = metrics.RequestCount,
                        SuccessfulRequests = metrics.SuccessfulRequests,
                        FailedRequests = metrics.FailedRequests,
                        SuccessRate = successRate,
                        AverageResponseTime = metrics.AverageResponseTime,
                        LoadPercentage = _totalRequests > 0 ? (metrics.RequestCount / (double)_totalRequests) * 100 : 0
                    });
                }
            }

            return stats;
        }

        public IEnumerable<ServiceEndpoint> GetEndpoints()
        {
            return _endpoints.Values.ToList();
        }

        private void InitializeEndpoints()
        {
            foreach (var endpoint in _options.Endpoints)
            {
                endpoint.Id = endpoint.Id ?? Guid.NewGuid().ToString();
                _endpoints[endpoint.Id] = endpoint;

                // 初始化端点指标
                _endpointMetrics[endpoint.Id] = new EndpointMetrics
                {
                    EndpointId = endpoint.Id
                };
            }
        }

        private IEnumerable<ServiceEndpoint> GetAvailableEndpoints()
        {
            return _endpoints.Values.Where(e => e.Status == EndpointStatus.Available);
        }

        private ILoadBalancingAlgorithm CreateAlgorithm(LoadBalancingAlgorithm algorithmType)
        {
            switch (algorithmType)
            {
                case LoadBalancingAlgorithm.RoundRobin:
                    return new RoundRobinAlgorithm(_logger);
                case LoadBalancingAlgorithm.WeightedRoundRobin:
                    return new WeightedRoundRobinAlgorithm(_logger);
                case LoadBalancingAlgorithm.Random:
                    return new RandomAlgorithm(_logger);
                case LoadBalancingAlgorithm.WeightedRandom:
                    return new WeightedRandomAlgorithm(_logger);
                case LoadBalancingAlgorithm.LeastConnections:
                    return new LeastConnectionsAlgorithm(_endpointMetrics, _logger);
                case LoadBalancingAlgorithm.LeastResponseTime:
                    return new LeastResponseTimeAlgorithm(_endpointMetrics, _logger);
                case LoadBalancingAlgorithm.HealthAware:
                    return new HealthAwareAlgorithm(_endpointMetrics, _logger);
                case LoadBalancingAlgorithm.ConsistentHash:
                    return new ConsistentHashAlgorithm(_logger);
                default:
                    return new RoundRobinAlgorithm(_logger);
            }
        }

        private void UpdateAdaptiveWeight(string endpointId, EndpointMetrics metrics)
        {
            if (!_endpoints.TryGetValue(endpointId, out var endpoint))
                return;

            // 基于性能动态调整权重
            var baseWeight = _options.BaseWeight;
            var performanceFactor = CalculatePerformanceFactor(metrics);
            var newWeight = Math.Max(1, (int)(baseWeight * performanceFactor));

            if (Math.Abs(endpoint.Weight - newWeight) > 1) // 避免频繁微调
            {
                endpoint.Weight = newWeight;
                _logger.LogDebug("自适应调整端点 {EndpointId} 权重: {OldWeight} -> {NewWeight}, 性能因子: {PerformanceFactor}", 
                    endpointId, endpoint.Weight, newWeight, performanceFactor);
            }
        }

        private double CalculatePerformanceFactor(EndpointMetrics metrics)
        {
            if (metrics.RequestCount == 0) return 1.0;

            // 成功率因子
            var successRate = metrics.SuccessfulRequests / (double)metrics.RequestCount;
            var successFactor = successRate;

            // 响应时间因子（响应时间越短，因子越大）
            var avgResponseMs = metrics.AverageResponseTime.TotalMilliseconds;
            var responseFactor = avgResponseMs > 0 ? Math.Min(2.0, 1000.0 / avgResponseMs) : 1.0;

            // 综合性能因子
            return (successFactor * 0.7 + responseFactor * 0.3);
        }
    }

    /// <summary>
    /// 负载均衡算法接口
    /// </summary>
    public interface ILoadBalancingAlgorithm
    {
        /// <summary>
        /// 选择端点
        /// </summary>
        /// <param name="endpoints">可用端点</param>
        /// <param name="context">上下文</param>
        /// <returns>选中的端点</returns>
        Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context);
    }

    /// <summary>
    /// 轮询算法
    /// </summary>
    public class RoundRobinAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;
        private int _currentIndex = -1;

        public RoundRobinAlgorithm(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var index = Interlocked.Increment(ref _currentIndex) % endpointList.Count;
            return Task.FromResult(endpointList[index]);
        }
    }

    /// <summary>
    /// 加权轮询算法
    /// </summary>
    public class WeightedRoundRobinAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, int> _currentWeights = new();

        public WeightedRoundRobinAlgorithm(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            ServiceEndpoint selected = null;
            int maxCurrentWeight = int.MinValue;
            int totalWeight = endpointList.Sum(e => e.Weight);

            foreach (var endpoint in endpointList)
            {
                var currentWeight = _currentWeights.AddOrUpdate(endpoint.Id, endpoint.Weight, (k, v) => v + endpoint.Weight);
                
                if (currentWeight > maxCurrentWeight)
                {
                    maxCurrentWeight = currentWeight;
                    selected = endpoint;
                }
            }

            if (selected != null)
            {
                _currentWeights.AddOrUpdate(selected.Id, 0, (k, v) => v - totalWeight);
            }

            return Task.FromResult(selected);
        }
    }

    /// <summary>
    /// 随机算法
    /// </summary>
    public class RandomAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;

        public RandomAlgorithm(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var index = Random.Shared.Next(endpointList.Count);
            return Task.FromResult(endpointList[index]);
        }
    }

    /// <summary>
    /// 加权随机算法
    /// </summary>
    public class WeightedRandomAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;

        public WeightedRandomAlgorithm(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var totalWeight = endpointList.Sum(e => e.Weight);
            if (totalWeight <= 0) return Task.FromResult(endpointList[Random.Shared.Next(endpointList.Count)]);

            var randomValue = Random.Shared.Next(totalWeight);
            var currentWeight = 0;

            foreach (var endpoint in endpointList)
            {
                currentWeight += endpoint.Weight;
                if (randomValue < currentWeight)
                {
                    return Task.FromResult(endpoint);
                }
            }

            return Task.FromResult(endpointList.Last());
        }
    }

    /// <summary>
    /// 最少连接算法
    /// </summary>
    public class LeastConnectionsAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, EndpointMetrics> _metrics;

        public LeastConnectionsAlgorithm(ConcurrentDictionary<string, EndpointMetrics> metrics, ILogger logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var selected = endpointList.OrderBy(endpoint =>
            {
                if (_metrics.TryGetValue(endpoint.Id, out var metrics))
                {
                    return metrics.ActiveConnections;
                }
                return 0;
            }).First();

            return Task.FromResult(selected);
        }
    }

    /// <summary>
    /// 最少响应时间算法
    /// </summary>
    public class LeastResponseTimeAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, EndpointMetrics> _metrics;

        public LeastResponseTimeAlgorithm(ConcurrentDictionary<string, EndpointMetrics> metrics, ILogger logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var selected = endpointList.OrderBy(endpoint =>
            {
                if (_metrics.TryGetValue(endpoint.Id, out var metrics))
                {
                    return metrics.AverageResponseTime.TotalMilliseconds;
                }
                return double.MaxValue;
            }).First();

            return Task.FromResult(selected);
        }
    }

    /// <summary>
    /// 健康感知算法
    /// </summary>
    public class HealthAwareAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, EndpointMetrics> _metrics;

        public HealthAwareAlgorithm(ConcurrentDictionary<string, EndpointMetrics> metrics, ILogger logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            var selected = endpointList.OrderByDescending(endpoint =>
            {
                if (_metrics.TryGetValue(endpoint.Id, out var metrics))
                {
                    var healthScore = CalculateHealthScore(metrics);
                    return healthScore * endpoint.Weight; // 结合权重
                }
                return endpoint.Weight;
            }).First();

            return Task.FromResult(selected);
        }

        private double CalculateHealthScore(EndpointMetrics metrics)
        {
            if (metrics.RequestCount == 0) return 1.0;

            var successRate = metrics.SuccessfulRequests / (double)metrics.RequestCount;
            var responseTimeFactor = Math.Min(1.0, 1000.0 / Math.Max(1, metrics.AverageResponseTime.TotalMilliseconds));
            
            return (successRate * 0.7 + responseTimeFactor * 0.3);
        }
    }

    /// <summary>
    /// 一致性哈希算法
    /// </summary>
    public class ConsistentHashAlgorithm : ILoadBalancingAlgorithm
    {
        private readonly ILogger _logger;

        public ConsistentHashAlgorithm(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServiceEndpoint> SelectEndpointAsync(IEnumerable<ServiceEndpoint> endpoints, LoadBalancingContext context)
        {
            var endpointList = endpoints.ToList();
            if (!endpointList.Any()) return Task.FromResult<ServiceEndpoint>(null);

            // 使用上下文中的键进行哈希
            var key = context?.HashKey ?? Guid.NewGuid().ToString();
            var hash = key.GetHashCode();
            var index = Math.Abs(hash) % endpointList.Count;

            return Task.FromResult(endpointList[index]);
        }
    }

    /// <summary>
    /// 端点指标
    /// </summary>
    public class EndpointMetrics
    {
        public string EndpointId { get; set; }
        public long RequestCount { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public int ActiveConnections { get; set; }
        public ConcurrentQueue<double> RecentResponseTimes { get; set; } = new ConcurrentQueue<double>();
    }

    /// <summary>
    /// 负载均衡配置选项
    /// </summary>
    public class LoadBalancingOptions
    {
        /// <summary>
        /// 负载均衡算法
        /// </summary>
        public LoadBalancingAlgorithm Algorithm { get; set; } = LoadBalancingAlgorithm.RoundRobin;

        /// <summary>
        /// 服务端点列表
        /// </summary>
        public List<ServiceEndpoint> Endpoints { get; set; } = new List<ServiceEndpoint>();

        /// <summary>
        /// 是否启用自适应权重调整
        /// </summary>
        public bool EnableAdaptiveWeights { get; set; } = false;

        /// <summary>
        /// 基础权重（用于自适应调整）
        /// </summary>
        public int BaseWeight { get; set; } = 10;

        /// <summary>
        /// 健康检查间隔
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 负载均衡算法枚举
    /// </summary>
    public enum LoadBalancingAlgorithm
    {
        /// <summary>
        /// 轮询
        /// </summary>
        RoundRobin,

        /// <summary>
        /// 加权轮询
        /// </summary>
        WeightedRoundRobin,

        /// <summary>
        /// 随机
        /// </summary>
        Random,

        /// <summary>
        /// 加权随机
        /// </summary>
        WeightedRandom,

        /// <summary>
        /// 最少连接
        /// </summary>
        LeastConnections,

        /// <summary>
        /// 最少响应时间
        /// </summary>
        LeastResponseTime,

        /// <summary>
        /// 健康感知
        /// </summary>
        HealthAware,

        /// <summary>
        /// 一致性哈希
        /// </summary>
        ConsistentHash
    }

    /// <summary>
    /// 负载均衡上下文
    /// </summary>
    public class LoadBalancingContext
    {
        /// <summary>
        /// 哈希键（用于一致性哈希）
        /// </summary>
        public string HashKey { get; set; }

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 请求类型
        /// </summary>
        public string RequestType { get; set; }

        /// <summary>
        /// 自定义属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 负载均衡统计信息
    /// </summary>
    public class LoadBalancingStatistics
    {
        /// <summary>
        /// 算法名称
        /// </summary>
        public string Algorithm { get; set; }

        /// <summary>
        /// 总请求数
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// 端点总数
        /// </summary>
        public int TotalEndpoints { get; set; }

        /// <summary>
        /// 可用端点数
        /// </summary>
        public int AvailableEndpoints { get; set; }

        /// <summary>
        /// 端点统计信息
        /// </summary>
        public List<EndpointLoadStatistics> EndpointStatistics { get; set; } = new List<EndpointLoadStatistics>();
    }

    /// <summary>
    /// 端点负载统计信息
    /// </summary>
    public class EndpointLoadStatistics
    {
        /// <summary>
        /// 端点ID
        /// </summary>
        public string EndpointId { get; set; }

        /// <summary>
        /// 端点地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 权重
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// 请求数
        /// </summary>
        public long RequestCount { get; set; }

        /// <summary>
        /// 成功请求数
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// 失败请求数
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均响应时间
        /// </summary>
        public TimeSpan AverageResponseTime { get; set; }

        /// <summary>
        /// 负载百分比
        /// </summary>
        public double LoadPercentage { get; set; }
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