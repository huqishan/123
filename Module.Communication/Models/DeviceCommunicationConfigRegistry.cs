using ControlLibrary;
using Shared.Abstractions.Enum;
using Shared.Models.Communication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Module.Communication.Models;

#region 配置元数据特性

/// <summary>
/// 标记一个设备通信配置模型，并声明它在界面和运行时中的注册信息。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DeviceCommunicationConfigAttribute : Attribute
{
    /// <summary>
    /// 初始化设备通信配置模型注册特性。
    /// </summary>
    /// <param name="typeId">通信配置类型唯一标识。</param>
    /// <param name="displayName">通信配置类型显示名称。</param>
    /// <param name="builderType">负责构建运行时通信配置的 Builder 类型。</param>
    public DeviceCommunicationConfigAttribute(string typeId, string displayName, Type builderType)
    {
        TypeId = typeId;
        DisplayName = displayName;
        BuilderType = builderType;
    }

    /// <summary>
    /// 通信配置类型唯一标识。
    /// </summary>
    public new string TypeId { get; }

    /// <summary>
    /// 通信配置类型显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 负责构建运行时通信配置的 Builder 类型。
    /// </summary>
    public Type BuilderType { get; }

    /// <summary>
    /// 通信配置类型说明文本。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 通信配置类型在下拉列表中的排序值。
    /// </summary>
    public int Order { get; set; }

    public CommunicationFamily Family { get; set; } = CommunicationFamily.Standard;
}

/// <summary>
/// 标记配置模型属性如何展示为连接参数字段。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ConfigFieldAttribute : Attribute
{
    /// <summary>
    /// 初始化连接参数字段特性。
    /// </summary>
    /// <param name="label">字段显示名称。</param>
    public ConfigFieldAttribute(string label)
    {
        Label = label;
    }

    /// <summary>
    /// 字段显示名称。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 字段存储键；为空时使用属性名。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 字段在动态表单中的排序值。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 字段编辑器类型。
    /// </summary>
    public ConfigFieldEditor Editor { get; set; } = ConfigFieldEditor.Text;

    /// <summary>
    /// 字段验证类型。
    /// </summary>
    public ConfigFieldValidation Validation { get; set; } = ConfigFieldValidation.None;

    /// <summary>
    /// 字段是否必填。
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// 数值字段允许的最小值。
    /// </summary>
    public int Minimum { get; set; } = int.MinValue;

    /// <summary>
    /// 数值字段允许的最大值。
    /// </summary>
    public int Maximum { get; set; } = int.MaxValue;

    /// <summary>
    /// 静态下拉选项文本，格式为 value:display|value:display。
    /// </summary>
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// 动态下拉选项提供器类型。
    /// </summary>
    public Type? OptionsProviderType { get; set; }

    /// <summary>
    /// 下拉框是否允许用户手动输入。
    /// </summary>
    public bool IsEditable { get; set; }
}

#endregion

#region 枚举与接口

/// <summary>
/// 连接参数字段的编辑器类型。
/// </summary>
public enum ConfigFieldEditor
{
    /// <summary>
    /// 文本输入框。
    /// </summary>
    Text,

    /// <summary>
    /// 下拉选择框。
    /// </summary>
    ComboBox
}

/// <summary>
/// 连接参数字段的内置验证类型。
/// </summary>
public enum ConfigFieldValidation
{
    /// <summary>
    /// 不执行特定格式验证。
    /// </summary>
    None,

    /// <summary>
    /// 验证为 IP 地址。
    /// </summary>
    IpAddress,

    /// <summary>
    /// 验证为端口号。
    /// </summary>
    Port,

    /// <summary>
    /// 验证为整数范围。
    /// </summary>
    IntegerRange
}

public enum CommunicationFamily
{
    Standard,
    Plc,
    Can
}

/// <summary>
/// 动态提供连接参数下拉选项的接口。
/// </summary>
public interface IConfigOptionsProvider
{
    /// <summary>
    /// 获取当前字段可用的选项集合。
    /// </summary>
    /// <returns>字段下拉选项集合。</returns>
    IEnumerable<SelectionOption> GetOptions();
}

/// <summary>
/// 将配置模型转换为运行时通信配置的非泛型接口。
/// </summary>
public interface IDeviceCommunicationConfigBuilder
{
    /// <summary>
    /// 该配置模型最终对应的运行时通信类型。
    /// </summary>
    CommuniactionType RuntimeType { get; }

    /// <summary>
    /// 当前通信类型是否支持通用报文发送测试。
    /// </summary>
    bool SupportsGenericSendTest { get; }

    /// <summary>
    /// 当前通信类型是否支持 PLC 读写测试。
    /// </summary>
    bool SupportsPlcReadWriteTest { get; }

    /// <summary>
    /// 根据配置模型生成列表摘要。
    /// </summary>
    /// <param name="config">配置模型实例。</param>
    /// <returns>摘要文本。</returns>
    string BuildSummary(object config);

    /// <summary>
    /// 根据配置模型生成运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">配置模型实例。</param>
    /// <returns>运行时通信配置。</returns>
    ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, object config);
}

/// <summary>
/// 将强类型配置模型转换为运行时通信配置的基类。
/// </summary>
/// <typeparam name="TConfig">配置模型类型。</typeparam>
public abstract class DeviceCommunicationConfigBuilder<TConfig> : IDeviceCommunicationConfigBuilder
{
    /// <summary>
    /// 该配置模型最终对应的运行时通信类型。
    /// </summary>
    public abstract CommuniactionType RuntimeType { get; }

    /// <summary>
    /// 当前通信类型是否支持通用报文发送测试。
    /// </summary>
    public virtual bool SupportsGenericSendTest => true;

