using Shared.Abstractions.ICommunication;
using Shared.Models.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Shared.Infrastructure.Communication
{
    #region 通信适配器注册

    /// <summary>
    /// 标记一个通信适配器，并声明它接收的强类型运行时配置。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class CommunicationAdapterAttribute : Attribute
    {
        /// <summary>
        /// 创建通信适配器注册信息。
        /// </summary>
        /// <param name="runtimeConfigType">适配器接收的运行时配置类型。</param>
        public CommunicationAdapterAttribute(Type runtimeConfigType)
        {
            RuntimeConfigType = runtimeConfigType;
        }

        /// <summary>
        /// 适配器接收的运行时配置类型。
        /// </summary>
        public Type RuntimeConfigType { get; }
    }

    /// <summary>
    /// 通信适配器注册表，负责将强类型运行时配置映射到具体通信适配器。
    /// </summary>
    public sealed class CommunicationAdapterRegistry
    {
        #region 字段

        /// <summary>
        /// 进程级默认适配器注册表的延迟初始化实例。
        /// </summary>
        private static readonly Lazy<CommunicationAdapterRegistry> DefaultRegistry =
            new(CreateDefaultRegistry);

        /// <summary>
        /// 已注册的适配器描述符，键为适配器接收的运行时配置类型。
        /// </summary>
        private readonly Dictionary<Type, CommunicationAdapterDescriptor> _descriptors = new();

        #endregion

        #region 属性

        /// <summary>
        /// 进程级通信适配器注册表。
        /// </summary>
        public static CommunicationAdapterRegistry Default => DefaultRegistry.Value;

        #endregion

        #region 注册与创建

        /// <summary>
        /// 扫描程序集，注册所有标记了 <see cref="CommunicationAdapterAttribute"/> 的适配器类型。
        /// </summary>
        /// <param name="assembly">待扫描的程序集。</param>
        public void RegisterFromAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (Type adapterType in GetLoadableTypes(assembly))
            {
                foreach (CommunicationAdapterAttribute attribute in adapterType.GetCustomAttributes<CommunicationAdapterAttribute>())
                {
                    Register(adapterType, attribute);
                }
            }
        }

        /// <summary>
        /// 注册单个通信适配器类型。
        /// </summary>
        /// <param name="adapterType">具体适配器 CLR 类型。</param>
        /// <param name="attribute">适配器注册元数据。</param>
        public void Register(Type adapterType, CommunicationAdapterAttribute attribute)
        {
            ArgumentNullException.ThrowIfNull(adapterType);
            ArgumentNullException.ThrowIfNull(attribute);

            CommunicationAdapterDescriptor descriptor = new(adapterType, attribute);
            _descriptors[descriptor.RuntimeConfigType] = descriptor;
        }

        /// <summary>
        /// 根据运行时通信配置创建具体通信适配器。
        /// </summary>
        /// <param name="config">运行时通信配置。</param>
        /// <returns>创建后的通信适配器。</returns>
        public CommunicationBase Create(ICommunicationRuntimeConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            Type configType = config.GetType();
            if (_descriptors.TryGetValue(configType, out CommunicationAdapterDescriptor? descriptor))
            {
                return descriptor.Create(config);
            }

            throw new NotSupportedException($"Unsupported communication runtime config: {configType.FullName}.");
        }

        /// <summary>
        /// 创建并初始化默认适配器注册表。
        /// </summary>
        /// <returns>初始化后的适配器注册表。</returns>
        private static CommunicationAdapterRegistry CreateDefaultRegistry()
        {
            CommunicationAdapterRegistry registry = new();
            registry.RegisterFromAssembly(typeof(CommunicationFactory).Assembly);
            return registry;
        }

        /// <summary>
        /// 获取程序集里可以成功加载的类型。
        /// </summary>
        /// <param name="assembly">待检查的程序集。</param>
        /// <returns>可加载的类型集合。</returns>
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
        }

        #endregion
    }

    /// <summary>
    /// 单个通信适配器注册项的描述符。
    /// </summary>
    internal sealed class CommunicationAdapterDescriptor
    {
        #region 字段

        /// <summary>
        /// 用于根据运行时配置创建适配器实例的公开构造函数。
        /// </summary>
        private readonly ConstructorInfo _constructor;

        #endregion

        #region 构造函数

        /// <summary>
        /// 根据适配器类型和注册元数据创建描述符。
        /// </summary>
        /// <param name="adapterType">具体适配器 CLR 类型。</param>
        /// <param name="attribute">适配器注册元数据。</param>
        public CommunicationAdapterDescriptor(Type adapterType, CommunicationAdapterAttribute attribute)
        {
            if (!typeof(CommunicationBase).IsAssignableFrom(adapterType))
            {
                throw new InvalidOperationException(
                    $"Communication adapter '{adapterType.FullName}' must inherit {nameof(CommunicationBase)}.");
            }

            if (!typeof(ICommunicationRuntimeConfig).IsAssignableFrom(attribute.RuntimeConfigType))
            {
                throw new InvalidOperationException(
                    $"Runtime config '{attribute.RuntimeConfigType.FullName}' must implement {nameof(ICommunicationRuntimeConfig)}.");
            }

            _constructor = adapterType.GetConstructor(new[] { attribute.RuntimeConfigType }) ??
                           throw new InvalidOperationException(
                               $"Communication adapter '{adapterType.FullName}' must declare a public constructor that accepts {attribute.RuntimeConfigType.Name}.");

            AdapterType = adapterType;
            RuntimeConfigType = attribute.RuntimeConfigType;
        }

        #endregion

        #region 属性

        /// <summary>
        /// 具体适配器 CLR 类型。
        /// </summary>
        public Type AdapterType { get; }

        /// <summary>
        /// 适配器接收的运行时配置类型。
        /// </summary>
        public Type RuntimeConfigType { get; }

        #endregion

        #region 创建

        /// <summary>
        /// 创建适配器实例。
        /// </summary>
        /// <param name="config">运行时通信配置。</param>
        /// <returns>创建后的通信适配器。</returns>
        public CommunicationBase Create(ICommunicationRuntimeConfig config)
        {
            return (CommunicationBase)_constructor.Invoke(new object[] { config });
        }

        #endregion
    }

    #endregion

    #region 运行时实例仓库

    /// <summary>
    /// 保存运行中的通信实例，并统一处理替换和移除时的关闭逻辑。
    /// </summary>
    internal sealed class CommunicationRuntimeStore
    {
        #region 字段

        /// <summary>
        /// 同步运行时通信实例字典的访问。
        /// </summary>
        private readonly object _syncRoot = new();

        /// <summary>
        /// 运行中的通信实例，按本地名称索引。
        /// </summary>
        private readonly Dictionary<string, CommunicationBase> _communications =
            new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region 生命周期

        /// <summary>
        /// 替换指定名称的通信实例；如果旧实例存在，则先关闭旧实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <param name="communication">新的通信实例。</param>
        /// <returns>已保存的通信实例。</returns>
        public CommunicationBase Replace(string name, CommunicationBase communication)
        {
            ArgumentNullException.ThrowIfNull(communication);
            string normalizedName = NormalizeRequiredName(name);

            lock (_syncRoot)
            {
                if (_communications.TryGetValue(normalizedName, out CommunicationBase? oldCommunication))
                {
                    oldCommunication.Close();
                }

                _communications[normalizedName] = communication;
                return communication;
            }
        }

        /// <summary>
        /// 根据名称获取通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <returns>通信实例；不存在时返回 <c>null</c>。</returns>
        public CommunicationBase? Get(string? name)
        {
            string normalizedName = NormalizeOptionalName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _communications.TryGetValue(normalizedName, out CommunicationBase? communication)
                    ? communication
                    : null;
            }
        }

        /// <summary>
        /// 尝试根据名称获取通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <param name="communication">查找到的通信实例。</param>
        /// <returns>名称对应的实例存在时返回 true。</returns>
        public bool TryGet(string? name, out CommunicationBase communication)
        {
            communication = null!;

            string normalizedName = NormalizeOptionalName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _communications.TryGetValue(normalizedName, out communication!);
            }
        }

        /// <summary>
        /// 移除并关闭指定名称的通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <returns>存在实例且已移除时返回 true。</returns>
        public bool Remove(string? name)
        {
            string normalizedName = NormalizeOptionalName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_communications.TryGetValue(normalizedName, out CommunicationBase? communication))
                {
                    return false;
                }

                communication.Close();
                _communications.Remove(normalizedName);
                return true;
            }
        }

        /// <summary>
        /// 规范化必填通信名称。
        /// </summary>
        /// <param name="name">原始通信名称。</param>
        /// <returns>去除首尾空白后的通信名称。</returns>
        private static string NormalizeRequiredName(string? name)
        {
            string normalizedName = NormalizeOptionalName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new ArgumentException("Communication local name is required.", nameof(name));
            }

            return normalizedName;
        }

        /// <summary>
        /// 规范化可选通信名称。
        /// </summary>
        /// <param name="name">原始通信名称。</param>
        /// <returns>去除首尾空白后的通信名称；为空时返回空字符串。</returns>
        private static string NormalizeOptionalName(string? name)
        {
            return name?.Trim() ?? string.Empty;
        }

        #endregion
    }

    #endregion

    #region 通信工厂门面

    /// <summary>
    /// 通信工厂门面，供调用方创建、查找和移除运行中的通信实例。
    /// </summary>
    public class CommunicationFactory
    {
        #region 字段

        /// <summary>
        /// 根据运行时配置创建具体适配器的注册表。
        /// </summary>
        private static readonly CommunicationAdapterRegistry AdapterRegistry = CommunicationAdapterRegistry.Default;

        /// <summary>
        /// 保存运行中通信实例的仓库。
        /// </summary>
        private static readonly CommunicationRuntimeStore RuntimeStore = new();

        #endregion

        #region 创建

        /// <summary>
        /// 根据运行时通信配置创建并保存通信实例。
        /// </summary>
        /// <param name="config">运行时通信配置。</param>
        /// <returns>创建后的通信实例。</returns>
        public static CommunicationBase CreateCommunicationProtocol(ICommunicationRuntimeConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ValidateConfig(config);

            CommunicationBase communication = AdapterRegistry.Create(config);
            return RuntimeStore.Replace(config.LocalName, communication);
        }

        #endregion

        #region 查找与移除

        /// <summary>
        /// 根据本地名称获取运行中的通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <returns>通信实例；不存在时返回 <c>null</c>。</returns>
        public static CommunicationBase Get(string? name)
        {
            return RuntimeStore.Get(name) ?? null!;
        }

        /// <summary>
        /// 尝试根据本地名称获取运行中的通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <param name="communication">查找到的通信实例。</param>
        /// <returns>名称对应的实例存在时返回 true。</returns>
        public static bool TryGet(string? name, out CommunicationBase communication)
        {
            return RuntimeStore.TryGet(name, out communication);
        }

        public static bool TryGet<T>(string? name, out T communication)
            where T : class
        {
            communication = null!;
            if (!RuntimeStore.TryGet(name, out CommunicationBase runtimeCommunication))
            {
                return false;
            }

            if (runtimeCommunication is T typedCommunication)
            {
                communication = typedCommunication;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据本地名称移除并关闭运行中的通信实例。
        /// </summary>
        /// <param name="name">通信本地名称。</param>
        /// <returns>存在实例且已移除时返回 true。</returns>
        public static bool Remove(string? name)
        {
            return RuntimeStore.Remove(name);
        }

        #endregion

        #region 校验

        /// <summary>
        /// 校验通信工厂所需的运行时配置值。
        /// </summary>
        /// <param name="config">运行时通信配置。</param>
        private static void ValidateConfig(ICommunicationRuntimeConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.LocalName))
            {
                throw new ArgumentException("Communication local name is required.", nameof(config));
            }
        }

        #endregion
    }

    #endregion
}
