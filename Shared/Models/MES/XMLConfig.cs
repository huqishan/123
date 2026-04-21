using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.MES
{
    public class XMLConfig
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string XMLAction { get; set; }
    }
}