    /// <summary>
    /// 当前通信类型是否支持 PLC 读写测试。
    /// </summary>
    public virtual bool SupportsPlcReadWriteTest => false;

    /// <summary>
    /// 根据非泛型配置模型生成列表摘要。
    /// </summary>
    /// <param name="config">配置模型实例。</param>
    /// <returns>摘要文本。</returns>
    public string BuildSummary(object config)
    {
        return BuildSummary((TConfig)config);
    }

    /// <summary>
    /// 根据非泛型配置模型生成运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">配置模型实例。</param>
    /// <returns>运行时通信配置。</returns>
    public ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, object config)
    {
        return BuildRuntimeConfig(localName, (TConfig)config);
    }

    /// <summary>
    /// 根据强类型配置模型生成列表摘要。
    /// </summary>
    /// <param name="config">配置模型实例。</param>
    /// <returns>摘要文本。</returns>
    protected abstract string BuildSummary(TConfig config);

    /// <summary>
    /// 根据强类型配置模型生成运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">配置模型实例。</param>
    /// <returns>运行时通信配置。</returns>
    protected abstract ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, TConfig config);
}

#endregion

#region 配置注册表

/// <summary>
/// 设备通信配置模型注册表，负责自动发现配置模型并提供描述信息。
/// </summary>
public sealed class DeviceCommunicationConfigRegistry
{
    #region 字段

    /// <summary>
    /// 默认全局注册表的延迟初始化实例。
    /// </summary>
    private static readonly Lazy<DeviceCommunicationConfigRegistry> DefaultRegistry =
        new(CreateDefaultRegistry);

    /// <summary>
    /// 已注册通信配置描述符，键为通信配置类型标识。
    /// </summary>
    private readonly Dictionary<string, DeviceCommunicationConfigDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 属性

    /// <summary>
    /// 默认全局注册表。
    /// </summary>
    public static DeviceCommunicationConfigRegistry Default => DefaultRegistry.Value;

    /// <summary>
    /// 所有已注册通信配置描述符。
    /// </summary>
    public IReadOnlyList<DeviceCommunicationConfigDescriptor> Descriptors =>
        _descriptors.Values
            .OrderBy(item => item.Order)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// 默认通信配置类型标识。
    /// </summary>
    public string DefaultTypeId => Descriptors.FirstOrDefault()?.TypeId ?? string.Empty;

    #endregion

    #region 注册与查找

    /// <summary>
    /// 从指定程序集扫描并注册通信配置模型。
    /// </summary>
    /// <param name="assembly">待扫描程序集。</param>
    public void RegisterFromAssembly(Assembly assembly)
    {
        foreach (Type configType in assembly.GetTypes())
        {
            DeviceCommunicationConfigAttribute? attribute =
                configType.GetCustomAttribute<DeviceCommunicationConfigAttribute>();
            if (attribute is null)
            {
                continue;
            }

            Register(configType, attribute);
        }
    }

    /// <summary>
    /// 判断指定通信配置类型是否已注册。
    /// </summary>
    /// <param name="typeId">通信配置类型标识。</param>
    /// <returns>已注册返回 true，否则返回 false。</returns>
    public bool Contains(string? typeId)
    {
        return !string.IsNullOrWhiteSpace(typeId) && _descriptors.ContainsKey(typeId);
    }

