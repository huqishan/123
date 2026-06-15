using Shared.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Module.Business.Services.BusinessOperations;

/// <summary>
/// 业务能力目录，负责从当前应用域已加载程序集中发现带特性的业务方法。
/// </summary>
public static class BusinessOperationCatalog
{
    #region 缓存字段

    /// <summary>
    /// 业务能力缓存锁，保证多线程刷新和读取时只构建一次目录。
    /// </summary>
    private static readonly object SyncRoot = new();

    /// <summary>
    /// 已发现的业务方法注册表；为空时会按需重新扫描。
    /// </summary>
    private static IReadOnlyList<BusinessOperationRegistration>? _registrations;

    #endregion

    #region 查询入口

    /// <summary>
    /// 获取所有已发现的业务设备。
    /// </summary>
    /// <returns>业务设备描述列表。</returns>
    public static IReadOnlyList<BusinessDeviceDescriptor> GetDevices()
    {
        return GetRegistrations()
            .GroupBy(registration => registration.Descriptor.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                BusinessOperationDescriptor descriptor = group.First().Descriptor;
                return new BusinessDeviceDescriptor(
                    descriptor.DeviceId,
                    descriptor.DeviceName,
                    string.Empty);
            })
            .OrderBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 获取指定设备支持的业务方法。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
    /// <returns>该设备支持的业务方法描述列表。</returns>
    public static IReadOnlyList<BusinessOperationDescriptor> GetOperations(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<BusinessOperationDescriptor>();
        }

