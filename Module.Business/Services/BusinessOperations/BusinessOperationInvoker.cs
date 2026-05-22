using Shared.Abstractions;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Mediator;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Business.Services.BusinessOperations;

/// <summary>
/// 业务方法统一调用器，负责按 DeviceId 和 OperationId 找到业务方法并完成参数转换。
/// </summary>
public static class BusinessOperationInvoker
{
    #region 服务解析字段

    /// <summary>
    /// 外部注入的服务解析委托，用于创建业务实例和填充运行时参数。
    /// </summary>
    private static Func<Type, object?>? _serviceResolver;

    #endregion

    #region 配置入口

    /// <summary>
    /// 配置服务解析委托。
    /// </summary>
    /// <param name="serviceResolver">根据类型解析服务实例的委托。</param>
    public static void ConfigureServiceResolver(Func<Type, object?> serviceResolver)
    {
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
    }

    #endregion

    #region 调用入口

    /// <summary>
    /// 判断指定业务方法是否可以被调用。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
    /// <param name="operationId">业务方法唯一标识。</param>
    /// <returns>业务方法存在返回 true，否则返回 false。</returns>
    public static bool CanInvoke(string deviceId, string operationId)
    {
        return BusinessOperationCatalog.Find(deviceId, operationId) is not null;
    }

    /// <summary>
    /// 调用指定业务方法。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
    /// <param name="operationId">业务方法唯一标识。</param>
    /// <param name="parameters">步骤配置中的参数值，键为业务方法参数名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务方法调用结果。</returns>
    public static async Task<BusinessOperationInvocationResult> InvokeAsync(
        string deviceId,
        string operationId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        return await InvokeAsync(deviceId, operationId, parameters, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<BusinessOperationInvocationResult> InvokeAsync(
        string deviceId,
        string operationId,
        IReadOnlyDictionary<string, string> parameters,
        ICommunication? currentCommunication,
        CancellationToken cancellationToken = default)
    {
        BusinessOperationRegistration? registration = BusinessOperationCatalog.FindRegistration(deviceId, operationId);
        if (registration is null)
        {
            return BusinessOperationInvocationResult.Failure(
                $"Business operation '{deviceId}.{operationId}' was not found.");
        }

        try
        {
            object? target = registration.Method.IsStatic
                ? null
                : CreateTarget(registration.DeclaringType);
            object?[] args = BuildArguments(registration, parameters, currentCommunication, cancellationToken);
            object? value = registration.Method.Invoke(target, args);
            if (value is Task task)
            {
                await task.ConfigureAwait(false);
                value = task.GetType().IsGenericType
                    ? task.GetType().GetProperty("Result")?.GetValue(task)
                    : null;
            }

            return BusinessOperationInvocationResult.Success(value);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        catch (Exception ex)
        {
            return BusinessOperationInvocationResult.Failure(ex.Message, ex);
        }
    }

    #endregion

    #region 参数分类

    /// <summary>
    /// 判断参数是否属于运行时参数；运行时参数不展示在步骤配置中。
    /// </summary>
    /// <param name="parameterType">参数类型。</param>
    /// <returns>运行时参数返回 true，否则返回 false。</returns>
    internal static bool IsRuntimeParameter(Type parameterType)
    {
        return parameterType == typeof(CancellationToken) ||
               parameterType == typeof(IMediator) ||
               parameterType == typeof(IEventAggregator) ||
               typeof(ICommunication).IsAssignableFrom(parameterType) ||
               typeof(ICommunicationClientSource).IsAssignableFrom(parameterType);
    }

    #endregion

    #region 实例创建

    /// <summary>
    /// 创建业务类型实例，优先使用容器中已注册的实例。
    /// </summary>
    /// <param name="type">业务类型。</param>
    /// <returns>业务类型实例。</returns>
    private static object CreateTarget(Type type)
    {
        object? registeredInstance = _serviceResolver?.Invoke(type);
        if (registeredInstance is not null)
        {
            return registeredInstance;
        }

        foreach (ConstructorInfo constructor in type
                     .GetConstructors()
                     .OrderByDescending(constructor => constructor.GetParameters().Length))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            object?[] args = new object?[parameters.Length];
            bool canCreate = true;
            for (int index = 0; index < parameters.Length; index++)
            {
                object? dependency = _serviceResolver?.Invoke(parameters[index].ParameterType);
                if (dependency is null)
                {
                    canCreate = false;
                    break;
                }

                args[index] = dependency;
            }

            if (canCreate)
            {
                return constructor.Invoke(args);
            }
        }

        return Activator.CreateInstance(type)
               ?? throw new InvalidOperationException($"Cannot create business type '{type.FullName}'.");
    }

    #endregion

    #region 参数构建

    /// <summary>
    /// 构建反射调用所需的参数数组。
    /// </summary>
    /// <param name="registration">业务方法注册信息。</param>
    /// <param name="values">步骤配置中的参数值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按方法签名顺序排列的参数数组。</returns>
    private static object?[] BuildArguments(
        BusinessOperationRegistration registration,
        IReadOnlyDictionary<string, string> values,
        ICommunication? currentCommunication,
        CancellationToken cancellationToken)
    {
        Dictionary<string, BusinessParameterDescriptor> descriptorByName = registration.Descriptor.Parameters
            .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> valueByName = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => pair.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        ParameterInfo[] methodParameters = registration.Method.GetParameters();
        object?[] args = new object?[methodParameters.Length];
        for (int index = 0; index < methodParameters.Length; index++)
        {
            ParameterInfo parameter = methodParameters[index];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                args[index] = cancellationToken;
                continue;
            }

            if (TryResolveCurrentCommunicationParameter(
                    registration,
                    parameter,
                    currentCommunication,
                    out object? currentCommunicationValue))
            {
                args[index] = currentCommunicationValue;
                continue;
            }

            if (IsRuntimeParameter(parameter.ParameterType))
            {
                object? service = _serviceResolver?.Invoke(parameter.ParameterType);
                if (service is null &&
                    (typeof(ICommunication).IsAssignableFrom(parameter.ParameterType) ||
                     typeof(ICommunicationClientSource).IsAssignableFrom(parameter.ParameterType)))
                {
                    throw new InvalidOperationException(
                        $"Business operation '{registration.Descriptor.DeviceId}.{registration.Descriptor.OperationId}' " +
                        $"requires current communication parameter '{parameter.Name}', but no compatible communication object is available.");
                }

                args[index] = service;
                continue;
            }

            string parameterName = parameter.Name ?? string.Empty;
            string value = valueByName.TryGetValue(parameterName, out string? configuredValue)
                ? configuredValue
                : descriptorByName.TryGetValue(parameterName, out BusinessParameterDescriptor? descriptor)
                    ? descriptor.DefaultValue
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(value) && parameter.HasDefaultValue)
            {
                args[index] = parameter.DefaultValue;
                continue;
            }

            args[index] = ConvertToParameterType(value, parameter.ParameterType);
        }

        return args;
    }