    /// <summary>
    /// 获取指定通信配置类型的描述符。
    /// </summary>
    /// <param name="typeId">通信配置类型标识。</param>
    /// <returns>通信配置描述符。</returns>
    public DeviceCommunicationConfigDescriptor Get(string typeId)
    {
        if (_descriptors.TryGetValue(typeId, out DeviceCommunicationConfigDescriptor? descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException($"Device communication type '{typeId}' is not registered.");
    }

    /// <summary>
    /// 获取指定通信配置类型描述符；不存在时返回默认类型描述符。
    /// </summary>
    /// <param name="typeId">通信配置类型标识。</param>
    /// <returns>通信配置描述符。</returns>
    public DeviceCommunicationConfigDescriptor GetOrDefault(string? typeId)
    {
        if (!string.IsNullOrWhiteSpace(typeId) &&
            _descriptors.TryGetValue(typeId, out DeviceCommunicationConfigDescriptor? descriptor))
        {
            return descriptor;
        }

        string defaultTypeId = DefaultTypeId;
        if (string.IsNullOrWhiteSpace(defaultTypeId))
        {
            throw new InvalidOperationException("No device communication config model is registered.");
        }

        return _descriptors[defaultTypeId];
    }

    /// <summary>
    /// 注册单个通信配置模型类型。
    /// </summary>
    /// <param name="configType">配置模型类型。</param>
    /// <param name="attribute">配置模型注册特性。</param>
    private void Register(Type configType, DeviceCommunicationConfigAttribute attribute)
    {
        if (!typeof(IDeviceCommunicationConfigBuilder).IsAssignableFrom(attribute.BuilderType))
        {
            throw new InvalidOperationException(
                $"Builder '{attribute.BuilderType.FullName}' must implement {nameof(IDeviceCommunicationConfigBuilder)}.");
        }

        if (Activator.CreateInstance(attribute.BuilderType) is not IDeviceCommunicationConfigBuilder builder)
        {
            throw new InvalidOperationException($"Builder '{attribute.BuilderType.FullName}' cannot be created.");
        }

        DeviceCommunicationConfigDescriptor descriptor = new(configType, attribute, builder);
        _descriptors[descriptor.TypeId] = descriptor;
    }

    /// <summary>
    /// 创建并初始化默认全局注册表。
    /// </summary>
    /// <returns>默认全局注册表。</returns>
    private static DeviceCommunicationConfigRegistry CreateDefaultRegistry()
    {
        DeviceCommunicationConfigRegistry registry = new();
        registry.RegisterFromAssembly(typeof(DeviceCommunicationConfigRegistry).Assembly);
        return registry;
    }

    #endregion
}

#endregion

#region 配置描述符

/// <summary>
/// 单个设备通信配置模型的完整描述。
/// </summary>
public sealed class DeviceCommunicationConfigDescriptor
{
    #region 字段

    /// <summary>
    /// 配置模型 CLR 类型。
    /// </summary>
    private readonly Type _configType;

    /// <summary>
    /// 配置模型到运行时通信配置的构建器。
    /// </summary>
    private readonly IDeviceCommunicationConfigBuilder _builder;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化设备通信配置描述符。
    /// </summary>
    /// <param name="configType">配置模型类型。</param>
    /// <param name="attribute">配置模型注册特性。</param>
    /// <param name="builder">运行时配置构建器。</param>
    public DeviceCommunicationConfigDescriptor(
        Type configType,
        DeviceCommunicationConfigAttribute attribute,
        IDeviceCommunicationConfigBuilder builder)
    {
        _configType = configType;
        _builder = builder;
        TypeId = attribute.TypeId;
        DisplayName = attribute.DisplayName;
        Description = attribute.Description;
        Family = attribute.Family;
        Order = attribute.Order;
        RuntimeType = builder.RuntimeType;
        SupportsGenericSendTest = builder.SupportsGenericSendTest;
        SupportsPlcReadWriteTest = builder.SupportsPlcReadWriteTest;
        Fields = CreateFields(configType);
    }

    #endregion

    #region 属性

    /// <summary>
    /// 通信配置类型唯一标识。
    /// </summary>
    public string TypeId { get; }

    /// <summary>
    /// 通信配置类型显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 通信配置类型说明文本。
    /// </summary>
    public string Description { get; }

    public CommunicationFamily Family { get; }

    /// <summary>
    /// 通信配置类型排序值。
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public CommuniactionType RuntimeType { get; }

    /// <summary>
    /// 是否支持通用报文发送测试。
    /// </summary>
    public bool SupportsGenericSendTest { get; }

    /// <summary>
    /// 是否支持 PLC 读写测试。
    /// </summary>
    public bool SupportsPlcReadWriteTest { get; }

    /// <summary>
    /// 当前配置类型是否为串口通信。
    /// </summary>
    public bool IsSerialPort => RuntimeType == CommuniactionType.COM;

    /// <summary>
    /// 配置模型字段描述集合。
    /// </summary>
    public IReadOnlyList<DeviceCommunicationConfigFieldDescriptor> Fields { get; }

    #endregion

    #region 参数与字段模型

    /// <summary>
    /// 根据配置模型默认值创建参数字典。
    /// </summary>
    /// <returns>默认参数字典。</returns>
    public Dictionary<string, string> CreateDefaultParameters()
    {
        object config = Activator.CreateInstance(_configType) ??
                        throw new InvalidOperationException($"Cannot create config '{_configType.FullName}'.");
        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase);

        foreach (DeviceCommunicationConfigFieldDescriptor field in Fields)
        {
            object? value = field.Property.GetValue(config);
            parameters[field.Key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return parameters;
    }

    /// <summary>
    /// 为指定设备通信配置创建动态字段视图模型。
    /// </summary>
    /// <param name="profile">设备通信配置。</param>
    /// <returns>动态字段视图模型集合。</returns>
    public IEnumerable<DeviceCommunicationConfigFieldViewModel> CreateFieldViewModels(DeviceCommunicationProfile profile)
    {
        Dictionary<string, string> defaults = CreateDefaultParameters();
        foreach (DeviceCommunicationConfigFieldDescriptor field in Fields)
        {
            if (!profile.HasParameter(field.Key) && defaults.TryGetValue(field.Key, out string? defaultValue))
            {
                profile.SetParameter(field.Key, defaultValue, raiseChanged: false);
            }

            yield return new DeviceCommunicationConfigFieldViewModel(profile, field);
        }
    }

    /// <summary>
    /// 从配置模型类型读取字段描述。
    /// </summary>
    /// <param name="configType">配置模型类型。</param>
    /// <returns>字段描述集合。</returns>
    private static IReadOnlyList<DeviceCommunicationConfigFieldDescriptor> CreateFields(Type configType)
    {
        return configType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<ConfigFieldAttribute>()
            })
            .Where(item => item.Attribute is not null)
            .Select(item => new DeviceCommunicationConfigFieldDescriptor(item.Property, item.Attribute!))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    #endregion

    #region 运行时配置构建

    /// <summary>
    /// 尝试构建设备运行时通信配置。
    /// </summary>
    /// <param name="profile">设备通信配置。</param>
    /// <param name="config">构建成功后的运行时通信配置。</param>
    /// <param name="validationMessage">验证或构建结果消息。</param>
    /// <returns>构建成功返回 true，否则返回 false。</returns>
    public bool TryBuildRuntimeConfig(
        DeviceCommunicationProfile profile,
        out ICommunicationRuntimeConfig? config,
        out string validationMessage)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(profile.LocalName))
        {
            validationMessage = "配置名称不能为空。";
            return false;
        }

        if (!TryCreateConfigObject(profile, validate: true, out object? configObject, out validationMessage) ||
            configObject is null)
        {
            return false;
        }

        try
        {
            config = _builder.BuildRuntimeConfig(profile.LocalName.Trim(), configObject);
            validationMessage = $"{DisplayName}配置有效。";
            return true;
        }
        catch (Exception ex)
        {
            validationMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 根据设备通信配置生成列表摘要。
    /// </summary>
    /// <param name="profile">设备通信配置。</param>
    /// <returns>摘要文本。</returns>
    public string BuildSummary(DeviceCommunicationProfile profile)
    {
        if (!TryCreateConfigObject(profile, validate: false, out object? configObject, out _) ||
            configObject is null)
        {
            return $"{DisplayName}配置未完成";
        }

        return _builder.BuildSummary(configObject);
    }

    /// <summary>
    /// 将参数字典转换为强类型配置模型。
    /// </summary>
    /// <param name="profile">设备通信配置。</param>
    /// <param name="validate">是否执行字段验证。</param>
    /// <param name="config">转换后的强类型配置模型。</param>
    /// <param name="validationMessage">验证结果消息。</param>
    /// <returns>转换成功返回 true，否则返回 false。</returns>
    private bool TryCreateConfigObject(
        DeviceCommunicationProfile profile,
        bool validate,
        out object? config,
        out string validationMessage)
    {
        config = Activator.CreateInstance(_configType);
        if (config is null)
        {
            validationMessage = $"无法创建配置模型：{_configType.Name}。";
            return false;
        }

        Dictionary<string, string> defaults = CreateDefaultParameters();
        foreach (DeviceCommunicationConfigFieldDescriptor field in Fields)
        {
            string rawValue = profile.GetParameter(field.Key);
            if (string.IsNullOrWhiteSpace(rawValue) && defaults.TryGetValue(field.Key, out string? defaultValue))
            {
                rawValue = defaultValue;
            }

            if (!field.TryConvert(rawValue, validate, out object? convertedValue, out validationMessage))
            {
                return false;
            }

            field.Property.SetValue(config, convertedValue);
        }

        validationMessage = string.Empty;
        return true;
    }

    #endregion
}

#endregion

#region 字段描述与字段视图模型

/// <summary>
/// 单个连接参数字段的描述信息。
/// </summary>
public sealed class DeviceCommunicationConfigFieldDescriptor
{
    #region 构造函数

    /// <summary>
    /// 初始化连接参数字段描述。
    /// </summary>
    /// <param name="property">配置模型属性。</param>
    /// <param name="attribute">字段展示特性。</param>
    public DeviceCommunicationConfigFieldDescriptor(PropertyInfo property, ConfigFieldAttribute attribute)
    {
        Property = property;
        Key = string.IsNullOrWhiteSpace(attribute.Key) ? property.Name : attribute.Key.Trim();
        Label = attribute.Label;
        Order = attribute.Order;
        Editor = attribute.Editor;
        Validation = attribute.Validation;
        IsRequired = attribute.IsRequired;
        Minimum = attribute.Minimum;
        Maximum = attribute.Maximum;
        OptionsText = attribute.Options;
        OptionsProviderType = attribute.OptionsProviderType;
        IsEditable = attribute.IsEditable;
    }

    #endregion

    #region 属性

    /// <summary>
    /// 对应的配置模型属性。
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// 字段存储键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 字段显示名称。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 字段排序值。
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// 字段编辑器类型。
    /// </summary>
    public ConfigFieldEditor Editor { get; }

    /// <summary>
    /// 字段验证类型。
    /// </summary>
    public ConfigFieldValidation Validation { get; }

    /// <summary>
    /// 字段是否必填。
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// 数值字段允许的最小值。
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// 数值字段允许的最大值。
    /// </summary>
    public int Maximum { get; }

    /// <summary>
    /// 静态下拉选项文本。
    /// </summary>
    public string OptionsText { get; }

    /// <summary>
    /// 动态下拉选项提供器类型。
    /// </summary>
    public Type? OptionsProviderType { get; }

    /// <summary>
    /// 下拉框是否允许用户手动输入。
    /// </summary>
    public bool IsEditable { get; }

    #endregion

    #region 选项与转换

    /// <summary>
    /// 获取字段下拉选项。
    /// </summary>
    /// <returns>字段下拉选项集合。</returns>
    public IEnumerable<SelectionOption> GetOptions()
    {
        if (OptionsProviderType is not null)
        {
            if (!typeof(IConfigOptionsProvider).IsAssignableFrom(OptionsProviderType))
            {
                throw new InvalidOperationException(
                    $"Options provider '{OptionsProviderType.FullName}' must implement {nameof(IConfigOptionsProvider)}.");
            }

            if (Activator.CreateInstance(OptionsProviderType) is IConfigOptionsProvider provider)
            {
                return provider.GetOptions();
            }
        }

        if (string.IsNullOrWhiteSpace(OptionsText))
        {
            return Array.Empty<SelectionOption>();
        }

        return OptionsText
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseOption)
            .ToArray();
    }

    /// <summary>
    /// 将字符串参数转换为配置模型属性值。
    /// </summary>
    /// <param name="rawValue">原始字符串参数。</param>
    /// <param name="validate">是否执行字段验证。</param>
    /// <param name="convertedValue">转换后的属性值。</param>
    /// <param name="validationMessage">验证结果消息。</param>
    /// <returns>转换成功返回 true，否则返回 false。</returns>
    public bool TryConvert(
        string rawValue,
        bool validate,
        out object? convertedValue,
        out string validationMessage)
    {
        rawValue ??= string.Empty;
        string value = rawValue.Trim();
        convertedValue = null;

        if (IsRequired && string.IsNullOrWhiteSpace(value))
        {
            validationMessage = $"{Label}不能为空。";
            return false;
        }

        if (!TryValidate(value, validate, out validationMessage))
        {
            return false;
        }

        Type targetType = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        if (targetType == typeof(string))
        {
            convertedValue = value;
            return true;
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                convertedValue = number;
                return true;
            }

            validationMessage = $"{Label}必须是数字。";
            return false;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(value, out bool boolValue))
            {
                convertedValue = boolValue;
                return true;
            }

            validationMessage = $"{Label}必须是 true 或 false。";
            return false;
        }

