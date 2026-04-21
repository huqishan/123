using Shared.Infrastructure.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Models.EventModels
{
    public class MyMessage : PubSubEvent<string>
    {
    }
}
