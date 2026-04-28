using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ControlLibrary.ControlViews.Structure.Models
{
    public enum DataStructureFormat
    {
        Json,
        Xml
    }

    public enum JsonFieldValueKind
    {
        String,
        Integer,
        Number,
        Boolean,
        Object,
        Array,
        Null
    }

    public sealed class DataStructureOption<T>
    {
        public DataStructureOption(T value, string displayName, string description)
        {
            Value = value;
            DisplayName = displayName;
            Description = description;
        }

        public T Value { get; }

        public string DisplayName { get; }

        public string Description { get; }
    }

    internal sealed class DataStructureFieldDocument
    {
        public string? Name { get; set; }

        public string? MesCode { get; set; }

        public string? ClientCode { get; set; }

        public string? DataType { get; set; }

        public string? DefaultValue { get; set; }

        public JsonFieldValueKind JsonValueKind { get; set; } = JsonFieldValueKind.String;

        public static DataStructureFieldDocument FromField(DataStructureFieldConfig field)
        {
            return new DataStructureFieldDocument
            {
                Name = field.Name,
                MesCode = field.MesCode,
                ClientCode = field.ClientCode,
                DataType = field.DataType,
                DefaultValue = field.DefaultValue,
                JsonValueKind = field.JsonValueKind
            };
        }

        public DataStructureFieldConfig ToField()
        {
            return new DataStructureFieldConfig
            {
                Name = string.IsNullOrWhiteSpace(Name) ? "字段 1" : Name.Trim(),
                MesCode = MesCode ?? string.Empty,
                ClientCode = ClientCode ?? string.Empty,
                DataType = DataType ?? string.Empty,
                DefaultValue = DefaultValue ?? string.Empty,
                JsonValueKind = JsonValueKind
            };
        }
    }

    internal sealed class DataStructureProfileDocument
    {
        public int Version { get; set; } = 1;

        public string? Name { get; set; }

        public DataStructureFormat Format { get; set; } = DataStructureFormat.Json;

        public string? RootName { get; set; }

        public string? XmlNamespace { get; set; }

        public List<DataStructureFieldDocument>? Fields { get; set; }

        public static DataStructureProfileDocument FromProfile(DataStructureProfile profile)
        {
            return new DataStructureProfileDocument
            {
                Name = profile.Name,
                Format = profile.Format,
                RootName = profile.RootName,
                XmlNamespace = profile.XmlNamespace,
                Fields = profile.Fields.Select(DataStructureFieldDocument.FromField).ToList()
            };
        }

        public DataStructureProfile ToProfile()
        {
            DataStructureProfile profile = new DataStructureProfile
            {
                Name = string.IsNullOrWhiteSpace(Name) ? "数据结构 1" : Name.Trim(),
                Format = Format,
                RootName = string.IsNullOrWhiteSpace(RootName) ? "payload" : RootName.Trim(),
                XmlNamespace = XmlNamespace ?? string.Empty
            };

            profile.Fields.Clear();
            if (Fields is { Count: > 0 })
            {
                foreach (DataStructureFieldDocument field in Fields)
                {
                    profile.AddField(field.ToField());
                }
            }
            else
            {
                profile.AddField(new DataStructureFieldConfig());
            }

            profile.SelectedField = profile.Fields.FirstOrDefault();
            return profile;
        }
    }

    public sealed class DataStructureFieldConfig : INotifyPropertyChanged
    {
        private string _name = "字段 1";
        private string _mesCode = "MES_CODE";
        private string _clientCode = "clientCode";
        private string _dataType = "string";
        private string _defaultValue = string.Empty;
        private JsonFieldValueKind _jsonValueKind = JsonFieldValueKind.String;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value, true);
        }

        public string MesCode
        {
            get => _mesCode;
            set => SetField(ref _mesCode, value, true);
        }

        public string ClientCode
        {
            get => _clientCode;
            set => SetField(ref _clientCode, value, true);
        }

        public string DataType
        {
            get => _dataType;
            set => SetField(ref _dataType, value, true);
        }

        public string DefaultValue
        {
            get => _defaultValue;
            set => SetField(ref _defaultValue, value, false);
        }

        public JsonFieldValueKind JsonValueKind
        {
            get => _jsonValueKind;
            set => SetField(ref _jsonValueKind, value, false);
        }

        public string Summary =>
            $"{MesCode} -> {ResolveOutputName()} / {DataTypeDisplayName}";

        public string DataTypeDisplayName =>
            JsonValueKind switch
            {
                JsonFieldValueKind.String => "JSON String",
                JsonFieldValueKind.Integer => "JSON Integer",
                JsonFieldValueKind.Number => "JSON Number",
                JsonFieldValueKind.Boolean => "JSON Boolean",
                JsonFieldValueKind.Object => "JSON Object",
                JsonFieldValueKind.Array => "JSON Array",
                JsonFieldValueKind.Null => "JSON Null",
                _ => JsonValueKind.ToString()
            };

        public string ResolveOutputName()
        {
            if (!string.IsNullOrWhiteSpace(ClientCode))
            {
                return ClientCode.Trim();
            }

            return string.IsNullOrWhiteSpace(Name) ? "field" : Name.Trim();
        }

        public DataStructureFieldConfig Clone(string name)
        {
            return new DataStructureFieldConfig
            {
                Name = name,
                MesCode = MesCode,
                ClientCode = ClientCode,
                DataType = DataType,
                DefaultValue = DefaultValue,
                JsonValueKind = JsonValueKind
            };
        }

        private bool SetField<T>(ref T field, T value, bool trimString, [CallerMemberName] string? propertyName = null)
        {
            object? normalizedValue = value;
            if (trimString && value is string stringValue)
            {
                normalizedValue = stringValue.Trim();
            }

            if (Equals(field, normalizedValue))
            {
                return false;
            }

            field = (T)normalizedValue!;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(DataTypeDisplayName));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class DataStructureProfile : INotifyPropertyChanged
    {
        private string _name = "数据结构 1";
        private DataStructureFormat _format = DataStructureFormat.Json;
        private string _rootName = "payload";
        private string _xmlNamespace = string.Empty;
        private DataStructureFieldConfig? _selectedField;

        public DataStructureProfile()
        {
            AddField(new DataStructureFieldConfig());
            SelectedField = Fields.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DataStructureFieldConfig> Fields { get; } = new ObservableCollection<DataStructureFieldConfig>();

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value, true);
        }

        public DataStructureFormat Format
        {
            get => _format;
            set
            {
                if (SetField(ref _format, value, false))
                {
                    OnPropertyChanged(nameof(IsJsonFormatSelected));
                    OnPropertyChanged(nameof(IsXmlFormatSelected));
                }
            }
        }

        public string RootName
        {
            get => _rootName;
            set => SetField(ref _rootName, value, true);
        }

        public string XmlNamespace
        {
            get => _xmlNamespace;
            set => SetField(ref _xmlNamespace, value, true);
        }

        public DataStructureFieldConfig? SelectedField
        {
            get => _selectedField;
            set
            {
                if (ReferenceEquals(_selectedField, value))
                {
                    return;
                }

                _selectedField = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedField));
            }
        }

        public bool HasSelectedField => SelectedField is not null;

        public bool IsJsonFormatSelected => Format == DataStructureFormat.Json;

        public bool IsXmlFormatSelected => Format == DataStructureFormat.Xml;

        public string Summary => $"{FormatDisplayName} / 根节点 {RootName} / 字段 {Fields.Count} 个";

        public string FormatDisplayName => Format == DataStructureFormat.Json ? "JSON" : "XML";

        public void AddField(DataStructureFieldConfig field)
        {
            Fields.Add(field);
            field.PropertyChanged += Field_PropertyChanged;
            OnPropertyChanged(nameof(Summary));
        }

        public void RemoveField(DataStructureFieldConfig field)
        {
            if (!Fields.Remove(field))
            {
                return;
            }

            field.PropertyChanged -= Field_PropertyChanged;
            if (ReferenceEquals(SelectedField, field))
            {
                SelectedField = Fields.FirstOrDefault();
            }

            OnPropertyChanged(nameof(Summary));
        }

        public DataStructureProfile Clone(string name)
        {
            DataStructureProfile profile = new DataStructureProfile
            {
                Name = name,
                Format = Format,
                RootName = RootName,
                XmlNamespace = XmlNamespace
            };

            profile.Fields.Clear();
            foreach (DataStructureFieldConfig field in Fields)
            {
                profile.AddField(field.Clone(field.Name));
            }

            profile.SelectedField = profile.Fields.FirstOrDefault();
            return profile;
        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Summary));
        }

        private bool SetField<T>(ref T field, T value, bool trimString, [CallerMemberName] string? propertyName = null)
        {
            object? normalizedValue = value;
            if (trimString && value is string stringValue)
            {
                normalizedValue = stringValue.Trim();
            }

            if (Equals(field, normalizedValue))
            {
                return false;
            }

            field = (T)normalizedValue!;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(Summary));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
