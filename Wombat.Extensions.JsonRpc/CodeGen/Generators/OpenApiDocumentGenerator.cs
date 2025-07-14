using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.CodeGen.Generators
{
    /// <summary>
    /// OpenAPI文档生成器
    /// </summary>
    public class OpenApiDocumentGenerator
    {
        private readonly ILogger<OpenApiDocumentGenerator> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public OpenApiDocumentGenerator(ILogger<OpenApiDocumentGenerator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 生成OpenAPI文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI文档</returns>
        public async Task<OpenApiDocument> GenerateAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            options = options ?? CodeGenerationOptions.CreateDocumentationOptions();
            
            _logger?.LogDebug("开始生成OpenAPI文档，服务数量: {Count}", services.Count());

            var document = new OpenApiDocument
            {
                OpenApi = options.OpenApiOptions.OpenApiVersion,
                Info = CreateApiInfo(options),
                Servers = CreateServers(options),
                Paths = new Dictionary<string, OpenApiPathItem>(),
                Components = new OpenApiComponents()
            };

            // 生成路径
            foreach (var service in services)
            {
                GenerateServicePaths(document, service, options);
            }

            // 生成组件
            if (options.OpenApiOptions.GenerateComponents)
            {
                GenerateComponents(document, services, options);
            }

            // 生成安全方案
            GenerateSecuritySchemes(document, options);

            _logger?.LogDebug("OpenAPI文档生成完成，路径数量: {PathCount}", document.Paths.Count);

            return document;
        }

        /// <summary>
        /// 生成OpenAPI JSON
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI JSON字符串</returns>
        public async Task<string> GenerateJsonAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            var document = await GenerateAsync(services, options);
            
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            return JsonConvert.SerializeObject(document, settings);
        }

        /// <summary>
        /// 创建API信息
        /// </summary>
        /// <param name="options">生成选项</param>
        /// <returns>API信息</returns>
        private OpenApiInfo CreateApiInfo(CodeGenerationOptions options)
        {
            return new OpenApiInfo
            {
                Title = options.ApiTitle,
                Description = options.ApiDescription,
                Version = options.ApiVersion,
                Contact = new OpenApiContact
                {
                    Name = options.Author
                }
            };
        }

        /// <summary>
        /// 创建服务器列表
        /// </summary>
        /// <param name="options">生成选项</param>
        /// <returns>服务器列表</returns>
        private List<OpenApiServer> CreateServers(CodeGenerationOptions options)
        {
            var servers = new List<OpenApiServer>();

            if (options.OpenApiOptions.Servers?.Count > 0)
            {
                servers.AddRange(options.OpenApiOptions.Servers);
            }
            else
            {
                servers.Add(new OpenApiServer
                {
                    Url = options.BaseUrl,
                    Description = "默认服务器"
                });
            }

            return servers;
        }

        /// <summary>
        /// 生成服务路径
        /// </summary>
        /// <param name="document">OpenAPI文档</param>
        /// <param name="service">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateServicePaths(OpenApiDocument document, ServiceMetadata service, CodeGenerationOptions options)
        {
            foreach (var method in service.Methods)
            {
                var path = $"/rpc/{service.ServiceName}/{method.MethodName}";
                
                var pathItem = new OpenApiPathItem
                {
                    Post = CreateOperation(method, service, options)
                };

                document.Paths[path] = pathItem;
            }
        }

        /// <summary>
        /// 创建操作
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <param name="service">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>OpenAPI操作</returns>
        private OpenApiOperation CreateOperation(MethodMetadata method, ServiceMetadata service, CodeGenerationOptions options)
        {
            var operation = new OpenApiOperation
            {
                OperationId = $"{service.ServiceName}_{method.MethodName}",
                Summary = method.DisplayName ?? method.MethodName,
                Description = method.Description,
                Tags = new List<string> { service.ServiceName },
                RequestBody = CreateRequestBody(method, options),
                Responses = CreateResponses(method, options)
            };

            // 添加安全要求
            if (method.RequireAuthentication)
            {
                operation.Security = new List<OpenApiSecurityRequirement>();
                // 这里可以根据实际的认证方案添加安全要求
            }

            return operation;
        }

        /// <summary>
        /// 创建请求体
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>请求体</returns>
        private OpenApiRequestBody CreateRequestBody(MethodMetadata method, CodeGenerationOptions options)
        {
            var requestBody = new OpenApiRequestBody
            {
                Description = "JSON-RPC请求",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>()
            };

            var schema = CreateJsonRpcRequestSchema(method);
            var mediaType = new OpenApiMediaType
            {
                Schema = schema
            };

            if (options.OpenApiOptions.IncludeExamples)
            {
                mediaType.Example = CreateRequestExample(method);
            }

            requestBody.Content["application/json"] = mediaType;

            return requestBody;
        }

        /// <summary>
        /// 创建响应
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>响应字典</returns>
        private Dictionary<string, OpenApiResponse> CreateResponses(MethodMetadata method, CodeGenerationOptions options)
        {
            var responses = new Dictionary<string, OpenApiResponse>();

            // 成功响应
            var successResponse = new OpenApiResponse
            {
                Description = method.IsNotification ? "通知已发送" : "操作成功",
                Content = new Dictionary<string, OpenApiMediaType>()
            };

            if (!method.IsNotification)
            {
                var schema = CreateJsonRpcResponseSchema(method);
                var mediaType = new OpenApiMediaType
                {
                    Schema = schema
                };

                if (options.OpenApiOptions.IncludeExamples)
                {
                    mediaType.Example = CreateResponseExample(method);
                }

                successResponse.Content["application/json"] = mediaType;
            }

            responses["200"] = successResponse;

            // 错误响应
            responses["400"] = new OpenApiResponse
            {
                Description = "请求错误",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = CreateErrorResponseSchema()
                    }
                }
            };

            responses["500"] = new OpenApiResponse
            {
                Description = "服务器错误",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = CreateErrorResponseSchema()
                    }
                }
            };

            return responses;
        }

        /// <summary>
        /// 创建JSON-RPC请求Schema
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>Schema对象</returns>
        private object CreateJsonRpcRequestSchema(MethodMetadata method)
        {
            var schema = new
            {
                type = "object",
                required = new[] { "jsonrpc", "method" },
                properties = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new { type = "string", @enum = new[] { "2.0" } },
                    ["method"] = new { type = "string", @enum = new[] { method.MethodName } },
                    ["id"] = new { type = "string" }
                }
            };

            if (method.Parameters.Length > 0)
            {
                var parametersSchema = CreateParametersSchema(method);
                ((Dictionary<string, object>)schema.properties)["params"] = parametersSchema;
            }

            return schema;
        }

        /// <summary>
        /// 创建JSON-RPC响应Schema
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>Schema对象</returns>
        private object CreateJsonRpcResponseSchema(MethodMetadata method)
        {
            var properties = new Dictionary<string, object>
            {
                ["jsonrpc"] = new { type = "string", @enum = new[] { "2.0" } },
                ["id"] = new { type = "string" }
            };

            if (!method.IsNotification && method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
            {
                var resultSchema = CreateTypeSchema(GetActualReturnType(method.ReturnType));
                properties["result"] = resultSchema;
            }

            properties["error"] = CreateErrorSchema();

            return new
            {
                type = "object",
                properties = properties
            };
        }

        /// <summary>
        /// 创建参数Schema
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>Schema对象</returns>
        private object CreateParametersSchema(MethodMetadata method)
        {
            if (method.Parameters.Length == 1)
            {
                // 单个参数直接作为params
                return CreateTypeSchema(method.Parameters[0].Type);
            }
            else
            {
                // 多个参数作为数组
                return new
                {
                    type = "array",
                    items = new
                    {
                        oneOf = method.Parameters.Select(p => CreateTypeSchema(p.Type)).ToArray()
                    }
                };
            }
        }

        /// <summary>
        /// 创建类型Schema
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>Schema对象</returns>
        private object CreateTypeSchema(Type type)
        {
            if (type == typeof(string))
                return new { type = "string" };
            if (type == typeof(int) || type == typeof(long))
                return new { type = "integer" };
            if (type == typeof(double) || type == typeof(decimal))
                return new { type = "number" };
            if (type == typeof(bool))
                return new { type = "boolean" };
            if (type == typeof(DateTime))
                return new { type = "string", format = "date-time" };
            if (type == typeof(Guid))
                return new { type = "string", format = "uuid" };

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return new
                {
                    type = "array",
                    items = CreateTypeSchema(elementType)
                };
            }

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return new
                    {
                        type = "array",
                        items = CreateTypeSchema(elementType)
                    };
                }

                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    var underlyingType = type.GetGenericArguments()[0];
                    var underlyingSchema = CreateTypeSchema(underlyingType);
                    return new
                    {
                        oneOf = new[] { underlyingSchema, new { type = "null" } }
                    };
                }
            }

            if (type.IsClass && type != typeof(string))
            {
                return new
                {
                    type = "object",
                    @ref = $"#/components/schemas/{type.Name}"
                };
            }

            return new { type = "object" };
        }

        /// <summary>
        /// 创建错误Schema
        /// </summary>
        /// <returns>错误Schema</returns>
        private object CreateErrorSchema()
        {
            return new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["code"] = new { type = "integer" },
                    ["message"] = new { type = "string" },
                    ["data"] = new { type = "object" }
                },
                required = new[] { "code", "message" }
            };
        }

        /// <summary>
        /// 创建错误响应Schema
        /// </summary>
        /// <returns>错误响应Schema</returns>
        private object CreateErrorResponseSchema()
        {
            return new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new { type = "string", @enum = new[] { "2.0" } },
                    ["error"] = CreateErrorSchema(),
                    ["id"] = new { type = "string" }
                },
                required = new[] { "jsonrpc", "error" }
            };
        }

        /// <summary>
        /// 生成组件
        /// </summary>
        /// <param name="document">OpenAPI文档</param>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        private void GenerateComponents(OpenApiDocument document, IEnumerable<ServiceMetadata> services, CodeGenerationOptions options)
        {
            var typeSet = new HashSet<Type>();

            // 收集所有类型
            foreach (var service in services)
            {
                foreach (var method in service.Methods)
                {
                    foreach (var param in method.Parameters)
                    {
                        CollectType(param.Type, typeSet);
                    }

                    if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
                    {
                        CollectType(GetActualReturnType(method.ReturnType), typeSet);
                    }
                }
            }

            // 生成Schema定义
            foreach (var type in typeSet.Where(t => !IsBuiltInType(t) && t.IsClass))
            {
                document.Components.Schemas[type.Name] = CreateDetailedTypeSchema(type);
            }
        }

        /// <summary>
        /// 创建详细类型Schema
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>详细Schema</returns>
        private object CreateDetailedTypeSchema(Type type)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            var typeProperties = type.GetProperties();
            foreach (var property in typeProperties)
            {
                properties[ToCamelCase(property.Name)] = CreateTypeSchema(property.PropertyType);
                
                if (!IsNullableType(property.PropertyType))
                {
                    required.Add(ToCamelCase(property.Name));
                }
            }

            var schema = new
            {
                type = "object",
                properties = properties
            };

            if (required.Count > 0)
            {
                return new
                {
                    type = "object",
                    properties = properties,
                    required = required.ToArray()
                };
            }

            return schema;
        }

        /// <summary>
        /// 生成安全方案
        /// </summary>
        /// <param name="document">OpenAPI文档</param>
        /// <param name="options">生成选项</param>
        private void GenerateSecuritySchemes(OpenApiDocument document, CodeGenerationOptions options)
        {
            // JWT Bearer认证
            document.Components.SecuritySchemes["bearerAuth"] = new OpenApiSecurityScheme
            {
                Type = "http",
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT认证令牌"
            };

            // API Key认证
            document.Components.SecuritySchemes["apiKeyAuth"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                Scheme = "apiKey",
                Description = "API Key认证"
            };
        }

        /// <summary>
        /// 创建请求示例
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>请求示例</returns>
        private object CreateRequestExample(MethodMetadata method)
        {
            var example = new
            {
                jsonrpc = "2.0",
                method = method.MethodName,
                id = "1"
            };

            if (method.Parameters.Length > 0)
            {
                var paramsExample = CreateParametersExample(method);
                return new
                {
                    jsonrpc = "2.0",
                    method = method.MethodName,
                    @params = paramsExample,
                    id = "1"
                };
            }

            return example;
        }

        /// <summary>
        /// 创建响应示例
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>响应示例</returns>
        private object CreateResponseExample(MethodMetadata method)
        {
            if (method.IsNotification)
            {
                return null;
            }

            var example = new
            {
                jsonrpc = "2.0",
                id = "1"
            };

            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
            {
                var resultExample = CreateTypeExample(GetActualReturnType(method.ReturnType));
                return new
                {
                    jsonrpc = "2.0",
                    result = resultExample,
                    id = "1"
                };
            }

            return example;
        }

        /// <summary>
        /// 创建参数示例
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>参数示例</returns>
        private object CreateParametersExample(MethodMetadata method)
        {
            if (method.Parameters.Length == 1)
            {
                return CreateTypeExample(method.Parameters[0].Type);
            }
            else
            {
                return method.Parameters.Select(p => CreateTypeExample(p.Type)).ToArray();
            }
        }

        /// <summary>
        /// 创建类型示例
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>类型示例</returns>
        private object CreateTypeExample(Type type)
        {
            if (type == typeof(string))
                return "string";
            if (type == typeof(int))
                return 0;
            if (type == typeof(long))
                return 0L;
            if (type == typeof(double))
                return 0.0;
            if (type == typeof(decimal))
                return 0m;
            if (type == typeof(bool))
                return false;
            if (type == typeof(DateTime))
                return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            if (type == typeof(Guid))
                return Guid.Empty.ToString();

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return new[] { CreateTypeExample(elementType) };
            }

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return new[] { CreateTypeExample(elementType) };
                }

                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    return null;
                }
            }

            return new { };
        }

        /// <summary>
        /// 收集类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="typeSet">类型集合</param>
        private void CollectType(Type type, HashSet<Type> typeSet)
        {
            if (type == null || IsBuiltInType(type) || typeSet.Contains(type))
                return;

            typeSet.Add(type);

            if (type.IsArray)
            {
                CollectType(type.GetElementType(), typeSet);
            }
            else if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    CollectType(arg, typeSet);
                }
            }
            else if (type.IsClass && type != typeof(string))
            {
                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    CollectType(property.PropertyType, typeSet);
                }
            }
        }

        /// <summary>
        /// 检查是否为内置类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>是否为内置类型</returns>
        private bool IsBuiltInType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(DateTime) || 
                   type == typeof(Guid) || 
                   type == typeof(decimal) ||
                   type == typeof(object) ||
                   type == typeof(void) ||
                   type == typeof(Task);
        }

        /// <summary>
        /// 检查是否为可空类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>是否为可空类型</returns>
        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        /// <summary>
        /// 获取实际返回类型
        /// </summary>
        /// <param name="returnType">返回类型</param>
        /// <returns>实际返回类型</returns>
        private Type GetActualReturnType(Type returnType)
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return returnType.GetGenericArguments()[0];
            }

            return returnType;
        }

        /// <summary>
        /// 转换为驼峰命名
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>驼峰命名</returns>
        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
} 