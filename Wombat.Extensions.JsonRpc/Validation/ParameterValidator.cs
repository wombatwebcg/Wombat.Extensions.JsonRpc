using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Wombat.Extensions.JsonRpc.Contracts;

namespace Wombat.Extensions.JsonRpc.Validation
{
    /// <summary>
    /// 参数验证器 - 自动验证方法参数
    /// </summary>
    public class ParameterValidator
    {
        private readonly ILogger<ParameterValidator> _logger;
        private readonly Dictionary<Type, Func<object, ValidationAttribute, bool>> _validatorMap;

        public ParameterValidator(ILogger<ParameterValidator> logger = null)
        {
            _logger = logger;
            _validatorMap = new Dictionary<Type, Func<object, ValidationAttribute, bool>>
            {
                { typeof(RpcParamNotNullAttribute), ValidateNotNull },
                { typeof(RpcParamRangeAttribute), ValidateRange },
                { typeof(RpcParamStringLengthAttribute), ValidateStringLength },
                { typeof(RpcParamRegexAttribute), ValidateRegex },
                { typeof(RpcParamCustomValidationAttribute), ValidateCustom }
            };
        }

        /// <summary>
        /// 验证方法参数
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <param name="parameters">参数值</param>
        /// <returns>验证结果</returns>
        public async Task<ValidationResult> ValidateParametersAsync(MethodInfo method, object[] parameters)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (parameters == null)
                parameters = new object[0];

            var parameterInfos = method.GetParameters();
            var errors = new List<ValidationError>();

            _logger?.LogDebug("开始验证方法 {Method} 的参数，参数数量: {Count}", method.Name, parameters.Length);

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                var paramInfo = parameterInfos[i];
                var paramValue = i < parameters.Length ? parameters[i] : null;

                // 获取参数的验证特性
                var validationAttributes = paramInfo.GetCustomAttributes<ValidationAttribute>();
                
                foreach (var attribute in validationAttributes)
                {
                    var result = await ValidateParameterAsync(paramInfo.Name, paramValue, attribute);
                    if (!result.IsValid)
                    {
                        errors.Add(new ValidationError
                        {
                            ParameterName = paramInfo.Name,
                            ErrorMessage = result.ErrorMessage,
                            AttemptedValue = paramValue,
                            ValidationType = attribute.GetType().Name
                        });
                    }
                }
            }

            var validationResult = new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Method = method.Name
            };

            if (!validationResult.IsValid)
            {
                _logger?.LogWarning("方法 {Method} 参数验证失败: {Errors}", 
                    method.Name, 
                    string.Join(", ", errors.Select(e => $"{e.ParameterName}: {e.ErrorMessage}")));
            }

            return validationResult;
        }

        /// <summary>
        /// 验证单个参数
        /// </summary>
        /// <param name="parameter">参数信息</param>
        /// <param name="value">参数值</param>
        /// <exception cref="RpcValidationException">验证失败时抛出</exception>
        public void ValidateParameter(ParameterInfo parameter, object value)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            var attributes = parameter.GetCustomAttributes<ValidationAttribute>();
            var errors = new List<ValidationError>();

            foreach (var attribute in attributes)
            {
                if (_validatorMap.TryGetValue(attribute.GetType(), out var validator))
                {
                    if (!validator(value, attribute))
                    {
                        errors.Add(new ValidationError
                        {
                            ParameterName = parameter.Name,
                            ErrorMessage = attribute.ErrorMessage ?? $"参数 '{parameter.Name}' 验证失败",
                            AttemptedValue = value,
                            ValidationType = attribute.GetType().Name
                        });
                    }
                }
            }

            if (errors.Any())
            {
                var validationResults = errors.Select(e => new System.ComponentModel.DataAnnotations.ValidationResult(
                    e.ErrorMessage, 
                    new[] { e.ParameterName }
                ));
                throw new RpcValidationException(validationResults);
            }
        }

        /// <summary>
        /// 验证单个参数
        /// </summary>
        private async Task<ParameterValidationResult> ValidateParameterAsync(string parameterName, object value, ValidationAttribute attribute)
        {
            try
            {
                if (_validatorMap.TryGetValue(attribute.GetType(), out var validator))
                {
                    var isValid = validator(value, attribute);
                    return new ParameterValidationResult
                    {
                        IsValid = isValid,
                        ErrorMessage = isValid ? null : attribute.FormatErrorMessage(parameterName)
                    };
                }

                _logger?.LogWarning("未找到验证器: {AttributeType}", attribute.GetType().Name);
                return new ParameterValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "验证参数 {Parameter} 时发生异常", parameterName);
                return new ParameterValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"验证时发生错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 验证非空约束
        /// </summary>
        private bool ValidateNotNull(object value, ValidationAttribute attribute)
        {
            return value != null;
        }

        /// <summary>
        /// 验证数值范围
        /// </summary>
        private bool ValidateRange(object value, ValidationAttribute attribute)
        {
            if (value == null) return true; // 空值由NotNull特性处理

            var rangeAttr = (RpcParamRangeAttribute)attribute;
            
            if (value is IComparable comparable)
            {
                return comparable.CompareTo(rangeAttr.Min) >= 0 && 
                       comparable.CompareTo(rangeAttr.Max) <= 0;
            }

            return false;
        }

        /// <summary>
        /// 验证字符串长度
        /// </summary>
        private bool ValidateStringLength(object value, ValidationAttribute attribute)
        {
            if (value == null) return true;
            
            var stringLengthAttr = (RpcParamStringLengthAttribute)attribute;
            var stringValue = value.ToString();

            return stringValue.Length >= stringLengthAttr.MinLength && 
                   stringValue.Length <= stringLengthAttr.MaxLength;
        }

        /// <summary>
        /// 验证正则表达式
        /// </summary>
        private bool ValidateRegex(object value, ValidationAttribute attribute)
        {
            if (value == null) return true;
            
            var regexAttr = (RpcParamRegexAttribute)attribute;
            var stringValue = value.ToString();

            return Regex.IsMatch(stringValue, regexAttr.Pattern);
        }

        /// <summary>
        /// 验证自定义验证
        /// </summary>
        private bool ValidateCustom(object value, ValidationAttribute attribute)
        {
            var customAttr = (RpcParamCustomValidationAttribute)attribute;
            
            try
            {
                var method = customAttr.ValidatorType.GetMethod(customAttr.MethodName);
                if (method == null)
                {
                    _logger?.LogError("未找到验证方法: {Type}.{Method}", 
                        customAttr.ValidatorType.Name, customAttr.MethodName);
                    return false;
                }

                var result = method.Invoke(null, new[] { value });
                return result is bool valid && valid;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行自定义验证时发生异常");
                return false;
            }
        }
    }

    /// <summary>
    /// 参数验证结果
    /// </summary>
    internal class ParameterValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
        public string Method { get; set; }
    }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public class ValidationError
    {
        public string ParameterName { get; set; }
        public string ErrorMessage { get; set; }
        public object AttemptedValue { get; set; }
        public string ValidationType { get; set; }
    }
} 