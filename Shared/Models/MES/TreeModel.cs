using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.MES
{
    public class TreeModel
    {
        public bool IsRoot { get; set; }
        public string ClientCode { get; set; }
        public string MESCode { get; set; }
        public string DataType { get; set; }
        public string DefectValue { get; set; }
        public bool IsWhile { get; set; }
        public bool IsNull { get; set; }
        public int WhileCount { get; set; }
        public string KeepDecimalLength { get; set; }
        public string XMLNameSpace { get; set; }
        public ObservableCollection<TreeModel> Childs { get; set; } = new ObservableCollection<TreeModel>();
        public string JudgeValue { get; set; }
        public string OKText { get; set; }
        public string NGText { get; set; }
        public TreeModel Clone()
        {
            return clone(this);
            TreeModel clone(TreeModel model)
            {
                TreeModel treeModel = new TreeModel();
                foreach (var item in model.GetType().GetProperties())
                {
                    if (item.Name.Equals("Childs"))
                    {
                        foreach (var c in model.Childs)
                        {
                            treeModel.Childs.Add(clone(c));
                        }
                    }
                    else
                    {
                        treeModel.GetType().GetProperty(item.Name).SetValue(treeModel, item.GetValue(model));
                    }
                }
                return treeModel;
            }

        }
    }
}
