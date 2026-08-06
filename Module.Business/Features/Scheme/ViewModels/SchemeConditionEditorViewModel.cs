using ControlLibrary;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Module.Business.Features.Scheme.ViewModels.PresentationModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace Module.Business.Features.Scheme.ViewModels;

/// <summary>
/// 方案界面显示条件编辑器，集中维护方案内所有返回值的显示状态和显示名称。
/// </summary>
public sealed class SchemeConditionEditorViewModel : ViewModelProperties
{
    #region 私有字段

    private SchemeProfile? _editingScheme;
    private bool _isOpen;
    private bool _showAllReturns;

    #endregion

    #region 构造与集合

    public SchemeConditionEditorViewModel()
    {
        ConditionsView = CollectionViewSource.GetDefaultView(Conditions);
        ConditionsView.Filter = FilterCondition;
        ConditionsView.SortDescriptions.Add(
            new SortDescription(nameof(SchemeConditionEditorItem.IsShowView), ListSortDirection.Descending));
        ConditionsView.SortDescriptions.Add(
            new SortDescription(nameof(SchemeConditionEditorItem.WorkStepName), ListSortDirection.Ascending));

        SaveCommand = new RelayCommand(_ => Save());
        CloseCommand = new RelayCommand(_ => Close());
    }

    public ObservableCollection<SchemeConditionEditorItem> Conditions { get; } = new();

    public ICollectionView ConditionsView { get; }

    #endregion

    #region 展示状态与命令

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>
    /// 默认仅显示已选中的返回值；开启后显示全部返回值，便于勾选新的界面显示项。
    /// </summary>
    public bool ShowAllReturns
    {
        get => _showAllReturns;
        set
        {
            if (!SetField(ref _showAllReturns, value))
            {
                return;
            }

            ConditionsView.Refresh();
            OnPropertyChanged(nameof(VisibleConditionCount));
        }
    }

    public string Title => _editingScheme is null
        ? "判断条件"
        : $"{_editingScheme.SchemeName} · 判断条件";

    public int TotalConditionCount => Conditions.Count;

    public int SelectedConditionCount => Conditions.Count(item => item.IsShowView);

    public int VisibleConditionCount => ConditionsView.Cast<object>().Count();

    public ICommand SaveCommand { get; }

    public ICommand CloseCommand { get; }

    #endregion

    #region 打开、保存与关闭

    public void Open(SchemeProfile scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        foreach (SchemeConditionEditorItem item in Conditions)
        {
            item.PropertyChanged -= Condition_PropertyChanged;
        }

        Conditions.Clear();
        _editingScheme = scheme;
        foreach (SchemeWorkStepItem workStep in scheme.Steps.OrderBy(item => item.Num))
        {
            foreach (WorkStepOperation operation in workStep.Operations.OrderBy(item => item.Num))
            {
                foreach (ReturnValue returnValue in operation.ReturnValues.OrderBy(item => item.Num))
                {
                    SchemeConditionEditorItem item = new(
                        returnValue,
                        workStep.StepName,
                        string.IsNullOrWhiteSpace(operation.Summary) ? operation.PCommandName : operation.Summary,
                        operation.ReturnValue);
                    item.PropertyChanged += Condition_PropertyChanged;
                    Conditions.Add(item);
                }
            }
        }

        ShowAllReturns = false;
        ConditionsView.Refresh();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TotalConditionCount));
        OnPropertyChanged(nameof(SelectedConditionCount));
        OnPropertyChanged(nameof(VisibleConditionCount));
        IsOpen = true;
    }

    private void Save()
    {
        if (_editingScheme is null)
        {
            return;
        }

        bool hasChanges = false;
        foreach (SchemeConditionEditorItem item in Conditions)
        {
            string normalizedViewDataName = item.ViewDataName.Trim();
            if (item.Source.IsShowView == item.IsShowView &&
                string.Equals(item.Source.ViewDataName, normalizedViewDataName, StringComparison.Ordinal))
            {
                continue;
            }

            item.Source.IsShowView = item.IsShowView;
            item.Source.ViewDataName = normalizedViewDataName;
            hasChanges = true;
        }

        if (hasChanges)
        {
            _editingScheme.MarkModified();
        }

        Close();
    }

    public void Close()
    {
        IsOpen = false;
        _editingScheme = null;
        OnPropertyChanged(nameof(Title));
    }

    #endregion

    #region 筛选与行通知

    private bool FilterCondition(object item)
    {
        return item is SchemeConditionEditorItem condition &&
               (ShowAllReturns || condition.IsShowView);
    }

    private void Condition_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SchemeConditionEditorItem.IsShowView))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedConditionCount));
        // 刷新后已勾选项始终排在前面；默认筛选状态下，取消勾选的行会立即移出当前列表。
        ConditionsView.Refresh();
        OnPropertyChanged(nameof(VisibleConditionCount));
    }

    #endregion
}