        return GetRegistrations()
            .Where(registration => TextEquals(registration.Descriptor.DeviceId, deviceId))
            .Select(registration => registration.Descriptor)
            .OrderBy(operation => operation.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(operation => operation.OperationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 查找指定设备上的指定业务方法描述。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
    /// <param name="operationId">业务方法唯一标识。</param>
    /// <returns>匹配的业务方法描述；找不到时返回空。</returns>
    public static BusinessOperationDescriptor? Find(string deviceId, string operationId)
    {
        return FindRegistration(deviceId, operationId)?.Descriptor;
    }

    /// <summary>
    /// 查找指定设备上的指定业务方法注册信息，供调用器拿到 MethodInfo。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
    /// <param name="operationId">业务方法唯一标识。</param>
    /// <returns>匹配的业务方法注册信息；找不到时返回空。</returns>
    internal static BusinessOperationRegistration? FindRegistration(string deviceId, string operationId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return GetRegistrations()
            .FirstOrDefault(registration =>
                TextEquals(registration.Descriptor.DeviceId, deviceId) &&
                TextEquals(registration.Descriptor.OperationId, operationId));
    }

    /// <summary>
     /// 清空业务能力缓存，下次读取时重新扫描当前应用域已加载程序集。
     /// </summary>
    public static void Refresh()
    {
        lock (SyncRoot)
        {
            _registrations = null;
        }
    }

    #endregion

    #region 缓存读取

    /// <summary>
    /// 获取业务方法注册表；首次读取时执行扫描并缓存结果。
    /// </summary>
    /// <returns>业务方法注册表。</returns>
    private static IReadOnlyList<BusinessOperationRegistration> GetRegistrations()
    {
        if (_registrations is not null)
        {
            return _registrations;
        }

        lock (SyncRoot)
        {
            _registrations ??= LoadRegistrations();
            return _registrations;
        }
    }

    #endregion

    #region 业务扫描

    /// <summary>
    /// 扫描所有候选程序集，生成业务方法注册表。
    /// </summary>
    /// <returns>扫描得到的业务方法注册表。</returns>
    private static IReadOnlyList<BusinessOperationRegistration> LoadRegistrations()
    {
        List<BusinessOperationRegistration> registrations = new();
        foreach (Assembly assembly in EnumerateBusinessAssemblies())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                DeviceBusinessAttribute? deviceAttribute = type.GetCustomAttribute<DeviceBusinessAttribute>();
                if (deviceAttribute is null || string.IsNullOrWhiteSpace(deviceAttribute.DeviceId))
                {
                    continue;
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    BusinessOperationAttribute? operationAttribute = method.GetCustomAttribute<BusinessOperationAttribute>();
                    if (operationAttribute is null)
                    {
                        continue;
                    }

                    string operationId = string.IsNullOrWhiteSpace(operationAttribute.OperationId)
                        ? method.Name
                        : operationAttribute.OperationId;
                    BusinessOperationDescriptor descriptor = new(
                        deviceAttribute.DeviceId,
                        string.IsNullOrWhiteSpace(deviceAttribute.DisplayName) ? deviceAttribute.DeviceId : deviceAttribute.DisplayName,
                        operationId,
                        string.IsNullOrWhiteSpace(operationAttribute.DisplayName) ? operationId : operationAttribute.DisplayName,
                        operationAttribute.Description,
                        GetFriendlyTypeName(UnwrapTaskReturnType(method.ReturnType)),
                        CreateParameterDescriptors(method));

                    registrations.Add(new BusinessOperationRegistration(descriptor, type, method));
                }
            }
        }

        return registrations
            .GroupBy(
                registration => $"{registration.Descriptor.DeviceId}\u001F{registration.Descriptor.OperationId}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    /// <summary>
    /// 枚举当前应用域中已加载的业务程序集。
    /// </summary>
    /// <returns>可参与业务扫描的已加载程序集序列。</returns>
    private static IEnumerable<Assembly> EnumerateBusinessAssemblies()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic))
        {
            if (seen.Add(assembly.FullName ?? assembly.Location))
            {
                yield return assembly;
            }
        }
    }

    /// <summary>
    /// 获取程序集中的可加载类型，兼容部分类型加载失败的程序集。
    /// </summary>
    /// <param name="assembly">待扫描程序集。</param>
    /// <returns>可加载类型序列。</returns>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    #endregion

    #region 描述构建

    /// <summary>
    /// 根据业务方法签名和参数特性生成参数描述。
    /// </summary>
    /// <param name="method">业务方法反射信息。</param>
    /// <returns>业务方法参数描述列表。</returns>
    private static IReadOnlyList<BusinessParameterDescriptor> CreateParameterDescriptors(MethodInfo method)
    {
        return method.GetParameters()
            .Where(parameter => !BusinessOperationInvoker.IsRuntimeParameter(parameter.ParameterType))
            .Select((parameter, index) =>
            {
                BusinessParamAttribute? attribute = parameter.GetCustomAttribute<BusinessParamAttribute>();
                string name = string.IsNullOrWhiteSpace(parameter.Name) ? $"arg{index + 1}" : parameter.Name!;
                string defaultValue = attribute?.DefaultValue ?? string.Empty;
                if (string.IsNullOrWhiteSpace(defaultValue) && parameter.HasDefaultValue && parameter.DefaultValue is not null)
                {
                    defaultValue = Convert.ToString(parameter.DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
                }

                string displayName = attribute?.DisplayName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = name;
                }

                return new BusinessParameterDescriptor(
                    name,
                    displayName,
                    GetFriendlyTypeName(parameter.ParameterType),
                    string.IsNullOrWhiteSpace(attribute?.Description) ? displayName : attribute!.Description,
                    defaultValue,
                    parameter.HasDefaultValue || !string.IsNullOrWhiteSpace(defaultValue) || IsNullable(parameter.ParameterType),
                    index + 1);
            })
            .ToArray();
    }

    #endregion

    #region 类型工具

    /// <summary>
    /// 将 Task 返回类型拆成真实业务返回类型。
    /// </summary>
    /// <param name="returnType">方法声明返回类型。</param>
    /// <returns>去掉 Task 包装后的返回类型。</returns>
    private static Type UnwrapTaskReturnType(Type returnType)
    {
        if (returnType == typeof(Task))
        {
            return typeof(void);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType;
    }

    /// <summary>
    /// 获取适合配置界面展示和保存的类型名称。
    /// </summary>
    /// <param name="type">原始类型。</param>
    /// <returns>友好的类型名称。</returns>
    private static string GetFriendlyTypeName(Type type)
    {
        Type normalizedType = Nullable.GetUnderlyingType(type) ?? type;
        if (normalizedType == typeof(void))
        {
            return "void";
        }

        if (normalizedType == typeof(string))
        {
            return "string";
        }

        if (normalizedType == typeof(int))
        {
            return "int";
        }

        if (normalizedType == typeof(double))
        {
            return "double";
        }

        if (normalizedType == typeof(decimal))
        {
            return "decimal";
        }

        if (normalizedType == typeof(bool))
        {
            return "bool";
        }

        return normalizedType.Name;
    }

    /// <summary>
    /// 判断类型是否允许空值。
    /// </summary>
    /// <param name="type">待判断类型。</param>
    /// <returns>允许空值返回 true，否则返回 false。</returns>
    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    #endregion

    #region 通用工具

    /// <summary>
    /// 按忽略大小写和首尾空白的规则比较文本。
    /// </summary>
    /// <param name="left">左侧文本。</param>
    /// <param name="right">右侧文本。</param>
    /// <returns>文本相等返回 true，否则返回 false。</returns>
    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

#region 注册模型

/// <summary>
/// 业务方法注册信息，保存描述信息和真正执行所需的反射信息。
/// </summary>
/// <param name="Descriptor">业务方法描述。</param>
/// <param name="DeclaringType">声明业务方法的类型。</param>
/// <param name="Method">业务方法反射信息。</param>
internal sealed record BusinessOperationRegistration(
    BusinessOperationDescriptor Descriptor,
    Type DeclaringType,
    MethodInfo Method);

#endregion
