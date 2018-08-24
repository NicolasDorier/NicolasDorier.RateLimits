using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    public class RequestRate
    {
        public static bool TryParse(string str, out RequestRate requestRate)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            requestRate = null;
            str = str.Trim();
            var parts = str.Split("r/", StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length != 2)
            {
                return false;
            }
            if (!int.TryParse(parts[0].Trim(), out var requestCount))
                return false;
            if (requestCount <= 0)
                return false;
            RequestRatePeriod period = RequestRatePeriod.Day;
            switch (parts[1].Trim().ToLowerInvariant())
            {
                case "s":
                case "sec":
                case "second":
                    period = RequestRatePeriod.Second;
                    break;
                case "m":
                case "min":
                case "minute":
                    period = RequestRatePeriod.Minute;
                    break;
                case "h":
                case "hour":
                    period = RequestRatePeriod.Hour;
                    break;
                case "d":
                case "day":
                    period = RequestRatePeriod.Day;
                    break;
                default:
                    return false;
            }
            requestRate = new RequestRate(requestCount, period);
            return true;
        }

        public RequestRate(int requestCount, RequestRatePeriod period)
        {
            if (requestCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestCount));
            Period = period;
            RequestCount = requestCount;
        }

        public int RequestCount { get; }
        public RequestRatePeriod Period { get; }

        public TimeSpan PeriodTime =>
            Period == RequestRatePeriod.Second ? TimeSpan.FromSeconds(1.0) :
            Period == RequestRatePeriod.Minute ? TimeSpan.FromMinutes(1.0) :
            Period == RequestRatePeriod.Hour ? TimeSpan.FromHours(1.0) :
            Period == RequestRatePeriod.Day ? TimeSpan.FromDays(1.0) : throw new NotSupportedException();

        public TimeSpan TimePerRequest => TimeSpan.FromTicks(PeriodTime.Ticks / RequestCount);

        public override string ToString()
        {
            var period =    Period == RequestRatePeriod.Second ? "s" :
                            Period == RequestRatePeriod.Minute ? "m" :
                            Period == RequestRatePeriod.Hour ? "h" :
                            Period == RequestRatePeriod.Day ? "d" : throw new NotSupportedException();
            return $"{RequestCount}r/{period}";
        }
    }
    public enum RequestRatePeriod
    {
        Second,
        Minute,
        Hour,
        Day
    }
}
