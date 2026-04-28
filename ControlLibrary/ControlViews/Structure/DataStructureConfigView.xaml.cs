using ControlLibrary.ControlViews.Structure.Models;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlLibrary.ControlViews.Structure
{
    public partial class DataStructureConfigView : UserControl, INotifyPropertyChanged
    {
        private static readonly string DataStructureConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "DataStructure");

        private static readonly Brush SuccessBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

        private static readonly Brush WarningBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

        private static readonly Brush NeutralBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

        private readonly Dictionary<DataStructureProfile, string> _profileStorageFileNames = new Dictionary<DataStructureProfile, string>();
        private DataStructureProfile? _selectedProfile;
        private string _previewText = "请选择或创建一个数据结构配置。";
        private string _previewStatusText = "等待输入";
        private Brush _previewStatusBrush = NeutralBrush;

        public DataStructureConfigView()
        {
            InitializeComponent();

            Formats = new ObservableCollection<DataStructureOption<DataStructureFormat>>
            {
                new DataStructureOption<DataStructureFormat>(DataStructureFormat.Json, "JSON", "生成 JSON 数据结构。"),
                new DataStructureOption<DataStructureFormat>(DataStructureFormat.Xml, "XML", "生成 XML 数据结构。")
            };

            JsonValueKinds = new ObservableCollection<DataStructureOption<JsonFieldValueKind>>
            {
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.String, "String", "字符串。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Integer, "Integer", "整数。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Number, "Number", "浮点数。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Boolean, "Boolean", "布尔值。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Object, "Object", "对象，默认值填写 JSON 对象。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Array, "Array", "数组，默认值填写 JSON 数组。"),
                new DataStructureOption<JsonFieldValueKind>(JsonFieldValueKind.Null, "Null", "空值。")
            };

            int loadedProfileCount = LoadProfilesFromDisk();
            if (loadedProfileCount == 0)
            {
                AddProfile(CreateSampleProfile());
                SetPreviewStatus("未发现本地配置，已创建默认示例。", NeutralBrush);
            }
            else
            {
                SetPreviewStatus($"已读取 {loadedProfileCount} 个数据结构配置。", SuccessBrush);
            }

            DataContext = this;
            SelectedProfile = Profiles.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DataStructureProfile> Profiles { get; } = new ObservableCollection<DataStructureProfile>();

        public ObservableCollection<DataStructureOption<DataStructureFormat>> Formats { get; }

        public ObservableCollection<DataStructureOption<JsonFieldValueKind>> JsonValueKinds { get; }

        public DataStructureProfile? SelectedProfile
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
                    UnsubscribeFieldChanges(_selectedProfile);
                }

                _selectedProfile = value;
                if (_selectedProfile is not null)
                {
                    _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
                    SubscribeFieldChanges(_selectedProfile);
                }

                OnPropertyChanged();
                UpdatePreview();
            }
        }

        public string ConfigDirectoryDisplay => $"配置目录：{DataStructureConfigDirectory}";

        public string PreviewText
        {
            get => _previewText;
            private set => SetField(ref _previewText, value);
        }

        public string PreviewStatusText
        {
            get => _previewStatusText;
            private set => SetField(ref _previewStatusText, value);
        }

        public Brush PreviewStatusBrush
        {
            get => _previewStatusBrush;
            private set => SetField(ref _previewStatusBrush, value);
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            DataStructureProfile profile = new DataStructureProfile
            {
                Name = GenerateUniqueName("数据结构"),
                RootName = "payload"
            };
            profile.Fields.Clear();
            profile.AddField(new DataStructureFieldConfig { Name = "字段 1", MesCode = "MES_CODE", ClientCode = "clientCode" });
            profile.SelectedField = profile.Fields.FirstOrDefault();
            AddProfile(profile);
            SelectedProfile = profile;
        }

        private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile is null)
            {
                return;
            }

            DataStructureProfile copy = SelectedProfile.Clone(GenerateCopyName(SelectedProfile.Name));
            AddProfile(copy);
            SelectedProfile = copy;
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile is null)
            {
                return;
            }

            DataStructureProfile profile = SelectedProfile;
            DeleteStoredProfileFile(profile);
            UnsubscribeFieldChanges(profile);
            profile.PropertyChanged -= SelectedProfile_PropertyChanged;
            Profiles.Remove(profile);
            SelectedProfile = Profiles.FirstOrDefault();

            if (Profiles.Count == 0)
            {
                PreviewText = "请新增一个数据结构配置。";
                SetPreviewStatus("已删除最后一个配置。", WarningBrush);
            }
        }

        private void SaveProfiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int savedCount = SaveProfilesToDisk();
                SetPreviewStatus($"已保存 {savedCount} 个数据结构配置。", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetPreviewStatus($"保存失败：{ex.Message}", WarningBrush);
            }
        }

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile is null)
            {
                return;
            }

            DataStructureFieldConfig field = new DataStructureFieldConfig
            {
                Name = GenerateUniqueFieldName(SelectedProfile, "字段"),
                MesCode = "MES_CODE",
                ClientCode = GenerateUniqueClientCode(SelectedProfile, "clientCode")
            };
            SelectedProfile.AddField(field);
            field.PropertyChanged += Field_PropertyChanged;
            SelectedProfile.SelectedField = field;
            UpdatePreview();
        }

        private void DuplicateField_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile?.SelectedField is null)
            {
                return;
            }

            DataStructureFieldConfig copy = SelectedProfile.SelectedField.Clone(GenerateUniqueFieldName(SelectedProfile, SelectedProfile.SelectedField.Name));
            copy.ClientCode = GenerateUniqueClientCode(SelectedProfile, copy.ClientCode);
            SelectedProfile.AddField(copy);
            copy.PropertyChanged += Field_PropertyChanged;
            SelectedProfile.SelectedField = copy;
            UpdatePreview();
        }

        private void DeleteField_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile?.SelectedField is null)
            {
                return;
            }

            DataStructureFieldConfig field = SelectedProfile.SelectedField;
            if (SelectedProfile.Fields.Count == 1)
            {
                SetPreviewStatus("至少需要保留一个字段。", WarningBrush);
                return;
            }

            field.PropertyChanged -= Field_PropertyChanged;
            SelectedProfile.RemoveField(field);
            UpdatePreview();
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (SelectedProfile is null)
            {
                PreviewText = "请选择或创建一个数据结构配置。";
                SetPreviewStatus("等待输入", NeutralBrush);
                return;
            }

            if (DataStructurePreviewBuilder.TryBuildPreview(SelectedProfile, out string previewText, out string message))
            {
                PreviewText = previewText;
                SetPreviewStatus(message, SuccessBrush);
            }
            else
            {
                PreviewText = string.Empty;
                SetPreviewStatus(message, WarningBrush);
            }
        }

        private void AddProfile(DataStructureProfile profile)
        {
            Profiles.Add(profile);
        }

        private static DataStructureProfile CreateSampleProfile()
        {
            DataStructureProfile profile = new DataStructureProfile
            {
                Name = "MES 默认结构",
                Format = DataStructureFormat.Json,
                RootName = "payload"
            };

            profile.Fields.Clear();
            profile.AddField(new DataStructureFieldConfig
            {
                Name = "工单号",
                MesCode = "MO_NO",
                ClientCode = "workOrder",
                DataType = "string",
                DefaultValue = "MO20260425001",
                JsonValueKind = JsonFieldValueKind.String
            });
            profile.AddField(new DataStructureFieldConfig
            {
                Name = "数量",
                MesCode = "QTY",
                ClientCode = "quantity",
                DataType = "int",
                DefaultValue = "1",
                JsonValueKind = JsonFieldValueKind.Integer
            });
            profile.SelectedField = profile.Fields.FirstOrDefault();
            return profile;
        }

        private int LoadProfilesFromDisk()
        {
            if (!Directory.Exists(DataStructureConfigDirectory))
            {
                return 0;
            }

            int loadedCount = 0;
            foreach (string filePath in Directory.EnumerateFiles(DataStructureConfigDirectory, "*.json").OrderBy(Path.GetFileName))
            {
                try
                {
                    string storageText = File.ReadAllText(filePath, Encoding.UTF8);
                    DataStructureProfileDocument? document = JsonHelper.DeserializeObject<DataStructureProfileDocument>(storageText.DesDecrypt());
                    if (document is null)
                    {
                        continue;
                    }

                    DataStructureProfile profile = document.ToProfile();
                    AddProfile(profile);
                    _profileStorageFileNames[profile] = Path.GetFileName(filePath);
                    loadedCount++;
                }
                catch (Exception ex)
                {
                    SetPreviewStatus($"读取配置失败：{Path.GetFileName(filePath)}，原因：{ex.Message}", WarningBrush);
                }
            }

            return loadedCount;
        }

        private int SaveProfilesToDisk()
        {
            Directory.CreateDirectory(DataStructureConfigDirectory);

            HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int savedCount = 0;
            foreach (DataStructureProfile profile in Profiles)
            {
                ValidateProfileForSave(profile);

                string fileName = BuildUniqueStorageFileName(profile.Name, usedFileNames);
                string filePath = Path.Combine(DataStructureConfigDirectory, fileName);
                string storageText = JsonHelper.SerializeObject(DataStructureProfileDocument.FromProfile(profile)).Encrypt();
                File.WriteAllText(filePath, storageText, Encoding.UTF8);

                if (_profileStorageFileNames.TryGetValue(profile, out string? oldFileName) &&
                    !string.Equals(oldFileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteStorageFile(oldFileName);
                }

                _profileStorageFileNames[profile] = fileName;
                savedCount++;
            }

            return savedCount;
        }

        private static void ValidateProfileForSave(DataStructureProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new InvalidOperationException("配置名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(profile.RootName))
            {
                throw new InvalidOperationException($"配置 {profile.Name} 的根节点/根属性不能为空。");
            }

            if (profile.Fields.Count == 0)
            {
                throw new InvalidOperationException($"配置 {profile.Name} 至少需要一个字段。");
            }

            if (!DataStructurePreviewBuilder.TryBuildPreview(profile, out _, out string message))
            {
                throw new InvalidOperationException($"配置 {profile.Name} 校验失败：{message}");
            }
        }

        private void DeleteStoredProfileFile(DataStructureProfile profile)
        {
            if (!_profileStorageFileNames.TryGetValue(profile, out string? fileName))
            {
                return;
            }

            TryDeleteStorageFile(fileName);
            _profileStorageFileNames.Remove(profile);
        }

        private static void TryDeleteStorageFile(string fileName)
        {
            try
            {
                string filePath = Path.Combine(DataStructureConfigDirectory, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
            }
        }

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

        private static string BuildSafeFileName(string value)
        {
            HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            StringBuilder builder = new StringBuilder(value.Trim().Length);
            foreach (char current in value.Trim())
            {
                builder.Append(invalidChars.Contains(current) || char.IsControl(current)
                    ? '_'
                    : char.IsWhiteSpace(current) ? '_' : current);
            }

            string safeName = builder.ToString().Trim(' ', '.');
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "DataStructure";
            }

            return safeName.Length <= 80 ? safeName : safeName[..80];
        }

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

        private string GenerateCopyName(string baseName)
        {
            string prefix = string.IsNullOrWhiteSpace(baseName) ? "数据结构" : baseName.Trim();
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

        private static string GenerateUniqueFieldName(DataStructureProfile profile, string prefix)
        {
            string baseName = string.IsNullOrWhiteSpace(prefix) ? "字段" : prefix.Trim();
            if (!profile.Fields.Any(field => string.Equals(field.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            {
                return baseName;
            }

            for (int index = 2; ; index++)
            {
                string name = $"{baseName} {index}";
                if (!profile.Fields.Any(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return name;
                }
            }
        }

        private static string GenerateUniqueClientCode(DataStructureProfile profile, string prefix)
        {
            string baseName = string.IsNullOrWhiteSpace(prefix) ? "clientCode" : prefix.Trim();
            if (!profile.Fields.Any(field => string.Equals(field.ClientCode, baseName, StringComparison.OrdinalIgnoreCase)))
            {
                return baseName;
            }

            for (int index = 2; ; index++)
            {
                string name = $"{baseName}{index}";
                if (!profile.Fields.Any(field => string.Equals(field.ClientCode, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return name;
                }
            }
        }

        private void SubscribeFieldChanges(DataStructureProfile profile)
        {
            foreach (DataStructureFieldConfig field in profile.Fields)
            {
                field.PropertyChanged += Field_PropertyChanged;
            }
        }

        private void UnsubscribeFieldChanges(DataStructureProfile profile)
        {
            foreach (DataStructureFieldConfig field in profile.Fields)
            {
                field.PropertyChanged -= Field_PropertyChanged;
            }
        }

        private void SetPreviewStatus(string text, Brush brush)
        {
            PreviewStatusText = text;
            PreviewStatusBrush = brush;
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
