using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.MES
{
    public class MesResult
    {
        public string Message { get; set; }
        public MesStatus State { get; set; }
        public string SendData { get; set; }
        public List<MesDataInfoItem> ReturnData { get; set; }
    }
    public enum MesStatus
    {
        UnUpLoad,
        ResultOK,
        StructNG,
        UpLoadNG,
        ResultNG,
    }
}
