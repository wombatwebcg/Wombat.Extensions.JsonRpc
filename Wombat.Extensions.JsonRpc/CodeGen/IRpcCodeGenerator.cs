using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.CodeGen
{
    /// <summary>
    /// RPC代码生成器接口
    /// </summary>
    public interface IRpcCodeGenerator
    {
        /// <summary>
        /// 生成C#客户端代理代码
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        Task<string> GenerateCSharpClientAsync(ServiceMetadata metadata, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成C#客户端代理代码（泛型）
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        Task<string> GenerateCSharpClientAsync<T>(CodeGenerationOptions options = null) where T : class;

        /// <summary>
        /// 生成TypeScript客户端代码
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        Task<string> GenerateTypeScriptClientAsync(ServiceMetadata metadata, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成TypeScript类型定义
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的类型定义</returns>
        Task<string> GenerateTypeScriptDefinitionsAsync(ServiceMetadata metadata, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成OpenAPI文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI文档</returns>
        Task<OpenApiDocument> GenerateOpenApiDocumentAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成OpenAPI JSON
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI JSON字符串</returns>
        Task<string> GenerateOpenApiJsonAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成Markdown文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Markdown文档</returns>
        Task<string> GenerateMarkdownDocumentationAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成服务器存根代码
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        Task<string> GenerateServerStubAsync(Type serviceType, CodeGenerationOptions options = null);

        /// <summary>
        /// 生成Postman集合
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Postman集合JSON</returns>
        Task<string> GeneratePostmanCollectionAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null);

        /// <summary>
        /// 批量生成所有代码
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成结果</returns>
        Task<CodeGenerationResult> GenerateAllAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null);
    }

    /// <summary>
    /// 代码生成选项
    /// </summary>
    public class CodeGenerationOptions
    {
        /// <summary>
        /// 命名空间
        /// </summary>
        public string Namespace { get; set; } = "Generated";

        /// <summary>
        /// 类名前缀
        /// </summary>
        public string ClassPrefix { get; set; } = string.Empty;

        /// <summary>
        /// 类名后缀
        /// </summary>
        public string ClassSuffix { get; set; } = "Client";

        /// <summary>
        /// 是否生成异步方法
        /// </summary>
        public bool GenerateAsyncMethods { get; set; } = true;

        /// <summary>
        /// 是否生成同步方法
        /// </summary>
        public bool GenerateSyncMethods { get; set; } = false;

        /// <summary>
        /// 是否包含注释
        /// </summary>
        public bool IncludeDocumentation { get; set; } = true;

        /// <summary>
        /// 是否包含参数验证
        /// </summary>
        public bool IncludeValidation { get; set; } = true;

        /// <summary>
        /// 是否包含异常处理
        /// </summary>
        public bool IncludeExceptionHandling { get; set; } = true;

        /// <summary>
        /// 输出格式
        /// </summary>
        public CodeOutputFormat OutputFormat { get; set; } = CodeOutputFormat.SingleFile;

        /// <summary>
        /// 服务器基础URL
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:8080";

        /// <summary>
        /// API版本
        /// </summary>
        public string ApiVersion { get; set; } = "v1";

        /// <summary>
        /// API标题
        /// </summary>
        public string ApiTitle { get; set; } = "RPC API";

        /// <summary>
        /// API描述
        /// </summary>
        public string ApiDescription { get; set; } = "Auto-generated RPC API documentation";

        /// <summary>
        /// 作者信息
        /// </summary>
        public string Author { get; set; } = "Auto-generated";

        /// <summary>
        /// 自定义模板路径
        /// </summary>
        public string CustomTemplatePath { get; set; }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public Dictionary<string, object> ExtendedProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// TypeScript选项
        /// </summary>
        public TypeScriptGenerationOptions TypeScriptOptions { get; set; } = new TypeScriptGenerationOptions();

        /// <summary>
        /// OpenAPI选项
        /// </summary>
        public OpenApiGenerationOptions OpenApiOptions { get; set; } = new OpenApiGenerationOptions();

        /// <summary>
        /// 创建默认选项
        /// </summary>
        /// <returns>默认选项</returns>
        public static CodeGenerationOptions CreateDefault()
        {
            return new CodeGenerationOptions();
        }

        /// <summary>
        /// 创建TypeScript选项
        /// </summary>
        /// <returns>TypeScript选项</returns>
        public static CodeGenerationOptions CreateTypeScriptOptions()
        {
            return new CodeGenerationOptions
            {
                Namespace = "RpcClient",
                ClassSuffix = "Client",
                GenerateAsyncMethods = true,
                GenerateSyncMethods = false,
                IncludeDocumentation = true,
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    UseESModules = true,
                    GenerateInterfaces = true,
                    UsePromises = true
                }
            };
        }

        /// <summary>
        /// 创建文档生成选项
        /// </summary>
        /// <returns>文档生成选项</returns>
        public static CodeGenerationOptions CreateDocumentationOptions()
        {
            return new CodeGenerationOptions
            {
                IncludeDocumentation = true,
                ApiTitle = "RPC API Documentation",
                ApiDescription = "Complete API documentation for RPC services",
                OpenApiOptions = new OpenApiGenerationOptions
                {
                    IncludeExamples = true,
                    IncludeSchemas = true,
                    GenerateComponents = true
                }
            };
        }
    }

    /// <summary>
    /// TypeScript生成选项
    /// </summary>
    public class TypeScriptGenerationOptions
    {
        /// <summary>
        /// 是否使用ES模块
        /// </summary>
        public bool UseESModules { get; set; } = true;

        /// <summary>
        /// 是否生成接口
        /// </summary>
        public bool GenerateInterfaces { get; set; } = true;

        /// <summary>
        /// 是否使用Promise
        /// </summary>
        public bool UsePromises { get; set; } = true;

        /// <summary>
        /// 是否使用严格类型
        /// </summary>
        public bool UseStrictTypes { get; set; } = true;

        /// <summary>
        /// 缩进大小
        /// </summary>
        public int IndentSize { get; set; } = 2;

        /// <summary>
        /// 是否使用分号
        /// </summary>
        public bool UseSemicolons { get; set; } = true;

        /// <summary>
        /// HTTP客户端库
        /// </summary>
        public TypeScriptHttpClient HttpClient { get; set; } = TypeScriptHttpClient.Fetch;
    }

    /// <summary>
    /// OpenAPI生成选项
    /// </summary>
    public class OpenApiGenerationOptions
    {
        /// <summary>
        /// OpenAPI版本
        /// </summary>
        public string OpenApiVersion { get; set; } = "3.0.3";

        /// <summary>
        /// 是否包含示例
        /// </summary>
        public bool IncludeExamples { get; set; } = true;

        /// <summary>
        /// 是否包含Schema
        /// </summary>
        public bool IncludeSchemas { get; set; } = true;

        /// <summary>
        /// 是否生成组件
        /// </summary>
        public bool GenerateComponents { get; set; } = true;

        /// <summary>
        /// 服务器列表
        /// </summary>
        public List<OpenApiServer> Servers { get; set; } = new List<OpenApiServer>();

        /// <summary>
        /// 安全方案
        /// </summary>
        public List<OpenApiSecurityScheme> SecuritySchemes { get; set; } = new List<OpenApiSecurityScheme>();
    }

    /// <summary>
    /// 代码输出格式
    /// </summary>
    public enum CodeOutputFormat
    {
        /// <summary>
        /// 单文件
        /// </summary>
        SingleFile,

        /// <summary>
        /// 多文件
        /// </summary>
        MultipleFiles,

        /// <summary>
        /// 压缩包
        /// </summary>
        Archive
    }

    /// <summary>
    /// TypeScript HTTP客户端
    /// </summary>
    public enum TypeScriptHttpClient
    {
        /// <summary>
        /// Fetch API
        /// </summary>
        Fetch,

        /// <summary>
        /// Axios
        /// </summary>
        Axios,

        /// <summary>
        /// 自定义
        /// </summary>
        Custom
    }

    /// <summary>
    /// 代码生成结果
    /// </summary>
    public class CodeGenerationResult
    {
        /// <summary>
        /// C#客户端代码
        /// </summary>
        public string CSharpClientCode { get; set; }

        /// <summary>
        /// TypeScript客户端代码
        /// </summary>
        public string TypeScriptClientCode { get; set; }

        /// <summary>
        /// TypeScript类型定义
        /// </summary>
        public string TypeScriptDefinitions { get; set; }

        /// <summary>
        /// OpenAPI文档
        /// </summary>
        public OpenApiDocument OpenApiDocument { get; set; }

        /// <summary>
        /// Markdown文档
        /// </summary>
        public string MarkdownDocumentation { get; set; }

        /// <summary>
        /// Postman集合
        /// </summary>
        public string PostmanCollection { get; set; }

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 生成统计
        /// </summary>
        public CodeGenerationStatistics Statistics { get; set; } = new CodeGenerationStatistics();

        /// <summary>
        /// 错误信息
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 代码生成统计
    /// </summary>
    public class CodeGenerationStatistics
    {
        /// <summary>
        /// 生成的服务数
        /// </summary>
        public int ServicesCount { get; set; }

        /// <summary>
        /// 生成的方法数
        /// </summary>
        public int MethodsCount { get; set; }

        /// <summary>
        /// 生成的文件数
        /// </summary>
        public int FilesCount { get; set; }

        /// <summary>
        /// 生成的代码行数
        /// </summary>
        public int LinesOfCode { get; set; }

        /// <summary>
        /// 生成耗时
        /// </summary>
        public TimeSpan GenerationTime { get; set; }
    }

    /// <summary>
    /// OpenAPI文档
    /// </summary>
    public class OpenApiDocument
    {
        /// <summary>
        /// OpenAPI版本
        /// </summary>
        public string OpenApi { get; set; } = "3.0.3";

        /// <summary>
        /// 文档信息
        /// </summary>
        public OpenApiInfo Info { get; set; } = new OpenApiInfo();

        /// <summary>
        /// 服务器列表
        /// </summary>
        public List<OpenApiServer> Servers { get; set; } = new List<OpenApiServer>();

        /// <summary>
        /// 路径定义
        /// </summary>
        public Dictionary<string, OpenApiPathItem> Paths { get; set; } = new Dictionary<string, OpenApiPathItem>();

        /// <summary>
        /// 组件定义
        /// </summary>
        public OpenApiComponents Components { get; set; } = new OpenApiComponents();

        /// <summary>
        /// 安全要求
        /// </summary>
        public List<OpenApiSecurityRequirement> Security { get; set; } = new List<OpenApiSecurityRequirement>();
    }

    /// <summary>
    /// OpenAPI信息
    /// </summary>
    public class OpenApiInfo
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = "RPC API";

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 联系信息
        /// </summary>
        public OpenApiContact Contact { get; set; }

        /// <summary>
        /// 许可证信息
        /// </summary>
        public OpenApiLicense License { get; set; }
    }

    /// <summary>
    /// OpenAPI服务器
    /// </summary>
    public class OpenApiServer
    {
        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// OpenAPI路径项
    /// </summary>
    public class OpenApiPathItem
    {
        /// <summary>
        /// POST操作
        /// </summary>
        public OpenApiOperation Post { get; set; }
    }

    /// <summary>
    /// OpenAPI操作
    /// </summary>
    public class OpenApiOperation
    {
        /// <summary>
        /// 操作ID
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// 摘要
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 标签
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 请求体
        /// </summary>
        public OpenApiRequestBody RequestBody { get; set; }

        /// <summary>
        /// 响应
        /// </summary>
        public Dictionary<string, OpenApiResponse> Responses { get; set; } = new Dictionary<string, OpenApiResponse>();
    }

    /// <summary>
    /// OpenAPI请求体
    /// </summary>
    public class OpenApiRequestBody
    {
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public Dictionary<string, OpenApiMediaType> Content { get; set; } = new Dictionary<string, OpenApiMediaType>();

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool Required { get; set; } = true;
    }

    /// <summary>
    /// OpenAPI响应
    /// </summary>
    public class OpenApiResponse
    {
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public Dictionary<string, OpenApiMediaType> Content { get; set; } = new Dictionary<string, OpenApiMediaType>();
    }

    /// <summary>
    /// OpenAPI媒体类型
    /// </summary>
    public class OpenApiMediaType
    {
        /// <summary>
        /// Schema
        /// </summary>
        public object Schema { get; set; }

        /// <summary>
        /// 示例
        /// </summary>
        public object Example { get; set; }
    }

    /// <summary>
    /// OpenAPI组件
    /// </summary>
    public class OpenApiComponents
    {
        /// <summary>
        /// Schema定义
        /// </summary>
        public Dictionary<string, object> Schemas { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 安全方案
        /// </summary>
        public Dictionary<string, OpenApiSecurityScheme> SecuritySchemes { get; set; } = new Dictionary<string, OpenApiSecurityScheme>();
    }

    /// <summary>
    /// OpenAPI安全方案
    /// </summary>
    public class OpenApiSecurityScheme
    {
        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 方案
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// Bearer格式
        /// </summary>
        public string BearerFormat { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// OpenAPI安全要求
    /// </summary>
    public class OpenApiSecurityRequirement : Dictionary<string, List<string>>
    {
    }

    /// <summary>
    /// OpenAPI联系信息
    /// </summary>
    public class OpenApiContact
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; }
    }

    /// <summary>
    /// OpenAPI许可证
    /// </summary>
    public class OpenApiLicense
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; }
    }
} 