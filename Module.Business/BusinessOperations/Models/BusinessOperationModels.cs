using System.Collections.Generic;

namespace Module.Business.Services.BusinessOperations;

#region 业务描述模型

/// <summary>
/// 业务设备描述，用于步骤编辑器展示可选择的设备或业务对象。
/// </summary>
/// <param name="DeviceId">设备唯一标识，也是步骤配置中保存的设备键。</param>
/// <param name="DisplayName">设备显示名称。</param>
/// <param name="Description">设备说明文本。</param>
public sealed record BusinessDeviceDescriptor(
    string DeviceId,
    string DisplayName,
    string Description);

/// <summary>
/// 业务方法参数描述，用于自动生成步骤参数编辑项。
/// </summary>
/// <param name="Name">参数代码名称，执行时按此名称取值。</param>
/// <param name="DisplayName">参数显示名称。</param>
/// <param name="TypeName">参数类型名称。</param>
/// <param name="Description">参数说明文本。</param>
/// <param name="DefaultValue">参数默认值。</param>
/// <param name="IsOptional">参数是否可选。</param>
/// <param name="Sequence">参数顺序。</param>
public sealed record BusinessParameterDescriptor(
    string Name,
    string DisplayName,
    string TypeName,
    string Description,
    string DefaultValue,
    bool IsOptional,
    int Sequence);

/// <summary>
/// 业务方法描述，用于统一表达“设备支持哪些业务、业务需要哪些参数、返回什么类型”。
/// </summary>
/// <param name="DeviceId">设备唯一标识。</param>
/// <param name="DeviceName">设备显示名称。</param>
/// <param name="OperationId">业务方法唯一标识，也是步骤配置中保存的业务键。</param>
/// <param name="DisplayName">业务方法显示名称。</param>
/// <param name="Description">业务方法说明文本。</param>
/// <param name="ReturnTypeName">返回值类型名称。</param>
/// <param name="Parameters">业务方法参数列表。</param>
public sealed record BusinessOperationDescriptor(
    string DeviceId,
    string DeviceName,
    string OperationId,
    string DisplayName,
    string Description,
    string ReturnTypeName,
    IReadOnlyList<BusinessParameterDescriptor> Parameters);

#endregion

#region 调用结果模型

/// <summary>
/// 业务方法调用结果，统一承载调用是否成功、错误消息和返回值。
/// </summary>
public sealed class BusinessOperationInvocationResult
{
    /// <summary>
    /// 创建业务方法调用结果。
    /// </summary>
    /// <param name="isSuccess">调用是否成功。</param>
    /// <param name="message">失败或提示消息。</param>
    /// <param name="result">业务方法返回值。</param>
    private BusinessOperationInvocationResult(bool isSuccess, string message, object? result)
    {
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        Result = result;
    }

    /// <summary>
    /// 调用是否成功。
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 失败或提示消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 业务方法返回值。
    /// </summary>
    public object? Result { get; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    /// <param name="result">业务方法返回值。</param>
    /// <returns>成功的业务调用结果。</returns>
    public static BusinessOperationInvocationResult Success(object? result)
    {
        return new BusinessOperationInvocationResult(true, string.Empty, result);
    }

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    /// <param name="message">失败消息。</param>
    /// <param name="result">失败时保留的返回值或异常对象。</param>
    /// <returns>失败的业务调用结果。</returns>
    public static BusinessOperationInvocationResult Failure(string message, object? result = null)
    {
        return new BusinessOperationInvocationResult(false, message, result);
    }
}

#endregion
