using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.MES
{
    public class MesDataInfoTree
    {
        public MesDataInfoTree()
        {
            MesDataInfoItems = new List<MesDataInfoItem>();
        }
        public MesDataInfoTree(string productID,bool testResult, string apiName, List<MesDataInfoItem> mesData)
        {
            this.ProductID = productID;
            this.Result = testResult;
            this.ApiName = apiName;
            this.MesDataInfoItems = mesData;
        }
        public List<MesDataInfoItem> MesDataInfoItems { get; set; }
        public string ApiName { get; set; }
        public string ProductID { get; set; }
        public bool Result { get; set; }
    }
    public class MesDataInfoItem
    {
        /// <summary>
        /// 上传代码
        /// </summary>
        public string Code { get; private set; }
        /// <summary>
        /// 上传的数据
        /// </summary>
        public object Value { get; private set; }
        public MesDataInfoItem(string code, object value)
        {
            this.Code = code;
            this.Value = value;
        }
    }
}
