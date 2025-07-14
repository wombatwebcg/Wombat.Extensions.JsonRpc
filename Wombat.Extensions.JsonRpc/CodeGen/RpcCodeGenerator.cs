using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.CodeGen.Generators;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.CodeGen
{
    /// <summary>
    /// RPC代码生成器实现
    /// </summary>
    public class RpcCodeGenerator : IRpcCodeGenerator
    {
        private readonly CSharpClientGenerator _csharpGenerator;
        private readonly TypeScriptClientGenerator _typeScriptGenerator;
        private readonly OpenApiDocumentGenerator _openApiGenerator;
        private readonly MarkdownDocumentGenerator _markdownGenerator;
        private readonly IRpcMetadataProvider _metadataProvider;
        private readonly ILogger<RpcCodeGenerator> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="metadataProvider">元数据提供程序</param>
        /// <param name="logger">日志记录器</param>
        public RpcCodeGenerator(
            IRpcMetadataProvider metadataProvider = null,
            ILogger<RpcCodeGenerator> logger = null)
        {
            _metadataProvider = metadataProvider ?? new DefaultRpcMetadataProvider();
            _logger = logger;
            
            _csharpGenerator = new CSharpClientGenerator(logger?.CreateLogger<CSharpClientGenerator>());
            _typeScriptGenerator = new TypeScriptClientGenerator(logger?.CreateLogger<TypeScriptClientGenerator>());
            _openApiGenerator = new OpenApiDocumentGenerator(logger?.CreateLogger<OpenApiDocumentGenerator>());
            _markdownGenerator = new MarkdownDocumentGenerator(logger?.CreateLogger<MarkdownDocumentGenerator>());
        }

        /// <summary>
        /// 生成C#客户端代理代码
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        public async Task<string> GenerateCSharpClientAsync(ServiceMetadata metadata, CodeGenerationOptions options = null)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            _logger?.LogInformation("开始生成C#客户端代码: {ServiceName}", metadata.ServiceName);

            try
            {
                var result = await _csharpGenerator.GenerateAsync(metadata, options);
                _logger?.LogInformation("C#客户端代码生成成功: {ServiceName}", metadata.ServiceName);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "C#客户端代码生成失败: {ServiceName}", metadata.ServiceName);
                throw;
            }
        }

        /// <summary>
        /// 生成C#客户端代理代码（泛型）
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        public async Task<string> GenerateCSharpClientAsync<T>(CodeGenerationOptions options = null) where T : class
        {
            var serviceType = typeof(T);
            _logger?.LogInformation("开始生成C#客户端代码: {ServiceType}", serviceType.Name);

            try
            {
                var metadata = _metadataProvider.ExtractServiceMetadata(serviceType);
                return await GenerateCSharpClientAsync(metadata, options);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "C#客户端代码生成失败: {ServiceType}", serviceType.Name);
                throw;
            }
        }

        /// <summary>
        /// 生成TypeScript客户端代码
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        public async Task<string> GenerateTypeScriptClientAsync(ServiceMetadata metadata, CodeGenerationOptions options = null)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            _logger?.LogInformation("开始生成TypeScript客户端代码: {ServiceName}", metadata.ServiceName);

            try
            {
                var result = await _typeScriptGenerator.GenerateClientAsync(metadata, options);
                _logger?.LogInformation("TypeScript客户端代码生成成功: {ServiceName}", metadata.ServiceName);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "TypeScript客户端代码生成失败: {ServiceName}", metadata.ServiceName);
                throw;
            }
        }

        /// <summary>
        /// 生成TypeScript类型定义
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的类型定义</returns>
        public async Task<string> GenerateTypeScriptDefinitionsAsync(ServiceMetadata metadata, CodeGenerationOptions options = null)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            _logger?.LogInformation("开始生成TypeScript类型定义: {ServiceName}", metadata.ServiceName);

            try
            {
                var result = await _typeScriptGenerator.GenerateDefinitionsAsync(metadata, options);
                _logger?.LogInformation("TypeScript类型定义生成成功: {ServiceName}", metadata.ServiceName);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "TypeScript类型定义生成失败: {ServiceName}", metadata.ServiceName);
                throw;
            }
        }

        /// <summary>
        /// 生成OpenAPI文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI文档</returns>
        public async Task<OpenApiDocument> GenerateOpenApiDocumentAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceList = services.ToList();
            _logger?.LogInformation("开始生成OpenAPI文档，服务数量: {Count}", serviceList.Count);

            try
            {
                var result = await _openApiGenerator.GenerateAsync(serviceList, options);
                _logger?.LogInformation("OpenAPI文档生成成功，服务数量: {Count}", serviceList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OpenAPI文档生成失败");
                throw;
            }
        }

        /// <summary>
        /// 生成OpenAPI JSON
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI JSON字符串</returns>
        public async Task<string> GenerateOpenApiJsonAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceList = services.ToList();
            _logger?.LogInformation("开始生成OpenAPI JSON，服务数量: {Count}", serviceList.Count);

            try
            {
                var result = await _openApiGenerator.GenerateJsonAsync(serviceList, options);
                _logger?.LogInformation("OpenAPI JSON生成成功，服务数量: {Count}", serviceList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OpenAPI JSON生成失败");
                throw;
            }
        }

        /// <summary>
        /// 生成Markdown文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Markdown文档</returns>
        public async Task<string> GenerateMarkdownDocumentationAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceList = services.ToList();
            _logger?.LogInformation("开始生成Markdown文档，服务数量: {Count}", serviceList.Count);

            try
            {
                var result = await _markdownGenerator.GenerateAsync(serviceList, options);
                _logger?.LogInformation("Markdown文档生成成功，服务数量: {Count}", serviceList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Markdown文档生成失败");
                throw;
            }
        }

        /// <summary>
        /// 生成服务器存根代码
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        public async Task<string> GenerateServerStubAsync(Type serviceType, CodeGenerationOptions options = null)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            _logger?.LogInformation("开始生成服务器存根代码: {ServiceType}", serviceType.Name);

            try
            {
                var metadata = _metadataProvider.ExtractServiceMetadata(serviceType);
                var result = await GenerateServerStubImplementation(metadata, options);
                _logger?.LogInformation("服务器存根代码生成成功: {ServiceType}", serviceType.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "服务器存根代码生成失败: {ServiceType}", serviceType.Name);
                throw;
            }
        }

        /// <summary>
        /// 生成Postman集合
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Postman集合JSON</returns>
        public async Task<string> GeneratePostmanCollectionAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceList = services.ToList();
            _logger?.LogInformation("开始生成Postman集合，服务数量: {Count}", serviceList.Count);

            try
            {
                var result = await GeneratePostmanCollectionImplementation(serviceList, options);
                _logger?.LogInformation("Postman集合生成成功，服务数量: {Count}", serviceList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Postman集合生成失败");
                throw;
            }
        }

        /// <summary>
        /// 批量生成所有代码
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成结果</returns>
        public async Task<CodeGenerationResult> GenerateAllAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceList = services.ToList();
            var stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("开始批量生成所有代码，服务数量: {Count}", serviceList.Count);

            var result = new CodeGenerationResult
            {
                Statistics = new CodeGenerationStatistics
                {
                    ServicesCount = serviceList.Count,
                    MethodsCount = serviceList.Sum(s => s.Methods.Length)
                }
            };

            try
            {
                var tasks = new List<Task>();

                // 并行生成各种代码
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (serviceList.Count == 1)
                        {
                            result.CSharpClientCode = await GenerateCSharpClientAsync(serviceList[0], options);
                        }
                        else
                        {
                            // 为多个服务生成组合的C#客户端代码
                            result.CSharpClientCode = await GenerateCombinedCSharpClientAsync(serviceList, options);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"C#客户端代码生成失败: {ex.Message}");
                        _logger?.LogError(ex, "C#客户端代码生成失败");
                    }
                }));

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (serviceList.Count == 1)
                        {
                            result.TypeScriptClientCode = await GenerateTypeScriptClientAsync(serviceList[0], options);
                            result.TypeScriptDefinitions = await GenerateTypeScriptDefinitionsAsync(serviceList[0], options);
                        }
                        else
                        {
                            // 为多个服务生成组合的TypeScript代码
                            result.TypeScriptClientCode = await GenerateCombinedTypeScriptClientAsync(serviceList, options);
                            result.TypeScriptDefinitions = await GenerateCombinedTypeScriptDefinitionsAsync(serviceList, options);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"TypeScript代码生成失败: {ex.Message}");
                        _logger?.LogError(ex, "TypeScript代码生成失败");
                    }
                }));

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        result.OpenApiDocument = await GenerateOpenApiDocumentAsync(serviceList, options);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"OpenAPI文档生成失败: {ex.Message}");
                        _logger?.LogError(ex, "OpenAPI文档生成失败");
                    }
                }));

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        result.MarkdownDocumentation = await GenerateMarkdownDocumentationAsync(serviceList, options);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Markdown文档生成失败: {ex.Message}");
                        _logger?.LogError(ex, "Markdown文档生成失败");
                    }
                }));

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        result.PostmanCollection = await GeneratePostmanCollectionAsync(serviceList, options);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Postman集合生成失败: {ex.Message}");
                        _logger?.LogError(ex, "Postman集合生成失败");
                    }
                }));

                await Task.WhenAll(tasks);

                stopwatch.Stop();
                result.Statistics.GenerationTime = stopwatch.Elapsed;
                result.Statistics.FilesCount = GetGeneratedFilesCount(result);
                result.Statistics.LinesOfCode = GetLinesOfCode(result);

                _logger?.LogInformation("批量代码生成完成，耗时: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Errors.Add($"批量代码生成失败: {ex.Message}");
                result.Statistics.GenerationTime = stopwatch.Elapsed;
                _logger?.LogError(ex, "批量代码生成失败");
                return result;
            }
        }

        /// <summary>
        /// 生成组合的C#客户端代码
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>组合的C#代码</returns>
        private async Task<string> GenerateCombinedCSharpClientAsync(List<ServiceMetadata> services, CodeGenerationOptions options)
        {
            var combinedCode = new System.Text.StringBuilder();
            
            // 生成文件头和using语句
            combinedCode.AppendLine("// <auto-generated>");
            combinedCode.AppendLine($"// Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            combinedCode.AppendLine("// Generator: Wombat.Extensions.JsonRpc.CodeGen");
            combinedCode.AppendLine("// </auto-generated>");
            combinedCode.AppendLine();
            
            combinedCode.AppendLine("using System;");
            combinedCode.AppendLine("using System.Threading.Tasks;");
            combinedCode.AppendLine("using System.ComponentModel.DataAnnotations;");
            combinedCode.AppendLine("using Wombat.Extensions.JsonRpc.Client;");
            combinedCode.AppendLine("using Wombat.Extensions.JsonRpc.Contracts;");
            combinedCode.AppendLine();
            
            combinedCode.AppendLine($"namespace {options?.Namespace ?? "Generated"}");
            combinedCode.AppendLine("{");

            foreach (var service in services)
            {
                var serviceCode = await _csharpGenerator.GenerateAsync(service, options);
                // 提取类定义部分（去掉文件头和命名空间）
                var lines = serviceCode.Split('\n');
                var inNamespace = false;
                var braceCount = 0;
                
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("namespace "))
                    {
                        inNamespace = true;
                        continue;
                    }
                    
                    if (inNamespace)
                    {
                        if (line.Contains("{"))
                            braceCount++;
                        if (line.Contains("}"))
                            braceCount--;
                            
                        if (braceCount > 0)
                        {
                            combinedCode.AppendLine(line);
                        }
                        else if (braceCount == 0 && line.Trim() == "}")
                        {
                            // 跳过命名空间结束括号
                            continue;
                        }
                    }
                }
                
                combinedCode.AppendLine();
            }
            
            combinedCode.AppendLine("}");
            
            return combinedCode.ToString();
        }

        /// <summary>
        /// 生成组合的TypeScript客户端代码
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>组合的TypeScript代码</returns>
        private async Task<string> GenerateCombinedTypeScriptClientAsync(List<ServiceMetadata> services, CodeGenerationOptions options)
        {
            var combinedCode = new System.Text.StringBuilder();
            
            // 生成文件头
            combinedCode.AppendLine("// This file was auto-generated.");
            combinedCode.AppendLine($"// Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            combinedCode.AppendLine("// Generator: Wombat.Extensions.JsonRpc.CodeGen");
            combinedCode.AppendLine();

            foreach (var service in services)
            {
                var serviceCode = await _typeScriptGenerator.GenerateClientAsync(service, options);
                combinedCode.AppendLine(serviceCode);
                combinedCode.AppendLine();
            }
            
            return combinedCode.ToString();
        }

        /// <summary>
        /// 生成组合的TypeScript类型定义
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>组合的TypeScript类型定义</returns>
        private async Task<string> GenerateCombinedTypeScriptDefinitionsAsync(List<ServiceMetadata> services, CodeGenerationOptions options)
        {
            var combinedDefinitions = new System.Text.StringBuilder();
            
            // 生成文件头
            combinedDefinitions.AppendLine("// This file was auto-generated.");
            combinedDefinitions.AppendLine($"// Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            combinedDefinitions.AppendLine("// Generator: Wombat.Extensions.JsonRpc.CodeGen");
            combinedDefinitions.AppendLine();

            foreach (var service in services)
            {
                var definitions = await _typeScriptGenerator.GenerateDefinitionsAsync(service, options);
                combinedDefinitions.AppendLine(definitions);
                combinedDefinitions.AppendLine();
            }
            
            return combinedDefinitions.ToString();
        }

        /// <summary>
        /// 生成服务器存根实现
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>服务器存根代码</returns>
        private async Task<string> GenerateServerStubImplementation(ServiceMetadata metadata, CodeGenerationOptions options)
        {
            // 这里可以实现服务器存根代码生成逻辑
            // 为了简化示例，返回一个基本的存根
            return $"// 服务器存根代码 for {metadata.ServiceName}\n// TODO: 实现服务器存根生成逻辑";
        }

        /// <summary>
        /// 生成Postman集合实现
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Postman集合JSON</returns>
        private async Task<string> GeneratePostmanCollectionImplementation(List<ServiceMetadata> services, CodeGenerationOptions options)
        {
            // 这里可以实现Postman集合生成逻辑
            // 为了简化示例，返回一个基本的集合
            return $"{{\"info\": {{\"name\": \"RPC API Collection\", \"description\": \"Auto-generated collection for {services.Count} services\"}}}}";
        }

        /// <summary>
        /// 获取生成的文件数量
        /// </summary>
        /// <param name="result">生成结果</param>
        /// <returns>文件数量</returns>
        private int GetGeneratedFilesCount(CodeGenerationResult result)
        {
            var count = 0;
            if (!string.IsNullOrEmpty(result.CSharpClientCode)) count++;
            if (!string.IsNullOrEmpty(result.TypeScriptClientCode)) count++;
            if (!string.IsNullOrEmpty(result.TypeScriptDefinitions)) count++;
            if (result.OpenApiDocument != null) count++;
            if (!string.IsNullOrEmpty(result.MarkdownDocumentation)) count++;
            if (!string.IsNullOrEmpty(result.PostmanCollection)) count++;
            return count;
        }

        /// <summary>
        /// 获取代码行数
        /// </summary>
        /// <param name="result">生成结果</param>
        /// <returns>代码行数</returns>
        private int GetLinesOfCode(CodeGenerationResult result)
        {
            var lines = 0;
            if (!string.IsNullOrEmpty(result.CSharpClientCode))
                lines += result.CSharpClientCode.Split('\n').Length;
            if (!string.IsNullOrEmpty(result.TypeScriptClientCode))
                lines += result.TypeScriptClientCode.Split('\n').Length;
            if (!string.IsNullOrEmpty(result.TypeScriptDefinitions))
                lines += result.TypeScriptDefinitions.Split('\n').Length;
            if (!string.IsNullOrEmpty(result.MarkdownDocumentation))
                lines += result.MarkdownDocumentation.Split('\n').Length;
            return lines;
        }
    }
} 