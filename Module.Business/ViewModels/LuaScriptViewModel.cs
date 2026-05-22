using ControlLibrary;
using Module.Business.Models;
using Module.Business.ViewModels.PropertyVMs;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.Business.ViewModels;

/// <summary>
/// Lua 脚本配置视图模型，负责脚本列表的增删改查和本地 JSON 存储。
/// </summary>
public sealed class LuaScriptViewModel : ViewModelProperties
{
    #region 常量与样式字段

    /// <summary>
    /// Lua 脚本配置文件目录。
    /// </summary>
    private static readonly string LuaScriptConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");

    /// <summary>
    /// 成功状态提示颜色。
    /// </summary>
    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    /// <summary>
    /// 警告状态提示颜色。
    /// </summary>
    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    /// <summary>
    /// 中性状态提示颜色。
    /// </summary>
    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    #endregion

    #region 私有状态字段

    /// <summary>
    /// 脚本配置与存储文件名的映射，用于保存后清理旧文件。
    /// </summary>
    private readonly Dictionary<LuaScriptProfile, string> _profileStorageFileNames = new();

    /// <summary>
    /// 当前选中的 Lua 脚本配置。
    /// </summary>
    private LuaScriptProfile? _selectedProfile;

    /// <summary>
    /// 页面底部状态文本。
    /// </summary>
    private string _pageStatusText = "等待输入";

    /// <summary>
    /// 页面底部状态提示颜色。
    /// </summary>
    private Brush _pageStatusBrush = NeutralBrush;

    #endregion

    #region 构造与初始化

    /// <summary>
    /// 初始化 Lua 脚本配置视图模型，绑定命令并加载本地脚本。
    /// </summary>
    public LuaScriptViewModel()
    {
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DuplicateProfileCommand = new RelayCommand(_ => DuplicateProfile(), _ => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => SelectedProfile is not null);
        SaveProfilesCommand = new RelayCommand(_ => SaveProfiles());

        int loadedProfileCount = LoadProfilesFromDisk();
        if (loadedProfileCount == 0)
        {
            LuaScriptProfile profile = CreateSampleProfile();
            AddProfile(profile);
            SetPageStatus("未发现本地脚本，已创建默认示例。", NeutralBrush);
        }
        else
        {
            SetPageStatus($"已读取 {loadedProfileCount} 个 Lua 脚本。", SuccessBrush);
        }

        SelectedProfile = Profiles.FirstOrDefault();
    }

    #endregion

    #region 绑定集合与属性

    /// <summary>
    /// 当前可编辑的 Lua 脚本配置集合。
    /// </summary>
    public ObservableCollection<LuaScriptProfile> Profiles { get; } = new();