    private static bool TryResolveCurrentCommunicationParameter(
        BusinessOperationRegistration registration,
        ParameterInfo parameter,
        ICommunication? currentCommunication,
        out object? value)
    {
        value = null;
        if (currentCommunication is null)
        {
            return false;
        }

        if (parameter.ParameterType.IsInstanceOfType(currentCommunication))
        {
            value = currentCommunication;
            return true;
        }

        if (typeof(ICommunication).IsAssignableFrom(parameter.ParameterType) ||
            typeof(ICommunicationClientSource).IsAssignableFrom(parameter.ParameterType))
        {
            throw new InvalidOperationException(
                $"Business operation '{registration.Descriptor.DeviceId}.{registration.Descriptor.OperationId}' " +
                $"requires current communication parameter '{parameter.Name}' of type '{parameter.ParameterType.Name}', " +
                "but the current communication object is incompatible.");
        }

        return false;
    }

    #endregion

    #region 类型转换

    /// <summary>
    /// 将步骤中保存的字符串参数转换成业务方法声明的目标类型。
    /// </summary>
    /// <param name="value">步骤配置中的字符串值。</param>
    /// <param name="targetType">业务方法参数目标类型。</param>
    /// <returns>转换后的参数值。</returns>
    private static object? ConvertToParameterType(string value, Type targetType)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (string.IsNullOrWhiteSpace(value) && Nullable.GetUnderlyingType(targetType) is not null)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return value;
        }

        if (type == typeof(int))
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        if (type == typeof(double))
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0d;
        }

        if (type == typeof(decimal))
        {
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) ? result : 0m;
        }

        if (type == typeof(bool))
        {
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            return value.Trim() == "1";
        }

        if (type.IsEnum)
        {
            return Enum.TryParse(type, value, true, out object? enumValue)
                ? enumValue
                : Activator.CreateInstance(type);
        }

        if (type == typeof(Guid))
        {
            return Guid.TryParse(value, out Guid result) ? result : Guid.Empty;
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    #endregion
}
