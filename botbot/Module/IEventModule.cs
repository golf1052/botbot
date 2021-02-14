using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace botbot.Module
{
    public interface IEventModule
    {
        Task Handle(string type, JObject e);

        RecurringModule? RegisterRecurring();
    }
}