        validationMessage = $"字段 {Label} 不支持类型 {targetType.Name}。";
        return false;
    }

    /// <summary>
    /// 根据字段验证类型验证字符串参数。
    /// </summary>
    /// <param name="value">待验证字符串。</param>
    /// <param name="validate">是否执行字段验证。</param>
    /// <param name="validationMessage">验证结果消息。</param>
    /// <returns>验证通过返回 true，否则返回 false。</returns>
    private bool TryValidate(string value, bool validate, out string validationMessage)
    {
        validationMessage = string.Empty;
        if (!validate || string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (Validation)
        {
            case ConfigFieldValidation.IpAddress:
                if (!IPAddress.TryParse(value, out _))
                {
                    validationMessage = $"{Label}格式无效。";
                    return false;
                }

                return true;

            case ConfigFieldValidation.Port:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) ||
                    port < Minimum ||
                    port > Maximum ||
                    port < 0 ||
                    port > ushort.MaxValue)
                {
                    int min = Math.Max(0, Minimum);
                    int max = Math.Min(ushort.MaxValue, Maximum);
                    validationMessage = $"{Label}必须是 {min} 到 {max} 之间的数字。";
                    return false;
                }

                return true;

            case ConfigFieldValidation.IntegerRange:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ||
                    number < Minimum ||
                    number > Maximum)
                {
                    validationMessage = $"{Label}必须在 {Minimum} 到 {Maximum} 之间。";
                    return false;
                }

                return true;

            default:
                return true;
        }
    }

    /// <summary>
    /// 将静态选项文本转换为选项对象。
    /// </summary>
    /// <param name="value">静态选项文本。</param>
    /// <returns>选项对象。</returns>
    private static SelectionOption ParseOption(string value)
    {
        string[] parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? new SelectionOption(parts[0], parts[1])
            : new SelectionOption(value, value);
    }

    #endregion
}