    /// <summary>
    /// 当前选中的 Lua 脚本配置。
    /// </summary>
    public LuaScriptProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (ReferenceEquals(_selectedProfile, value))
            {
                return;
            }

            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
            }

            _selectedProfile = value;
            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
            }

            OnPropertyChanged();
            RaiseCommandStatesChanged();
        }
    }

    /// <summary>
    /// 页面状态显示文本。
    /// </summary>
    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    /// <summary>
    /// 页面状态显示颜色。
    /// </summary>
    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    #endregion

    #region 命令属性

    /// <summary>
    /// 新建 Lua 脚本命令。
    /// </summary>
    public ICommand NewProfileCommand { get; }

    /// <summary>
    /// 复制当前 Lua 脚本命令。
    /// </summary>
    public ICommand DuplicateProfileCommand { get; }

    /// <summary>
    /// 删除当前 Lua 脚本命令。
    /// </summary>
    public ICommand DeleteProfileCommand { get; }

    /// <summary>
    /// 保存所有 Lua 脚本命令。
    /// </summary>
    public ICommand SaveProfilesCommand { get; }

    #endregion

    #region 命令处理

    /// <summary>
    /// 新建一个空白 Lua 脚本配置并选中。
    /// </summary>
    private void NewProfile()
    {
        LuaScriptProfile profile = CreateNewProfile(GenerateUniqueName("Lua 脚本"));
        AddProfile(profile);
        SelectedProfile = profile;
        SetPageStatus($"已新建脚本：{profile.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前选中的 Lua 脚本配置。
    /// </summary>
    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            SetPageStatus("请先选择需要复制的脚本。", WarningBrush);
            return;
        }

        LuaScriptProfile copy = SelectedProfile.Clone(GenerateCopyName(SelectedProfile.Name));
        AddProfile(copy);
        SelectedProfile = copy;
        SetPageStatus($"已复制脚本：{copy.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的 Lua 脚本配置并清理对应文件。
    /// </summary>
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            SetPageStatus("请先选择需要删除的脚本。", WarningBrush);
            return;
        }

        int selectedIndex = Profiles.IndexOf(SelectedProfile);
        LuaScriptProfile deletedProfile = SelectedProfile;
        deletedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
        Profiles.Remove(deletedProfile);
        DeleteStoredProfileFile(deletedProfile);

        if (Profiles.Count == 0)
        {
            LuaScriptProfile profile = CreateNewProfile(GenerateUniqueName("Lua 脚本"));
            AddProfile(profile);
        }

        SelectedProfile = Profiles[Math.Clamp(selectedIndex, 0, Profiles.Count - 1)];
        SetPageStatus($"已删除脚本：{deletedProfile.Name}。", NeutralBrush);
    }

    /// <summary>
    /// 保存当前页面中的所有 Lua 脚本配置。
    /// </summary>
    private void SaveProfiles()
    {
        try
        {
            int savedCount = SaveProfilesToDisk();
            SetPageStatus($"已保存 {savedCount} 个 Lua 脚本。", SuccessBrush);
        }
        catch (Exception ex)
        {
            SetPageStatus($"保存失败：{ex.Message}", WarningBrush);
        }
    }

    #endregion

    #region 配置变更跟踪

    /// <summary>
    /// 监听当前脚本配置变化并更新页面状态。
    /// </summary>
    /// <param name="sender">触发属性变更的脚本配置。</param>
    /// <param name="e">属性变更事件参数。</param>
    private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LuaScriptProfile profile &&
            e.PropertyName is nameof(LuaScriptProfile.Name) or nameof(LuaScriptProfile.ScriptText))
        {
            SetPageStatus($"正在编辑：{profile.Name}。", NeutralBrush);
        }
    }

    /// <summary>
    /// 添加脚本配置到集合。
    /// </summary>
    /// <param name="profile">待添加的 Lua 脚本配置。</param>
    private void AddProfile(LuaScriptProfile profile)
    {
        Profiles.Add(profile);
    }

    #endregion

    #region 配置创建

    /// <summary>
    /// 创建默认示例 Lua 脚本配置。
    /// </summary>
    /// <returns>默认示例脚本配置。</returns>
    private static LuaScriptProfile CreateSampleProfile()
    {
        return new LuaScriptProfile
        {
            Name = "Lua 示例脚本",
            ScriptText = string.Join(
                Environment.NewLine,
                "-- 点击执行脚本查看返回值",
                "local message = \"Hello Lua\"",
                "return message")
        };
    }

    /// <summary>
    /// 按指定名称创建空白 Lua 脚本配置。
    /// </summary>
    /// <param name="name">脚本名称。</param>
    /// <returns>新的 Lua 脚本配置。</returns>
    private static LuaScriptProfile CreateNewProfile(string name)
    {
        return new LuaScriptProfile
        {
            Name = name,
            ScriptText = "return \"Hello Lua\""
        };
    }

    #endregion

    #region 加载与反序列化

    /// <summary>
    /// 从本地脚本目录加载 Lua 脚本配置。
    /// </summary>
    /// <returns>成功加载的脚本数量。</returns>
    private int LoadProfilesFromDisk()
    {
        if (!Directory.Exists(LuaScriptConfigDirectory))
        {
            return 0;
        }

        int loadedCount = 0;
        foreach (string filePath in Directory.EnumerateFiles(LuaScriptConfigDirectory, "*.json").OrderBy(Path.GetFileName))
        {
            try
            {
                string storageText = File.ReadAllText(filePath, Encoding.UTF8);
                LuaScriptProfileDocument? document = DeserializeProfileDocument(storageText);
                if (document is null)
                {
                    continue;
                }

                LuaScriptProfile profile = document.ToProfile();
                profile.Name = BuildUniqueLoadedName(profile.Name);
                AddProfile(profile);
                _profileStorageFileNames[profile] = Path.GetFileName(filePath);
                loadedCount++;
            }
            catch (Exception ex)
            {
                SetPageStatus($"读取脚本失败：{Path.GetFileName(filePath)}，原因：{ex.Message}", WarningBrush);
            }
        }

        return loadedCount;
    }

    /// <summary>
    /// 将本地存储文本反序列化为脚本文档，兼容加密文本。
    /// </summary>
    /// <param name="storageText">本地文件中的脚本文本。</param>
    /// <returns>脚本文档；解析失败时返回 null。</returns>
    private static LuaScriptProfileDocument? DeserializeProfileDocument(string storageText)
    {
        try
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText);
        }
        catch
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText.DesDecrypt());
        }
    }

    #endregion

    #region 保存与清理

    /// <summary>
    /// 保存所有脚本配置到本地目录，并维护文件名映射。
    /// </summary>
    /// <returns>成功保存的脚本数量。</returns>
    private int SaveProfilesToDisk()
    {
        Directory.CreateDirectory(LuaScriptConfigDirectory);

        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<LuaScriptProfile, string> targetFileNames = new();
        foreach (LuaScriptProfile profile in Profiles)
        {
            ValidateProfileForSave(profile);
            targetFileNames[profile] = BuildUniqueStorageFileName(profile.Name, usedFileNames);
        }

        int savedCount = 0;
        foreach (LuaScriptProfile profile in Profiles)
        {
            string fileName = targetFileNames[profile];
            string filePath = Path.Combine(LuaScriptConfigDirectory, fileName);
            string storageText = JsonHelper.SerializeObject(LuaScriptProfileDocument.FromProfile(profile));
            File.WriteAllText(filePath, storageText, Encoding.UTF8);
            savedCount++;
        }

        foreach (LuaScriptProfile profile in Profiles)
        {
            string fileName = targetFileNames[profile];
            if (_profileStorageFileNames.TryGetValue(profile, out string? oldFileName) &&
                !string.Equals(oldFileName, fileName, StringComparison.OrdinalIgnoreCase) &&
                !usedFileNames.Contains(oldFileName))
            {
                TryDeleteStorageFile(oldFileName);
            }

            _profileStorageFileNames[profile] = fileName;
        }

        return savedCount;
    }

    /// <summary>
    /// 校验脚本配置是否满足保存要求。
    /// </summary>
    /// <param name="profile">待保存的脚本配置。</param>
    private static void ValidateProfileForSave(LuaScriptProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("脚本名称不能为空。");
        }
    }

    /// <summary>
    /// 删除脚本配置对应的本地存储文件。
    /// </summary>
    /// <param name="profile">被删除的脚本配置。</param>
    private void DeleteStoredProfileFile(LuaScriptProfile profile)
    {
        if (!_profileStorageFileNames.TryGetValue(profile, out string? fileName))
        {
            return;
        }

        TryDeleteStorageFile(fileName);
        _profileStorageFileNames.Remove(profile);
    }

    /// <summary>
    /// 尝试删除指定脚本文件，删除失败时静默忽略。
    /// </summary>
    /// <param name="fileName">脚本存储文件名。</param>
    private static void TryDeleteStorageFile(string fileName)
    {
        try
        {
            string filePath = Path.Combine(LuaScriptConfigDirectory, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    #endregion

    #region 文件名生成

    /// <summary>
    /// 根据脚本名称生成唯一的本地存储文件名。
    /// </summary>
    /// <param name="profileName">脚本名称。</param>
    /// <param name="usedFileNames">本次保存中已占用的文件名集合。</param>
    /// <returns>唯一的脚本存储文件名。</returns>
    private static string BuildUniqueStorageFileName(string profileName, HashSet<string> usedFileNames)
    {
        string safeName = BuildSafeFileName(profileName);
        string fileName = $"{safeName}.json";
        for (int index = 2; usedFileNames.Contains(fileName); index++)
        {
            fileName = $"{safeName}_{index}.json";
        }

        usedFileNames.Add(fileName);
        return fileName;
    }

    /// <summary>
    /// 将脚本名称转换为可用于文件名的安全文本。
    /// </summary>
    /// <param name="value">原始脚本名称。</param>
    /// <returns>安全文件名片段。</returns>
    private static string BuildSafeFileName(string value)
    {
        HashSet<char> invalidChars = new(Path.GetInvalidFileNameChars());
        StringBuilder builder = new(value.Trim().Length);
        foreach (char current in value.Trim())
        {
            builder.Append(invalidChars.Contains(current) || char.IsControl(current)
                ? '_'
                : char.IsWhiteSpace(current) ? '_' : current);
        }

        string safeName = builder.ToString().Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "LuaScript";
        }

        return safeName.Length <= 80 ? safeName : safeName[..80];
    }

    #endregion

    #region 名称生成

    /// <summary>
    /// 为从磁盘加载的脚本生成页面内唯一名称。
    /// </summary>
    /// <param name="loadedName">脚本文件中的原始名称。</param>
    /// <returns>页面内唯一的脚本名称。</returns>
    private string BuildUniqueLoadedName(string loadedName)
    {
        string baseName = string.IsNullOrWhiteSpace(loadedName) ? "Lua 脚本" : loadedName.Trim();
        if (!Profiles.Any(profile => string.Equals(profile.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        for (int index = 2; ; index++)
        {
            string name = $"{baseName} {index}";
            if (!Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    /// <summary>
    /// 按指定前缀生成新的唯一脚本名称。
    /// </summary>
    /// <param name="prefix">名称前缀。</param>
    /// <returns>唯一脚本名称。</returns>
    private string GenerateUniqueName(string prefix)
    {
        for (int index = 1; ; index++)
        {
            string name = $"{prefix} {index}";
            if (!Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    /// <summary>
    /// 为复制脚本生成唯一副本名称。
    /// </summary>
    /// <param name="baseName">被复制脚本的名称。</param>
    /// <returns>唯一副本名称。</returns>
    private string GenerateCopyName(string baseName)
    {
        string prefix = string.IsNullOrWhiteSpace(baseName) ? "Lua 脚本" : baseName.Trim();
        string firstName = $"{prefix} 副本";
        if (!Profiles.Any(profile => string.Equals(profile.Name, firstName, StringComparison.OrdinalIgnoreCase)))
        {
            return firstName;
        }

        for (int index = 2; ; index++)
        {
            string name = $"{firstName} {index}";
            if (!Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    #endregion

    #region 状态与命令刷新

    /// <summary>
    /// 更新页面状态文本和颜色。
    /// </summary>
    /// <param name="text">状态文本。</param>
    /// <param name="brush">状态颜色。</param>
    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    /// <summary>
    /// 刷新所有依赖选中脚本的命令状态。
    /// </summary>
    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(DuplicateProfileCommand);
        RaiseCommandState(DeleteProfileCommand);
    }

    /// <summary>
    /// 刷新单个 RelayCommand 的可执行状态。
    /// </summary>
    /// <param name="command">待刷新的命令。</param>
    private static void RaiseCommandState(ICommand command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion
}
