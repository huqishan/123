using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ControlLibrary
{
    public abstract class ViewModelProperties : INotifyPropertyChanged
    {
        #region 最后修改时间
        private DateTime _lastModifiedAt = DateTime.Now;
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModifiedAt
        {
            get => _lastModifiedAt;
            set
            {
                DateTime normalizedValue = value == default ? DateTime.Now : value;
                if (SetField(ref _lastModifiedAt, normalizedValue, false))
                {
                    OnPropertyChanged(nameof(LastModifiedText));
                }
            }
        }
        #endregion
        #region 通知事件

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region 展示属性

        [JsonIgnore]
        public virtual string LastModifiedText => $"最后修改：{LastModifiedAt:yyyy-MM-dd HH:mm:ss}";

        #endregion
        #region 属性通知方法

        protected bool SetField<T>(ref T field, T value, bool isLastModified = true, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            if (isLastModified)
            {
                LastModifiedAt = DateTime.Now;
            }
            return true;
        }
        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
