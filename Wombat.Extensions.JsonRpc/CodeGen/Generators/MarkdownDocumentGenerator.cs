using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.CodeGen.Generators
{
    /// <summary>
    /// Markdown文档生成器
    /// </summary>
    public class MarkdownDocumentGenerator
    {
        private readonly ILogger<MarkdownDocumentGenerator> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public MarkdownDocumentGenerator(ILogger<MarkdownDocumentGenerator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 生成Markdown文档
        /// </summary>
        /// <param name="services">服务元数据集合</param>
        /// <param name="options">生成选项</param>
        /// <returns>Markdown文档</returns>
        public async Task<string> GenerateAsync(IEnumerable<ServiceMetadata> services, CodeGenerationOptions options = null)
        {
            options = options ?? CodeGenerationOptions.CreateDocumentationOptions();
            
            _logger?.LogDebug("开始生成Markdown文档，服务数量: {Count}", services.Count());

            var sb = new StringBuilder();

            // 生成文档头部
            GenerateHeader(sb, options);

            // 生成目录
            GenerateTableOfContents(sb, services);

            // 生成服务文档
            foreach (var service in services.OrderBy(s => s.ServiceName))
            {
                GenerateServiceDocumentation(sb, service, options);
            }

            // 生成附录
            GenerateAppendix(sb, services, options);

            var result = sb.ToString();
            _logger?.LogDebug("Markdown文档生成完成，长度: {Length}", result.Length);

            return result;
        }

        /// <summary>
        /// 生成文档头部
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateHeader(StringBuilder sb, CodeGenerationOptions options)
        {
            sb.AppendLine($"# {options.ApiTitle}");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(options.ApiDescription))
            {
                sb.AppendLine(options.ApiDescription);
                sb.AppendLine();
            }

            sb.AppendLine("## API信息");
            sb.AppendLine();
            sb.AppendLine($"- **版本**: {options.ApiVersion}");
            sb.AppendLine($"- **基础URL**: {options.BaseUrl}");
            sb.AppendLine($"- **生成时间**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            if (!string.IsNullOrEmpty(options.Author))
            {
                sb.AppendLine($"- **作者**: {options.Author}");
            }
            sb.AppendLine();

            sb.AppendLine("## 协议说明");
            sb.AppendLine();
            sb.AppendLine("本API基于JSON-RPC 2.0协议，所有请求都通过HTTP POST方法发送到服务端点。");
            sb.AppendLine();
            sb.AppendLine("### 请求格式");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"jsonrpc\": \"2.0\",");
            sb.AppendLine("  \"method\": \"方法名\",");
            sb.AppendLine("  \"params\": [参数列表],");
            sb.AppendLine("  \"id\": \"请求ID\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### 响应格式");
            sb.AppendLine();
            sb.AppendLine("**成功响应**:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"jsonrpc\": \"2.0\",");
            sb.AppendLine("  \"result\": \"返回结果\",");
            sb.AppendLine("  \"id\": \"请求ID\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("**错误响应**:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"jsonrpc\": \"2.0\",");
            sb.AppendLine("  \"error\": {");
            sb.AppendLine("    \"code\": -32600,");
            sb.AppendLine("    \"message\": \"错误消息\",");
            sb.AppendLine("    \"data\": \"详细错误信息\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"id\": \"请求ID\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### 认证");
            sb.AppendLine();
            sb.AppendLine("某些API方法需要认证。支持以下认证方式：");
            sb.AppendLine();
            sb.AppendLine("- **JWT Bearer Token**: 在HTTP头中添加 `Authorization: Bearer <token>`");
            sb.AppendLine("- **API Key**: 在HTTP头中添加 `X-API-Key: <api-key>`");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成目录
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="services">服务集合</param>
        private void GenerateTableOfContents(StringBuilder sb, IEnumerable<ServiceMetadata> services)
        {
            sb.AppendLine("## 目录");
            sb.AppendLine();

            foreach (var service in services.OrderBy(s => s.ServiceName))
            {
                sb.AppendLine($"- [{service.ServiceName}](#{service.ServiceName.ToLower().Replace(" ", "-")})");
                
                foreach (var method in service.Methods.OrderBy(m => m.MethodName))
                {
                    var anchor = $"{service.ServiceName.ToLower()}-{method.MethodName.ToLower()}".Replace(" ", "-");
                    sb.AppendLine($"  - [{method.MethodName}](#{anchor})");
                }
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 生成服务文档
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="service">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateServiceDocumentation(StringBuilder sb, ServiceMetadata service, CodeGenerationOptions options)
        {
            sb.AppendLine($"## {service.ServiceName}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(service.Description))
            {
                sb.AppendLine(service.Description);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(service.Version))
            {
                sb.AppendLine($"**版本**: {service.Version}");
                sb.AppendLine();
            }

            if (service.RequireAuthentication == true)
            {
                sb.AppendLine("🔒 **此服务需要认证**");
                sb.AppendLine();
            }

            // 生成方法文档
            foreach (var method in service.Methods.OrderBy(m => m.MethodName))
            {
                GenerateMethodDocumentation(sb, method, service, options);
            }
        }

        /// <summary>
        /// 生成方法文档
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        /// <param name="service">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateMethodDocumentation(StringBuilder sb, MethodMetadata method, ServiceMetadata service, CodeGenerationOptions options)
        {
            var anchor = $"{service.ServiceName.ToLower()}-{method.MethodName.ToLower()}".Replace(" ", "-");
            sb.AppendLine($"### {method.MethodName} {{#{anchor}}}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(method.Description))
            {
                sb.AppendLine(method.Description);
                sb.AppendLine();
            }

            // 方法信息
            sb.AppendLine("**方法信息**:");
            sb.AppendLine();
            sb.AppendLine($"- **方法名**: `{method.MethodName}`");
            sb.AppendLine($"- **类型**: {(method.IsNotification ? "通知" : "请求-响应")}");
            if (method.RequireAuthentication)
            {
                sb.AppendLine("- **认证**: 🔒 需要认证");
            }
            if (method.TimeoutMs > 0)
            {
                sb.AppendLine($"- **超时**: {method.TimeoutMs}ms");
            }
            sb.AppendLine();

            // 参数文档
            if (method.Parameters.Length > 0)
            {
                sb.AppendLine("**参数**:");
                sb.AppendLine();
                sb.AppendLine("| 参数名 | 类型 | 必需 | 描述 |");
                sb.AppendLine("|--------|------|------|------|");

                foreach (var param in method.Parameters)
                {
                    var typeName = GetTypeName(param.Type);
                    var required = param.IsRequired ? "✅" : "❌";
                    var description = param.Description ?? "-";
                    
                    sb.AppendLine($"| `{param.Name}` | `{typeName}` | {required} | {description} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("**参数**: 无");
                sb.AppendLine();
            }

            // 返回值文档
            if (!method.IsNotification)
            {
                sb.AppendLine("**返回值**:");
                sb.AppendLine();
                if (method.ReturnType == typeof(void) || method.ReturnType == typeof(Task))
                {
                    sb.AppendLine("- **类型**: `void`");
                    sb.AppendLine("- **描述**: 无返回值");
                }
                else
                {
                    var returnType = GetActualReturnType(method.ReturnType);
                    var typeName = GetTypeName(returnType);
                    sb.AppendLine($"- **类型**: `{typeName}`");
                    sb.AppendLine("- **描述**: 操作结果");
                }
                sb.AppendLine();
            }

            // 请求示例
            GenerateRequestExample(sb, method, service);

            // 响应示例
            if (!method.IsNotification)
            {
                GenerateResponseExample(sb, method);
            }

            // 错误代码
            GenerateErrorCodes(sb, method);

            sb.AppendLine("---");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成请求示例
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        /// <param name="service">服务元数据</param>
        private void GenerateRequestExample(StringBuilder sb, MethodMetadata method, ServiceMetadata service)
        {
            sb.AppendLine("**请求示例**:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"jsonrpc\": \"2.0\",");
            sb.AppendLine($"  \"method\": \"{method.MethodName}\",");

            if (method.Parameters.Length > 0)
            {
                if (method.Parameters.Length == 1)
                {
                    var param = method.Parameters[0];
                    var example = GetTypeExample(param.Type);
                    sb.AppendLine($"  \"params\": {example},");
                }
                else
                {
                    sb.AppendLine("  \"params\": [");
                    for (int i = 0; i < method.Parameters.Length; i++)
                    {
                        var param = method.Parameters[i];
                        var example = GetTypeExample(param.Type);
                        var comma = i < method.Parameters.Length - 1 ? "," : "";
                        sb.AppendLine($"    {example}{comma}");
                    }
                    sb.AppendLine("  ],");
                }
            }

            if (!method.IsNotification)
            {
                sb.AppendLine("  \"id\": \"1\"");
            }

            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成响应示例
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        private void GenerateResponseExample(StringBuilder sb, MethodMetadata method)
        {
            sb.AppendLine("**响应示例**:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"jsonrpc\": \"2.0\",");

            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
            {
                var returnType = GetActualReturnType(method.ReturnType);
                var example = GetTypeExample(returnType);
                sb.AppendLine($"  \"result\": {example},");
            }
            else
            {
                sb.AppendLine("  \"result\": null,");
            }

            sb.AppendLine("  \"id\": \"1\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成错误代码
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        private void GenerateErrorCodes(StringBuilder sb, MethodMetadata method)
        {
            sb.AppendLine("**可能的错误代码**:");
            sb.AppendLine();
            sb.AppendLine("| 错误代码 | 错误消息 | 描述 |");
            sb.AppendLine("|----------|----------|------|");
            sb.AppendLine("| -32700 | Parse error | JSON解析错误 |");
            sb.AppendLine("| -32600 | Invalid Request | 无效的请求 |");
            sb.AppendLine("| -32601 | Method not found | 方法不存在 |");
            sb.AppendLine("| -32602 | Invalid params | 无效的参数 |");
            sb.AppendLine("| -32603 | Internal error | 内部错误 |");

            if (method.RequireAuthentication)
            {
                sb.AppendLine("| -32000 | Unauthorized | 未授权访问 |");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 生成附录
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="services">服务集合</param>
        /// <param name="options">生成选项</param>
        private void GenerateAppendix(StringBuilder sb, IEnumerable<ServiceMetadata> services, CodeGenerationOptions options)
        {
            sb.AppendLine("## 附录");
            sb.AppendLine();

            // 类型定义
            GenerateTypeDefinitions(sb, services);

            // 错误代码参考
            GenerateErrorCodeReference(sb);

            // 客户端示例
            GenerateClientExamples(sb, options);
        }

        /// <summary>
        /// 生成类型定义
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="services">服务集合</param>
        private void GenerateTypeDefinitions(StringBuilder sb, IEnumerable<ServiceMetadata> services)
        {
            sb.AppendLine("### 数据类型定义");
            sb.AppendLine();

            var typeSet = new HashSet<Type>();
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

            var customTypes = typeSet.Where(t => !IsBuiltInType(t) && t.IsClass).OrderBy(t => t.Name);
            if (customTypes.Any())
            {
                foreach (var type in customTypes)
                {
                    GenerateTypeDefinition(sb, type);
                }
            }
            else
            {
                sb.AppendLine("无自定义数据类型");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// 生成类型定义
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="type">类型</param>
        private void GenerateTypeDefinition(StringBuilder sb, Type type)
        {
            sb.AppendLine($"#### {type.Name}");
            sb.AppendLine();

            var properties = type.GetProperties();
            if (properties.Length > 0)
            {
                sb.AppendLine("| 属性名 | 类型 | 可空 | 描述 |");
                sb.AppendLine("|--------|------|------|------|");

                foreach (var property in properties)
                {
                    var propertyType = GetTypeName(property.PropertyType);
                    var nullable = IsNullableType(property.PropertyType) ? "✅" : "❌";
                    sb.AppendLine($"| `{property.Name}` | `{propertyType}` | {nullable} | - |");
                }
            }
            else
            {
                sb.AppendLine("无属性定义");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 生成错误代码参考
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        private void GenerateErrorCodeReference(StringBuilder sb)
        {
            sb.AppendLine("### 错误代码参考");
            sb.AppendLine();
            sb.AppendLine("| 错误代码 | 错误消息 | 描述 |");
            sb.AppendLine("|----------|----------|------|");
            sb.AppendLine("| -32700 | Parse error | 服务端接收到无效的JSON |");
            sb.AppendLine("| -32600 | Invalid Request | 发送的JSON不是有效的请求对象 |");
            sb.AppendLine("| -32601 | Method not found | 该方法不存在或无效 |");
            sb.AppendLine("| -32602 | Invalid params | 无效的方法参数 |");
            sb.AppendLine("| -32603 | Internal error | JSON-RPC内部错误 |");
            sb.AppendLine("| -32000到-32099 | Server error | 服务端错误 |");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成客户端示例
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateClientExamples(StringBuilder sb, CodeGenerationOptions options)
        {
            sb.AppendLine("### 客户端示例");
            sb.AppendLine();

            // JavaScript示例
            sb.AppendLine("#### JavaScript (fetch)");
            sb.AppendLine();
            sb.AppendLine("```javascript");
            sb.AppendLine("async function callRpcMethod(method, params) {");
            sb.AppendLine($"  const response = await fetch('{options.BaseUrl}', {{");
            sb.AppendLine("    method: 'POST',");
            sb.AppendLine("    headers: {");
            sb.AppendLine("      'Content-Type': 'application/json',");
            sb.AppendLine("    },");
            sb.AppendLine("    body: JSON.stringify({");
            sb.AppendLine("      jsonrpc: '2.0',");
            sb.AppendLine("      method: method,");
            sb.AppendLine("      params: params,");
            sb.AppendLine("      id: Date.now().toString()");
            sb.AppendLine("    })");
            sb.AppendLine("  });");
            sb.AppendLine();
            sb.AppendLine("  const result = await response.json();");
            sb.AppendLine("  ");
            sb.AppendLine("  if (result.error) {");
            sb.AppendLine("    throw new Error(`RPC Error ${result.error.code}: ${result.error.message}`);");
            sb.AppendLine("  }");
            sb.AppendLine("  ");
            sb.AppendLine("  return result.result;");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            // C#示例
            sb.AppendLine("#### C# (HttpClient)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();
            sb.AppendLine("public async Task<T> CallRpcMethodAsync<T>(string method, object parameters)");
            sb.AppendLine("{");
            sb.AppendLine("    var request = new");
            sb.AppendLine("    {");
            sb.AppendLine("        jsonrpc = \"2.0\",");
            sb.AppendLine("        method = method,");
            sb.AppendLine("        @params = parameters,");
            sb.AppendLine("        id = Guid.NewGuid().ToString()");
            sb.AppendLine("    };");
            sb.AppendLine();
            sb.AppendLine("    var json = JsonSerializer.Serialize(request);");
            sb.AppendLine("    var content = new StringContent(json, Encoding.UTF8, \"application/json\");");
            sb.AppendLine();
            sb.AppendLine($"    var response = await httpClient.PostAsync(\"{options.BaseUrl}\", content);");
            sb.AppendLine("    var responseJson = await response.Content.ReadAsStringAsync();");
            sb.AppendLine("    var result = JsonSerializer.Deserialize<JsonRpcResponse<T>>(responseJson);");
            sb.AppendLine();
            sb.AppendLine("    if (result.Error != null)");
            sb.AppendLine("    {");
            sb.AppendLine("        throw new Exception($\"RPC Error {result.Error.Code}: {result.Error.Message}\");");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    return result.Result;");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// 获取类型名称
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>类型名称</returns>
        private string GetTypeName(Type type)
        {
            if (type == typeof(void))
                return "void";
            if (type == typeof(string))
                return "string";
            if (type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "long";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(double))
                return "number";
            if (type == typeof(decimal))
                return "decimal";
            if (type == typeof(DateTime))
                return "DateTime";
            if (type == typeof(Guid))
                return "Guid";

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return $"{GetTypeName(elementType)}[]";
            }

            if (type.IsGenericType)
            {
                var genericTypeName = type.Name.Substring(0, type.Name.IndexOf('`'));
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
                return $"{genericTypeName}<{genericArgs}>";
            }

            return type.Name;
        }

        /// <summary>
        /// 获取类型示例
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>类型示例</returns>
        private string GetTypeExample(Type type)
        {
            if (type == typeof(string))
                return "\"示例字符串\"";
            if (type == typeof(int))
                return "42";
            if (type == typeof(long))
                return "1234567890";
            if (type == typeof(bool))
                return "true";
            if (type == typeof(double))
                return "3.14";
            if (type == typeof(decimal))
                return "99.99";
            if (type == typeof(DateTime))
                return "\"2023-12-25T10:30:00Z\"";
            if (type == typeof(Guid))
                return "\"12345678-1234-1234-1234-123456789012\"";

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var elementExample = GetTypeExample(elementType);
                return $"[{elementExample}]";
            }

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    var elementExample = GetTypeExample(elementType);
                    return $"[{elementExample}]";
                }

                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    return "null";
                }
            }

            return "{}";
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
    }
} 