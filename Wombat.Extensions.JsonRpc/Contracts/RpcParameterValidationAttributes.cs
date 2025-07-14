using System;
using System.ComponentModel.DataAnnotations;

namespace Wombat.Extensions.JsonRpc.Contracts
{
    /// <summary>
    /// 用于标注RPC方法参数不能为null的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcParamNotNullAttribute : ValidationAttribute
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public RpcParamNotNullAttribute() : base("参数 {0} 不能为空")
        {
        }

        /// <summary>
        /// 验证参数值
        /// </summary>
        public override bool IsValid(object value)
        {
            return value != null;
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name);
        }
    }

    /// <summary>
    /// 用于标注RPC方法参数范围的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcParamRangeAttribute : ValidationAttribute
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public object Min { get; }

        /// <summary>
        /// 最大值
        /// </summary>
        public object Max { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public RpcParamRangeAttribute(object min, object max) : base("参数 {0} 必须在 {1} 到 {2} 之间")
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// 验证参数值
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null) return true; // null值由其他特性处理

            if (!(value is IComparable comparableValue)) return false;

            try
            {
                return comparableValue.CompareTo(Min) >= 0 && comparableValue.CompareTo(Max) <= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name, Min, Max);
        }
    }

    /// <summary>
    /// 用于标注RPC方法参数字符串长度的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcParamStringLengthAttribute : ValidationAttribute
    {
        /// <summary>
        /// 最小长度
        /// </summary>
        public int MinLength { get; }

        /// <summary>
        /// 最大长度
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxLength">最大长度</param>
        public RpcParamStringLengthAttribute(int maxLength) : this(0, maxLength)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="minLength">最小长度</param>
        /// <param name="maxLength">最大长度</param>
        public RpcParamStringLengthAttribute(int minLength, int maxLength) : base("参数 {0} 长度必须在 {1} 到 {2} 之间")
        {
            MinLength = minLength;
            MaxLength = maxLength;
        }

        /// <summary>
        /// 验证参数值
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null) return true; // null值由其他特性处理

            if (!(value is string stringValue)) return false;

            return stringValue.Length >= MinLength && stringValue.Length <= MaxLength;
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name, MinLength, MaxLength);
        }
    }

    /// <summary>
    /// 用于标注RPC方法参数正则表达式验证的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcParamRegexAttribute : ValidationAttribute
    {
        /// <summary>
        /// 正则表达式模式
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pattern">正则表达式模式</param>
        public RpcParamRegexAttribute(string pattern) : base("参数 {0} 格式不正确")
        {
            Pattern = pattern;
        }

        /// <summary>
        /// 验证参数值
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null) return true; // null值由其他特性处理

            if (!(value is string stringValue)) return false;

            return System.Text.RegularExpressions.Regex.IsMatch(stringValue, Pattern);
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name);
        }
    }

    /// <summary>
    /// 用于标注RPC方法参数自定义验证的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcParamCustomValidationAttribute : ValidationAttribute
    {
        /// <summary>
        /// 验证器类型
        /// </summary>
        public Type ValidatorType { get; }

        /// <summary>
        /// 验证方法名
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="validatorType">验证器类型</param>
        /// <param name="methodName">验证方法名</param>
        public RpcParamCustomValidationAttribute(Type validatorType, string methodName) : base("参数 {0} 验证失败")
        {
            ValidatorType = validatorType;
            MethodName = methodName;
        }

        /// <summary>
        /// 验证参数值
        /// </summary>
        public override bool IsValid(object value)
        {
            try
            {
                var method = ValidatorType.GetMethod(MethodName, new[] { typeof(object) });
                if (method == null) return false;

                var result = method.Invoke(null, new[] { value });
                return result is bool boolResult && boolResult;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name);
        }
    }
} 