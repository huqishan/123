using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.DB
{
    public interface IEntity
    {
        object[] GetKeys();
    }
    public interface IEntity<TKey>
    {
        TKey Id { get; }
    }
}