/// <summary>
/// 连接参数动态字段的界面绑定模型。
/// </summary>
public sealed class DeviceCommunicationConfigFieldViewModel : ViewModelProperties
{
    #region 字段

    /// <summary>
    /// 当前字段所属的设备通信配置。
    /// </summary>
    private readonly DeviceCommunicationProfile _profile;

    /// <summary>
    /// 当前字段描述。
    /// </summary>
    private readonly DeviceCommunicationConfigFieldDescriptor _field;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化连接参数字段绑定模型。
    /// </summary>
    /// <param name="profile">设备通信配置。</param>
    /// <param name="field">字段描述。</param>
    public DeviceCommunicationConfigFieldViewModel(
        DeviceCommunicationProfile profile,
        DeviceCommunicationConfigFieldDescriptor field)
    {
        _profile = profile;
        _field = field;

        foreach (SelectionOption option in field.GetOptions())
        {
            Options.Add(option);
        }
    }

    #endregion

    #region 属性

    /// <summary>
    /// 字段存储键。
    /// </summary>
    public string Key => _field.Key;

    /// <summary>
    /// 字段显示名称。
    /// </summary>
    public string Label => _field.Label;

    /// <summary>
    /// 字段编辑器类型。
    /// </summary>
    public ConfigFieldEditor Editor => _field.Editor;

    /// <summary>
    /// 当前字段是否使用文本输入框。
    /// </summary>
    public bool IsTextEditor => Editor == ConfigFieldEditor.Text;

    /// <summary>
    /// 当前字段是否使用下拉选择框。
    /// </summary>
    public bool IsComboBoxEditor => Editor == ConfigFieldEditor.ComboBox;

    /// <summary>
    /// 下拉选择框是否允许手动输入。
    /// </summary>
    public bool IsEditable => _field.IsEditable;

    /// <summary>
    /// 当前字段可选项集合。
    /// </summary>
    public ObservableCollection<SelectionOption> Options { get; } = new();

    /// <summary>
    /// 当前字段值。
    /// </summary>
    public string Value
    {
        get => _profile.GetParameter(Key);
        set
        {
            if (_profile.SetParameter(Key, value ?? string.Empty))
            {
                OnPropertyChanged();
            }
        }
    }

    #endregion
}

#endregion

#region 选项提供器

/// <summary>
/// 串口号下拉选项提供器。
/// </summary>
public sealed class SerialPortOptionsProvider : IConfigOptionsProvider
{
    #region 选项读取

    /// <summary>
    /// 获取当前机器可用串口号选项。
    /// </summary>
    /// <returns>串口号选项集合。</returns>
    public IEnumerable<SelectionOption> GetOptions()
    {
        string[] portNames = GetDetectedPortNames();
        if (portNames.Length == 0)
        {
            portNames = ["COM1"];
        }

        return portNames.Select(portName => new SelectionOption(portName, portName));
    }

