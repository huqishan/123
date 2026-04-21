using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.Attributes
{
    public class ShowNameAttribute : Attribute
    {
        public ShowNameAttribute(int index, string name)
        {
            Name = name;
            Index = index;
        }
        public string Name;
        public int Index;
    }
}
