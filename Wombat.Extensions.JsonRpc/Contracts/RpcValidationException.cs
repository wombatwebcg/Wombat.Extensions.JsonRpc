using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Wombat.Extensions.JsonRpc.Contracts
{
    /// <summary>
    /// RPC验证异常
    /// </summary>
    public class RpcValidationException : Exception
    {
        /// <summary>
        /// 验证结果集合
        /// </summary>
        public IEnumerable<ValidationResult> ValidationResults { get; }

        /// <summary>
        /// 参数名称
        /// </summary>
        public string? ParameterName { get; }

        /// <summary>
        /// 方法名称
        /// </summary>
        public string? MethodName { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="validationResults">验证结果集合</param>
        public RpcValidationException(IEnumerable<ValidationResult> validationResults)
            : base(CreateMessage(validationResults))
        {
            ValidationResults = validationResults?.ToArray() ?? Array.Empty<ValidationResult>();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="validationResults">验证结果集合</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="methodName">方法名称</param>
        public RpcValidationException(IEnumerable<ValidationResult> validationResults, string? parameterName, string? methodName)
            : base(CreateMessage(validationResults, parameterName, methodName))
        {
            ValidationResults = validationResults?.ToArray() ?? Array.Empty<ValidationResult>();
            ParameterName = parameterName;
            MethodName = methodName;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="validationResult">验证结果</param>
        public RpcValidationException(ValidationResult validationResult)
            : this(new[] { validationResult })
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="validationResult">验证结果</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="methodName">方法名称</param>
        public RpcValidationException(ValidationResult validationResult, string? parameterName, string? methodName)
            : this(new[] { validationResult }, parameterName, methodName)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误消息</param>
        public RpcValidationException(string message)
            : base(message)
        {
            ValidationResults = Array.Empty<ValidationResult>();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public RpcValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
            ValidationResults = Array.Empty<ValidationResult>();
        }

        /// <summary>
        /// 创建错误消息
        /// </summary>
        /// <param name="validationResults">验证结果集合</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="methodName">方法名称</param>
        /// <returns>错误消息</returns>
        private static string CreateMessage(IEnumerable<ValidationResult> validationResults, string? parameterName = null, string? methodName = null)
        {
            var results = validationResults?.ToArray() ?? Array.Empty<ValidationResult>();
            
            if (results.Length == 0)
                return "参数验证失败";

            var messages = results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
            var message = string.Join("; ", messages);

            if (!string.IsNullOrEmpty(parameterName))
                message = $"参数 '{parameterName}' 验证失败: {message}";

            if (!string.IsNullOrEmpty(methodName))
                message = $"方法 '{methodName}' 中的{message}";

            return message;
        }

        /// <summary>
        /// 获取所有验证错误消息
        /// </summary>
        /// <returns>错误消息集合</returns>
        public IEnumerable<string> GetValidationErrors()
        {
            return ValidationResults
                .Where(r => !string.IsNullOrEmpty(r.ErrorMessage))
                .Select(r => r.ErrorMessage!);
        }

        /// <summary>
        /// 获取指定成员的验证错误
        /// </summary>
        /// <param name="memberName">成员名称</param>
        /// <returns>错误消息集合</returns>
        public IEnumerable<string> GetValidationErrors(string memberName)
        {
            return ValidationResults
                .Where(r => r.MemberNames.Contains(memberName) && !string.IsNullOrEmpty(r.ErrorMessage))
                .Select(r => r.ErrorMessage!);
        }

        /// <summary>
        /// 检查是否有指定成员的验证错误
        /// </summary>
        /// <param name="memberName">成员名称</param>
        /// <returns>是否有错误</returns>
        public bool HasValidationError(string memberName)
        {
            return ValidationResults.Any(r => r.MemberNames.Contains(memberName));
        }

        /// <summary>
        /// 转换为字典格式
        /// </summary>
        /// <returns>错误字典</returns>
        public Dictionary<string, string[]> ToDictionary()
        {
            var result = new Dictionary<string, string[]>();

            foreach (var validationResult in ValidationResults)
            {
                var errorMessage = validationResult.ErrorMessage ?? "验证失败";
                
                if (validationResult.MemberNames.Any())
                {
                    foreach (var memberName in validationResult.MemberNames)
                    {
                        if (result.ContainsKey(memberName))
                        {
                            var existing = result[memberName];
                            var newArray = new string[existing.Length + 1];
                            existing.CopyTo(newArray, 0);
                            newArray[existing.Length] = errorMessage;
                            result[memberName] = newArray;
                        }
                        else
                        {
                            result[memberName] = new[] { errorMessage };
                        }
                    }
                }
                else
                {
                    var key = ParameterName ?? "Unknown";
                    if (result.ContainsKey(key))
                    {
                        var existing = result[key];
                        var newArray = new string[existing.Length + 1];
                        existing.CopyTo(newArray, 0);
                        newArray[existing.Length] = errorMessage;
                        result[key] = newArray;
                    }
                    else
                    {
                        result[key] = new[] { errorMessage };
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// RPC参数验证器
    /// </summary>
    public static class RpcParameterValidator
    {
        /// <summary>
        /// 验证参数值
        /// </summary>
        /// <param name="value">参数值</param>
        /// <param name="parameterMetadata">参数元数据</param>
        /// <returns>验证结果</returns>
        public static ValidationResult ValidateParameter(object? value, ParameterMetadata parameterMetadata)
        {
            if (parameterMetadata == null)
                return ValidationResult.Success!;

            var context = new ValidationContext(new object()) { MemberName = parameterMetadata.Name };
            
            foreach (var validation in parameterMetadata.Validations)
            {
                var result = validation.GetValidationResult(value, context);
                if (result != ValidationResult.Success)
                {
                    return result;
                }
            }

            return ValidationResult.Success!;
        }

        /// <summary>
        /// 验证所有参数
        /// </summary>
        /// <param name="parameters">参数值数组</param>
        /// <param name="methodMetadata">方法元数据</param>
        /// <returns>验证结果集合</returns>
        public static IEnumerable<ValidationResult> ValidateParameters(object?[] parameters, MethodMetadata methodMetadata)
        {
            if (methodMetadata == null || !methodMetadata.EnableParameterValidation)
                yield break;

            var parameterMetadatas = methodMetadata.Parameters;
            
            for (int i = 0; i < Math.Min(parameters.Length, parameterMetadatas.Length); i++)
            {
                var paramValue = parameters[i];
                var paramMetadata = parameterMetadatas[i];
                
                var result = ValidateParameter(paramValue, paramMetadata);
                if (result != ValidationResult.Success)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// 验证参数并抛出异常
        /// </summary>
        /// <param name="parameters">参数值数组</param>
        /// <param name="methodMetadata">方法元数据</param>
        /// <exception cref="RpcValidationException">参数验证失败时抛出</exception>
        public static void ValidateParametersAndThrow(object?[] parameters, MethodMetadata methodMetadata)
        {
            var validationResults = ValidateParameters(parameters, methodMetadata).ToArray();
            
            if (validationResults.Length > 0)
            {
                throw new RpcValidationException(validationResults, null, methodMetadata.MethodName);
            }
        }
    }
} 