    /// <summary>
    /// 读取当前机器检测到的串口号。
    /// </summary>
    /// <returns>串口号数组。</returns>
    public static string[] GetDetectedPortNames()
    {
        try
        {
            return SerialPort.GetPortNames()
                .Where(portName => !string.IsNullOrWhiteSpace(portName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetSerialPortSortNumber)
                .ThenBy(portName => portName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 提取串口号中的数字部分用于排序。
    /// </summary>
    /// <param name="portName">串口号。</param>
    /// <returns>串口排序值。</returns>
    private static int GetSerialPortSortNumber(string portName)
    {
        if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(portName[3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            return number;
        }

        return int.MaxValue;
    }

    #endregion
}

#endregion

#region TCP 客户端配置

/// <summary>
/// TCP 客户端通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "tcp-client",
    "TCP客户端",
    typeof(TcpClientConfigBuilder),
    Description = "主动连接远端设备。",
    Order = 10)]
public sealed class TcpClientConfig
{
    /// <summary>
    /// 远端 IP 地址。
    /// </summary>
    [ConfigField("远端 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string RemoteIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 远端端口。
    /// </summary>
    [ConfigField("远端端口", Order = 20, Validation = ConfigFieldValidation.Port, Minimum = 1)]
    public int RemotePort { get; set; } = 502;

    /// <summary>
    /// 本地 IP 地址。
    /// </summary>
    [ConfigField("本地 IP 地址", Order = 30, Validation = ConfigFieldValidation.IpAddress)]
    public string LocalIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 本地端口；0 表示由系统分配。
    /// </summary>
    [ConfigField("本地端口", Order = 40, Validation = ConfigFieldValidation.Port, Minimum = 0)]
    public int LocalPort { get; set; }
}

/// <summary>
/// TCP 客户端通信配置构建器。
/// </summary>
public sealed class TcpClientConfigBuilder : DeviceCommunicationConfigBuilder<TcpClientConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.TCPClient;

    /// <summary>
    /// 生成 TCP 客户端配置摘要。
    /// </summary>
    /// <param name="config">TCP 客户端配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(TcpClientConfig config)
    {
        return $"远端 {config.RemoteIpAddress}:{config.RemotePort}  本地 {config.LocalIpAddress}:{config.LocalPort}";
    }

    /// <summary>
    /// 构建 TCP 客户端运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">TCP 客户端配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, TcpClientConfig config)
    {
        return new TcpClientRuntimeConfig(
            localName,
            config.RemoteIpAddress,
            config.RemotePort,
            config.LocalIpAddress,
            config.LocalPort);
    }
}

#endregion

#region TCP 服务端配置

/// <summary>
/// TCP 服务端通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "tcp-server",
    "TCP服务端",
    typeof(TcpServerConfigBuilder),
    Description = "启动本地监听并等待外部连接。",
    Order = 20)]
public sealed class TcpServerConfig
{
    /// <summary>
    /// 本地监听 IP 地址。
    /// </summary>
    [ConfigField("本地 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string LocalIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 本地监听端口。
    /// </summary>
    [ConfigField("本地端口", Order = 20, Validation = ConfigFieldValidation.Port, Minimum = 1)]
    public int LocalPort { get; set; } = 6000;
}

/// <summary>
/// TCP 服务端通信配置构建器。
/// </summary>
public sealed class TcpServerConfigBuilder : DeviceCommunicationConfigBuilder<TcpServerConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.TCPServer;

    /// <summary>
    /// 生成 TCP 服务端配置摘要。
    /// </summary>
    /// <param name="config">TCP 服务端配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(TcpServerConfig config)
    {
        return $"监听 {config.LocalIpAddress}:{config.LocalPort}";
    }

    /// <summary>
    /// 构建 TCP 服务端运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">TCP 服务端配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, TcpServerConfig config)
    {
        return new TcpServerRuntimeConfig(localName, config.LocalIpAddress, config.LocalPort);
    }
}

#endregion

#region UDP 配置

/// <summary>
/// UDP 通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "udp",
    "UDP",
    typeof(UdpConfigBuilder),
    Description = "无连接数据报通信。",
    Order = 30)]
public sealed class UdpConfig
{
    /// <summary>
    /// 远端 IP 地址。
    /// </summary>
    [ConfigField("远端 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string RemoteIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 远端端口。
    /// </summary>
    [ConfigField("远端端口", Order = 20, Validation = ConfigFieldValidation.Port, Minimum = 1)]
    public int RemotePort { get; set; } = 7000;

    /// <summary>
    /// 本地 IP 地址。
    /// </summary>
    [ConfigField("本地 IP 地址", Order = 30, Validation = ConfigFieldValidation.IpAddress)]
    public string LocalIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 本地端口。
    /// </summary>
    [ConfigField("本地端口", Order = 40, Validation = ConfigFieldValidation.Port, Minimum = 0)]
    public int LocalPort { get; set; } = 7001;
}

/// <summary>
/// UDP 通信配置构建器。
/// </summary>
public sealed class UdpConfigBuilder : DeviceCommunicationConfigBuilder<UdpConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.UDP;

    /// <summary>
    /// 生成 UDP 配置摘要。
    /// </summary>
    /// <param name="config">UDP 配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(UdpConfig config)
    {
        return $"远端 {config.RemoteIpAddress}:{config.RemotePort}  本地 {config.LocalIpAddress}:{config.LocalPort}";
    }

    /// <summary>
    /// 构建 UDP 运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">UDP 配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, UdpConfig config)
    {
        return new UdpClientRuntimeConfig(
            localName,
            config.RemoteIpAddress,
            config.RemotePort,
            config.LocalIpAddress,
            config.LocalPort);
    }
}

#endregion

#region 串口配置

/// <summary>
/// 串口通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "serial-port",
    "串口",
    typeof(SerialPortConfigBuilder),
    Description = "串口通信。",
    Order = 40)]
