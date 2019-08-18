using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class RecurringModule
    {
        public TimeSpan Interval { get; private set; }

        public Func<Task> Func { get; private set; }

        public RecurringModule(TimeSpan interval, Func<Task> func)
        {
            Interval = interval;
            Func = func;
        }
    }
}
