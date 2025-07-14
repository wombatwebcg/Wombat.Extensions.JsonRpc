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
    /// TypeScript客户端生成器
    /// </summary>
    public class TypeScriptClientGenerator
    {
        private readonly ILogger<TypeScriptClientGenerator> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public TypeScriptClientGenerator(ILogger<TypeScriptClientGenerator> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 生成TypeScript客户端代码
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的代码</returns>
        public async Task<string> GenerateClientAsync(ServiceMetadata metadata, CodeGenerationOptions options = null)
        {
            options = options ?? CodeGenerationOptions.CreateTypeScriptOptions();
            
            _logger?.LogDebug("开始生成TypeScript客户端代码: {ServiceName}", metadata.ServiceName);

            var sb = new StringBuilder();

            // 生成文件头
            GenerateFileHeader(sb, options);

            // 生成导入语句
            GenerateImports(sb, options);

            // 生成类型定义
            GenerateTypeDefinitions(sb, metadata, options);

            // 生成客户端类
            GenerateClientClass(sb, metadata, options);

            var result = sb.ToString();
            _logger?.LogDebug("TypeScript客户端代码生成完成，长度: {Length}", result.Length);

            return result;
        }

        /// <summary>
        /// 生成TypeScript类型定义
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>生成的类型定义</returns>
        public async Task<string> GenerateDefinitionsAsync(ServiceMetadata metadata, CodeGenerationOptions options = null)
        {
            options = options ?? CodeGenerationOptions.CreateTypeScriptOptions();
            
            _logger?.LogDebug("开始生成TypeScript类型定义: {ServiceName}", metadata.ServiceName);

            var sb = new StringBuilder();

            // 生成文件头
            GenerateFileHeader(sb, options);

            // 生成类型定义
            GenerateTypeDefinitions(sb, metadata, options);

            var result = sb.ToString();
            _logger?.LogDebug("TypeScript类型定义生成完成，长度: {Length}", result.Length);

            return result;
        }

        /// <summary>
        /// 生成文件头
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateFileHeader(StringBuilder sb, CodeGenerationOptions options)
        {
            sb.AppendLine("// This file was auto-generated.");
            sb.AppendLine($"// Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("// Generator: Wombat.Extensions.JsonRpc.CodeGen");
            if (!string.IsNullOrEmpty(options.Author))
            {
                sb.AppendLine($"// Author: {options.Author}");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// 生成导入语句
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateImports(StringBuilder sb, CodeGenerationOptions options)
        {
            var tsOptions = options.TypeScriptOptions;

            if (tsOptions.HttpClient == TypeScriptHttpClient.Axios)
            {
                sb.AppendLine("import axios, { AxiosInstance, AxiosResponse } from 'axios';");
            }
            else if (tsOptions.HttpClient == TypeScriptHttpClient.Fetch)
            {
                // Fetch is built-in, no import needed
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 生成类型定义
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateTypeDefinitions(StringBuilder sb, ServiceMetadata metadata, CodeGenerationOptions options)
        {
            var tsOptions = options.TypeScriptOptions;

            // 生成参数和返回值类型
            var typeSet = new HashSet<Type>();
            foreach (var method in metadata.Methods)
            {
                // 收集参数类型
                foreach (var param in method.Parameters)
                {
                    CollectType(param.Type, typeSet);
                }

                // 收集返回值类型
                if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
                {
                    CollectType(GetActualReturnType(method.ReturnType), typeSet);
                }
            }

            // 生成基础类型
            GenerateBasicTypes(sb, options);

            // 生成自定义类型
            foreach (var type in typeSet.Where(t => !IsBuiltInType(t)).OrderBy(t => t.Name))
            {
                GenerateTypeDefinition(sb, type, options);
            }

            // 生成方法接口
            if (tsOptions.GenerateInterfaces)
            {
                GenerateServiceInterface(sb, metadata, options);
            }
        }

        /// <summary>
        /// 生成基础类型
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateBasicTypes(StringBuilder sb, CodeGenerationOptions options)
        {
            sb.AppendLine("// JSON-RPC基础类型");
            sb.AppendLine("export interface JsonRpcRequest {");
            sb.AppendLine("  jsonrpc: '2.0';");
            sb.AppendLine("  method: string;");
            sb.AppendLine("  params?: any;");
            sb.AppendLine("  id?: string | number;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("export interface JsonRpcResponse<T = any> {");
            sb.AppendLine("  jsonrpc: '2.0';");
            sb.AppendLine("  result?: T;");
            sb.AppendLine("  error?: JsonRpcError;");
            sb.AppendLine("  id?: string | number;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("export interface JsonRpcError {");
            sb.AppendLine("  code: number;");
            sb.AppendLine("  message: string;");
            sb.AppendLine("  data?: any;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("export class RpcError extends Error {");
            sb.AppendLine("  constructor(public code: number, message: string, public data?: any) {");
            sb.AppendLine("    super(message);");
            sb.AppendLine("    this.name = 'RpcError';");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成类型定义
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="type">类型</param>
        /// <param name="options">生成选项</param>
        private void GenerateTypeDefinition(StringBuilder sb, Type type, CodeGenerationOptions options)
        {
            if (options.IncludeDocumentation)
            {
                sb.AppendLine($"/**");
                sb.AppendLine($" * {type.Name}类型定义");
                sb.AppendLine($" */");
            }

            sb.AppendLine($"export interface {GetTypeScriptTypeName(type)} {{");

            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                var propertyName = ToCamelCase(property.Name);
                var propertyType = GetTypeScriptTypeName(property.PropertyType);
                var optional = IsNullableType(property.PropertyType) ? "?" : "";

                if (options.IncludeDocumentation)
                {
                    sb.AppendLine($"  /** {property.Name} */");
                }
                sb.AppendLine($"  {propertyName}{optional}: {propertyType};");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成服务接口
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateServiceInterface(StringBuilder sb, ServiceMetadata metadata, CodeGenerationOptions options)
        {
            var serviceName = GetServiceName(metadata);

            if (options.IncludeDocumentation)
            {
                sb.AppendLine($"/**");
                sb.AppendLine($" * {metadata.Description ?? $"{serviceName} RPC客户端接口"}");
                sb.AppendLine($" */");
            }

            sb.AppendLine($"export interface I{serviceName}Client {{");

            foreach (var method in metadata.Methods)
            {
                GenerateInterfaceMethod(sb, method, options);
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成接口方法
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateInterfaceMethod(StringBuilder sb, MethodMetadata method, CodeGenerationOptions options)
        {
            var methodName = ToCamelCase(method.MethodName);
            var parameters = GenerateTypeScriptParameters(method);
            var returnType = GetTypeScriptReturnType(method, options);

            if (options.IncludeDocumentation)
            {
                sb.AppendLine($"  /**");
                sb.AppendLine($"   * {method.Description ?? method.DisplayName}");
                
                foreach (var param in method.Parameters)
                {
                    sb.AppendLine($"   * @param {ToCamelCase(param.Name)} {param.Description ?? param.Name}");
                }

                if (!method.IsNotification)
                {
                    sb.AppendLine($"   * @returns {GetReturnTypeDescription(method.ReturnType)}");
                }
                sb.AppendLine($"   */");
            }

            sb.AppendLine($"  {methodName}({parameters}): {returnType};");
        }

        /// <summary>
        /// 生成客户端类
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="metadata">服务元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateClientClass(StringBuilder sb, ServiceMetadata metadata, CodeGenerationOptions options)
        {
            var serviceName = GetServiceName(metadata);
            var className = $"{serviceName}Client";

            if (options.IncludeDocumentation)
            {
                sb.AppendLine($"/**");
                sb.AppendLine($" * {metadata.Description ?? $"{serviceName} RPC客户端实现"}");
                sb.AppendLine($" */");
            }

            sb.AppendLine($"export class {className} implements I{serviceName}Client {{");

            // 生成字段
            GenerateClientFields(sb, options);

            // 生成构造函数
            GenerateClientConstructor(sb, className, options);

            // 生成方法实现
            foreach (var method in metadata.Methods)
            {
                GenerateMethodImplementation(sb, method, options);
            }

            // 生成辅助方法
            GenerateHelperMethods(sb, options);

            sb.AppendLine("}");
        }

        /// <summary>
        /// 生成客户端字段
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateClientFields(StringBuilder sb, CodeGenerationOptions options)
        {
            var tsOptions = options.TypeScriptOptions;

            if (tsOptions.HttpClient == TypeScriptHttpClient.Axios)
            {
                sb.AppendLine("  private readonly httpClient: AxiosInstance;");
            }
            else
            {
                sb.AppendLine("  private readonly baseUrl: string;");
            }

            sb.AppendLine("  private requestId = 1;");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成客户端构造函数
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="className">类名</param>
        /// <param name="options">生成选项</param>
        private void GenerateClientConstructor(StringBuilder sb, string className, CodeGenerationOptions options)
        {
            var tsOptions = options.TypeScriptOptions;

            if (options.IncludeDocumentation)
            {
                sb.AppendLine("  /**");
                sb.AppendLine("   * 构造函数");
                sb.AppendLine("   * @param baseUrl 服务基础URL");
                sb.AppendLine("   */");
            }

            if (tsOptions.HttpClient == TypeScriptHttpClient.Axios)
            {
                sb.AppendLine("  constructor(baseUrl: string) {");
                sb.AppendLine("    this.httpClient = axios.create({");
                sb.AppendLine("      baseURL: baseUrl,");
                sb.AppendLine("      headers: {");
                sb.AppendLine("        'Content-Type': 'application/json',");
                sb.AppendLine("      },");
                sb.AppendLine("    });");
                sb.AppendLine("  }");
            }
            else
            {
                sb.AppendLine("  constructor(baseUrl: string) {");
                sb.AppendLine("    this.baseUrl = baseUrl;");
                sb.AppendLine("  }");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 生成方法实现
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateMethodImplementation(StringBuilder sb, MethodMetadata method, CodeGenerationOptions options)
        {
            var methodName = ToCamelCase(method.MethodName);
            var parameters = GenerateTypeScriptParameters(method);
            var returnType = GetTypeScriptReturnType(method, options);

            if (options.IncludeDocumentation)
            {
                sb.AppendLine("  /** @inheritdoc */");
            }

            sb.AppendLine($"  async {methodName}({parameters}): {returnType} {{");

            // 生成参数验证
            if (options.IncludeValidation)
            {
                GenerateTypeScriptParameterValidation(sb, method);
            }

            // 生成请求体
            sb.AppendLine("    const request: JsonRpcRequest = {");
            sb.AppendLine("      jsonrpc: '2.0',");
            sb.AppendLine($"      method: '{method.MethodName}',");

            if (method.Parameters.Length > 0)
            {
                if (method.Parameters.Length == 1)
                {
                    sb.AppendLine($"      params: {ToCamelCase(method.Parameters[0].Name)},");
                }
                else
                {
                    sb.AppendLine("      params: [");
                    for (int i = 0; i < method.Parameters.Length; i++)
                    {
                        var param = method.Parameters[i];
                        var comma = i < method.Parameters.Length - 1 ? "," : "";
                        sb.AppendLine($"        {ToCamelCase(param.Name)}{comma}");
                    }
                    sb.AppendLine("      ],");
                }
            }

            if (!method.IsNotification)
            {
                sb.AppendLine("      id: this.requestId++,");
            }

            sb.AppendLine("    };");
            sb.AppendLine();

            // 生成HTTP调用
            GenerateHttpCall(sb, method, options);

            sb.AppendLine("  }");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成HTTP调用
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        private void GenerateHttpCall(StringBuilder sb, MethodMetadata method, CodeGenerationOptions options)
        {
            var tsOptions = options.TypeScriptOptions;

            if (options.IncludeExceptionHandling)
            {
                sb.AppendLine("    try {");
            }

            if (tsOptions.HttpClient == TypeScriptHttpClient.Axios)
            {
                sb.AppendLine("      const response = await this.httpClient.post<JsonRpcResponse>('/', request);");
                sb.AppendLine("      const result = response.data;");
            }
            else
            {
                sb.AppendLine("      const response = await fetch(this.baseUrl, {");
                sb.AppendLine("        method: 'POST',");
                sb.AppendLine("        headers: {");
                sb.AppendLine("          'Content-Type': 'application/json',");
                sb.AppendLine("        },");
                sb.AppendLine("        body: JSON.stringify(request),");
                sb.AppendLine("      });");
                sb.AppendLine();
                sb.AppendLine("      if (!response.ok) {");
                sb.AppendLine("        throw new Error(`HTTP error! status: ${response.status}`);");
                sb.AppendLine("      }");
                sb.AppendLine();
                sb.AppendLine("      const result: JsonRpcResponse = await response.json();");
            }

            if (!method.IsNotification)
            {
                sb.AppendLine();
                sb.AppendLine("      if (result.error) {");
                sb.AppendLine("        throw new RpcError(result.error.code, result.error.message, result.error.data);");
                sb.AppendLine("      }");
                sb.AppendLine();
                sb.AppendLine("      return result.result;");
            }

            if (options.IncludeExceptionHandling)
            {
                sb.AppendLine("    } catch (error) {");
                sb.AppendLine("      if (error instanceof RpcError) {");
                sb.AppendLine("        throw error;");
                sb.AppendLine("      }");
                sb.AppendLine($"      throw new RpcError(-1, `调用方法 '{method.MethodName}' 失败: ${{error.message}}`, error);");
                sb.AppendLine("    }");
            }
        }

        /// <summary>
        /// 生成参数验证
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="method">方法元数据</param>
        private void GenerateTypeScriptParameterValidation(StringBuilder sb, MethodMetadata method)
        {
            foreach (var param in method.Parameters.Where(p => p.IsRequired))
            {
                var paramName = ToCamelCase(param.Name);
                sb.AppendLine($"    if ({paramName} === null || {paramName} === undefined) {{");
                sb.AppendLine($"      throw new Error('参数 {param.Name} 不能为空');");
                sb.AppendLine("    }");
            }

            if (method.Parameters.Any(p => p.IsRequired))
            {
                sb.AppendLine();
            }
        }

        /// <summary>
        /// 生成辅助方法
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="options">生成选项</param>
        private void GenerateHelperMethods(StringBuilder sb, CodeGenerationOptions options)
        {
            // 可以在这里添加辅助方法，比如批量调用、连接管理等
        }

        /// <summary>
        /// 生成TypeScript参数
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <returns>参数字符串</returns>
        private string GenerateTypeScriptParameters(MethodMetadata method)
        {
            if (method.Parameters == null || method.Parameters.Length == 0)
            {
                return string.Empty;
            }

            var parameters = method.Parameters.Select(p =>
            {
                var paramName = ToCamelCase(p.Name);
                var paramType = GetTypeScriptTypeName(p.Type);
                var optional = p.IsRequired ? "" : "?";

                return $"{paramName}{optional}: {paramType}";
            });

            return string.Join(", ", parameters);
        }

        /// <summary>
        /// 获取TypeScript返回类型
        /// </summary>
        /// <param name="method">方法元数据</param>
        /// <param name="options">生成选项</param>
        /// <returns>返回类型字符串</returns>
        private string GetTypeScriptReturnType(MethodMetadata method, CodeGenerationOptions options)
        {
            if (method.IsNotification)
            {
                return "Promise<void>";
            }

            if (method.ReturnType == typeof(void) || method.ReturnType == typeof(Task))
            {
                return "Promise<void>";
            }

            var actualType = GetActualReturnType(method.ReturnType);
            var typeName = GetTypeScriptTypeName(actualType);
            
            return $"Promise<{typeName}>";
        }

        /// <summary>
        /// 获取TypeScript类型名称
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>TypeScript类型名称</returns>
        private string GetTypeScriptTypeName(Type type)
        {
            if (type == typeof(void))
                return "void";
            if (type == typeof(string))
                return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(DateTime))
                return "Date";
            if (type == typeof(Guid))
                return "string";
            if (type == typeof(object))
                return "any";

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return $"{GetTypeScriptTypeName(elementType)}[]";
            }

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>) || genericTypeDefinition == typeof(ICollection<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return $"{GetTypeScriptTypeName(elementType)}[]";
                }

                if (genericTypeDefinition == typeof(Dictionary<,>))
                {
                    var valueType = type.GetGenericArguments()[1];
                    return $"Record<string, {GetTypeScriptTypeName(valueType)}>";
                }

                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    var underlyingType = type.GetGenericArguments()[0];
                    return $"{GetTypeScriptTypeName(underlyingType)} | null";
                }
            }

            return type.Name;
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

        /// <summary>
        /// 获取服务名称
        /// </summary>
        /// <param name="metadata">服务元数据</param>
        /// <returns>服务名称</returns>
        private string GetServiceName(ServiceMetadata metadata)
        {
            return metadata.ServiceName?.Replace("Service", "").Replace("I", "") ?? "Unknown";
        }

        /// <summary>
        /// 获取返回类型描述
        /// </summary>
        /// <param name="returnType">返回类型</param>
        /// <returns>返回类型描述</returns>
        private string GetReturnTypeDescription(Type returnType)
        {
            if (returnType == typeof(void) || returnType == typeof(Task))
            {
                return "Promise<void>";
            }

            var actualType = GetActualReturnType(returnType);
            return $"Promise<{GetTypeScriptTypeName(actualType)}>";
        }
    }
} 