public sealed class SerialPortConfig
{
    /// <summary>
    /// 串口名称。
    /// </summary>
    [ConfigField(
        "端口名称",
        Order = 10,
        Editor = ConfigFieldEditor.ComboBox,
        OptionsProviderType = typeof(SerialPortOptionsProvider),
        IsEditable = true)]
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// 串口波特率。
    /// </summary>
    [ConfigField("波特率", Order = 20, Editor = ConfigFieldEditor.ComboBox, Options = "1200|2400|4800|9600|19200|38400|57600|115200")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 串口校验位。
    /// </summary>
    [ConfigField("校验位", Order = 30, Editor = ConfigFieldEditor.ComboBox, Options = "0:0 - 无|1:1 - 奇校验|2:2 - 偶校验|3:3 - 标记|4:4 - 空格")]
    public int Parity { get; set; }

    /// <summary>
    /// 串口数据位。
    /// </summary>
    [ConfigField("数据位", Order = 40, Editor = ConfigFieldEditor.ComboBox, Options = "5|6|7|8")]
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 串口停止位。
    /// </summary>
    [ConfigField("停止位", Order = 50, Editor = ConfigFieldEditor.ComboBox, Options = "0:0 - 无|1:1 - 1位|2:2 - 2位|3:3 - 1.5位")]
    public int StopBits { get; set; } = 1;
}

/// <summary>
/// 串口通信配置构建器。
/// </summary>
public sealed class SerialPortConfigBuilder : DeviceCommunicationConfigBuilder<SerialPortConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.COM;

    /// <summary>
    /// 生成串口配置摘要。
    /// </summary>
    /// <param name="config">串口配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(SerialPortConfig config)
    {
        return $"{config.PortName}  波特率 {config.BaudRate}bps  校验位 {config.Parity}  数据位 {config.DataBits}  停止位 {config.StopBits}";
    }

    /// <summary>
    /// 构建串口运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">串口配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, SerialPortConfig config)
    {
        return new SerialPortRuntimeConfig(
            localName,
            config.PortName,
            config.BaudRate,
            config.Parity,
            config.DataBits,
            config.StopBits);
    }
}

#endregion

#region PLC Modbus 配置

/// <summary>
/// PLC Modbus TCP 通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "plc-modbus",
    "PLC Modbus TCP",
    typeof(PlcModbusConfigBuilder),
    Family = CommunicationFamily.Plc,
    Description = "使用 Modbus TCP 的 PLC 通信。",
    Order = 50)]
public sealed class PlcModbusConfig
{
    /// <summary>
    /// 远端 IP 地址。
    /// </summary>
    [ConfigField("远端 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string RemoteIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 远端端口。
    /// </summary>
    [ConfigField("远端端口", Order = 20, Validation = ConfigFieldValidation.Port, Minimum = 1)]
    public int RemotePort { get; set; } = 502;

    /// <summary>
    /// 本地 IP 地址。
    /// </summary>
    [ConfigField("本地 IP 地址", Order = 30, Validation = ConfigFieldValidation.IpAddress)]
    public string LocalIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 本地端口。
    /// </summary>
    [ConfigField("本地端口", Order = 40, Validation = ConfigFieldValidation.Port, Minimum = 0)]
    public int LocalPort { get; set; }
}

/// <summary>
/// PLC Modbus TCP 通信配置构建器。
/// </summary>
public sealed class PlcModbusConfigBuilder : DeviceCommunicationConfigBuilder<PlcModbusConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.PLC;

    /// <summary>
    /// 是否支持通用报文发送测试。
    /// </summary>
    public override bool SupportsGenericSendTest => false;

    /// <summary>
    /// 是否支持 PLC 读写测试。
    /// </summary>
    public override bool SupportsPlcReadWriteTest => true;

    /// <summary>
    /// 生成 PLC Modbus TCP 配置摘要。
    /// </summary>
    /// <param name="config">PLC Modbus TCP 配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(PlcModbusConfig config)
    {
        return $"PLC Modbus  远端 {config.RemoteIpAddress}:{config.RemotePort}";
    }

    /// <summary>
    /// 构建 PLC Modbus TCP 运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">PLC Modbus TCP 配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, PlcModbusConfig config)
    {
        return new ModbusTcpPlcRuntimeConfig(
            localName,
            config.RemoteIpAddress,
            config.RemotePort,
            config.LocalIpAddress,
            config.LocalPort);
    }
}

#endregion

#region PLC MX 配置

/// <summary>
/// PLC MX 通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "plc-mx",
    "PLC MX",
    typeof(PlcMxConfigBuilder),
    Family = CommunicationFamily.Plc,
    Description = "使用三菱 MX 逻辑站模式的 PLC 通信。",
    Order = 60)]
public sealed class PlcMxConfig
{
    /// <summary>
    /// PLC 逻辑站号。
    /// </summary>
    [ConfigField("PLC 逻辑站号", Order = 10, Validation = ConfigFieldValidation.IntegerRange, Minimum = 0, Maximum = 1023)]
    public int StationNumber { get; set; }

