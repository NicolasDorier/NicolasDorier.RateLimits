using System.Threading;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    public interface IRateLimitService
    {
        int BucketsCount { get; }
        void SetZone(LimitRequestZone requestZone);
        void SetZone(string requestZone);
        Task<bool> Throttle(string zoneName, object scope = null, CancellationToken cancellationToken = default);
    }
}
