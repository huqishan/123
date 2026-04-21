using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.Attributes
{
    public class LuaAttribute : Attribute
    {
        public LuaAttribute(string luaMethodName)
        {
            LuaMethodName = luaMethodName;
        }
        public string LuaMethodName;
    }
}