    /// <summary>
    /// PLC 访问密码。
    /// </summary>
    [ConfigField("PLC 密码", Order = 20, IsRequired = false)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// PLC MX 通信配置构建器。
/// </summary>
public sealed class PlcMxConfigBuilder : DeviceCommunicationConfigBuilder<PlcMxConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.PLC;

    /// <summary>
    /// 是否支持通用报文发送测试。
    /// </summary>
    public override bool SupportsGenericSendTest => false;

    /// <summary>
    /// 是否支持 PLC 读写测试。
    /// </summary>
    public override bool SupportsPlcReadWriteTest => true;

    /// <summary>
    /// 生成 PLC MX 配置摘要。
    /// </summary>
    /// <param name="config">PLC MX 配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(PlcMxConfig config)
    {
        return $"PLC MX  逻辑站号 {config.StationNumber}";
    }

    /// <summary>
    /// 构建 PLC MX 运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">PLC MX 配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, PlcMxConfig config)
    {
        return new MxPlcRuntimeConfig(
            localName,
            config.StationNumber,
            config.Password);
    }
}

#endregion

#region PLC S7 配置

/// <summary>
/// PLC S7 通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "plc-s7",
    "PLC S7",
    typeof(PlcS7ConfigBuilder),
    Family = CommunicationFamily.Plc,
    Description = "使用 Siemens S7 以太网协议的 PLC 通信。",
    Order = 70)]
public sealed class PlcS7Config
{
    /// <summary>
    /// S7 PLC 远端 IP 地址。
    /// </summary>
    [ConfigField("远端 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string RemoteIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// S7 PLC CPU 类型。
    /// </summary>
    [ConfigField("PLC CPU 类型", Order = 20, Editor = ConfigFieldEditor.ComboBox, Options = "S7200:S7-200|S7300:S7-300|S7400:S7-400|S71200:S7-1200|S71500:S7-1500")]
    public string CpuType { get; set; } = S7CpuTypeNames.S71200;

    /// <summary>
    /// S7 PLC Rack 编号。
    /// </summary>
    [ConfigField("PLC Rack", Order = 30, Validation = ConfigFieldValidation.IntegerRange, Minimum = 0, Maximum = 7)]
    public int Rack { get; set; }

    /// <summary>
    /// S7 PLC Slot 编号。
    /// </summary>
    [ConfigField("PLC Slot", Order = 40, Validation = ConfigFieldValidation.IntegerRange, Minimum = 0, Maximum = 31)]
    public int Slot { get; set; } = 1;
}

/// <summary>
/// PLC S7 通信配置构建器。
/// </summary>
public sealed class PlcS7ConfigBuilder : DeviceCommunicationConfigBuilder<PlcS7Config>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.PLC;

    /// <summary>
    /// 是否支持通用报文发送测试。
    /// </summary>
    public override bool SupportsGenericSendTest => false;

    /// <summary>
    /// 是否支持 PLC 读写测试。
    /// </summary>
    public override bool SupportsPlcReadWriteTest => true;

    /// <summary>
    /// 生成 PLC S7 配置摘要。
    /// </summary>
    /// <param name="config">PLC S7 配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(PlcS7Config config)
    {
        return $"PLC S7  {config.CpuType}  {config.RemoteIpAddress}  Rack {config.Rack}  Slot {config.Slot}";
    }

    /// <summary>
    /// 构建 PLC S7 运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">PLC S7 配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, PlcS7Config config)
    {
        return new S7PlcRuntimeConfig(
            localName,
            config.RemoteIpAddress,
            config.CpuType,
            config.Rack,
            config.Slot);
    }
}

#endregion

#region CAN TCPCAN 配置

/// <summary>
/// TCP CAN 通信配置模型。
/// </summary>
[DeviceCommunicationConfig(
    "can-tcp",
    "TCP CAN",
    typeof(TcpCanConfigBuilder),
    Family = CommunicationFamily.Can,
    Description = "使用 TCP 的 CAN 通信。",
    Order = 80)]
public sealed class TcpCanConfig
{
    /// <summary>
    /// 远端 IP 地址。
    /// </summary>
    [ConfigField("远端 IP 地址", Order = 10, Validation = ConfigFieldValidation.IpAddress)]
    public string RemoteIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// 远端端口。
    /// </summary>
    [ConfigField("远端端口", Order = 20, Validation = ConfigFieldValidation.Port, Minimum = 1)]
    public int RemotePort { get; set; } = 502;

    /// <summary>
    /// 波特率。
    /// </summary>
    [ConfigField("波特率", Order = 30, Validation = ConfigFieldValidation.IntegerRange, Minimum = 1)]
    public int BaudRate { get; set; } = 500;


}

/// <summary>
/// PLC Modbus TCP 通信配置构建器。
/// </summary>
public sealed class TcpCanConfigBuilder : DeviceCommunicationConfigBuilder<TcpCanConfig>
{
    /// <summary>
    /// 运行时通信类型。
    /// </summary>
    public override CommuniactionType RuntimeType => CommuniactionType.CAN;

    /// <summary>
    /// 是否支持通用报文发送测试。
    /// </summary>
    public override bool SupportsGenericSendTest => true;

    /// <summary>
    /// 是否支持 PLC 读写测试。
    /// </summary>
    public override bool SupportsPlcReadWriteTest => false;

    /// <summary>
    /// 生成 PLC Modbus TCP 配置摘要。
    /// </summary>
    /// <param name="config">PLC Modbus TCP 配置。</param>
    /// <returns>摘要文本。</returns>
    protected override string BuildSummary(TcpCanConfig config)
    {
        return $"PLC Modbus  远端 {config.RemoteIpAddress}:{config.RemotePort}";
    }

    /// <summary>
    /// 构建 PLC Modbus TCP 运行时通信配置。
    /// </summary>
    /// <param name="localName">设备配置名称。</param>
    /// <param name="config">PLC Modbus TCP 配置。</param>
    /// <returns>运行时通信配置。</returns>
    protected override ICommunicationRuntimeConfig BuildRuntimeConfig(string localName, TcpCanConfig config)
    {
        return new ModbusTcpPlcRuntimeConfig(
            localName,
            config.RemoteIpAddress,
            config.RemotePort,
            "",
            3);
    }
}

#endregion
