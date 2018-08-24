using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    public interface IDelay
    {
        Task Wait(TimeSpan delay);
    }

    public class TaskDelay : IDelay
    {
        TaskDelay()
        {

        }
        private static readonly TaskDelay _Instance = new TaskDelay();
        public static TaskDelay Instance
        {
            get
            {
                return _Instance;
            }
        }
        public Task Wait(TimeSpan delay)
        {
            return Task.Delay(delay);
        }
    }
}
