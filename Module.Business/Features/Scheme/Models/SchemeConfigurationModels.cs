using ControlLibrary;
using Module.Business.Features.SchemeConfiguration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace Module.Business.Models;

/// <summary>
/// 业务配置根对象，统一保存方案配置。
/// </summary>
public sealed class SchemeConfigurationCatalog
{
    /// <summary>
    /// 业务方案集合。
    /// </summary>
    public ObservableCollection<SchemeProfile> Schemes { get; set; } = new();
}

/// <summary>
/// 方案导入导出包，包含方案本体。
/// </summary>
public sealed class SchemeConfigurationPackage
{
    /// <summary>
    /// 导入导出包版本。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 方案本体。
    /// </summary>
    public SchemeProfile? Scheme { get; set; }
}
