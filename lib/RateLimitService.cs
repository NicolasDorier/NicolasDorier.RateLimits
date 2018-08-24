using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    public class RateLimitService
    {
        public RateLimitService(IDelay delay = null)
        {
            _Delay = delay ?? TaskDelay.Instance;
        }
        class BucketHandle
        {
            public LeakyBucket Bucket;
            public Task Drain;
            int _RefCount = 0;

            internal bool Ref()
            {
                if(_RefCount == -1)
                    return false;
                Interlocked.Increment(ref _RefCount);
                return true;
            }
            internal void Release(IDelay delay)
            {
                if(Interlocked.Decrement(ref _RefCount) == 0)
                {
                    delay.Wait(Bucket.LimitRequestZone.RequestRate.TimePerRequest).ContinueWith((_) =>
                    {
                        if(Interlocked.CompareExchange(ref _RefCount, -1, 0) == 0)
                        {
                            Bucket.Close();
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
            }
        }

        ConcurrentDictionary<string, LimitRequestZone> _Zones = new ConcurrentDictionary<string, LimitRequestZone>();
        ConcurrentDictionary<(string, object), BucketHandle> _BucketHandles = new ConcurrentDictionary<(string, object), BucketHandle>();

        IDelay _Delay;

        public void SetZone(LimitRequestZone requestZone)
        {
            if(requestZone == null)
                throw new ArgumentNullException(nameof(requestZone));
            _Zones.AddOrUpdate(requestZone.Name, requestZone, (a, b) => requestZone);
        }

        public void SetZone(string requestZone)
        {
            if(requestZone == null)
                throw new ArgumentNullException(nameof(requestZone));
            if(LimitRequestZone.TryParse(requestZone, out var zone))
                SetZone(zone);
            else
                throw new FormatException("Invalid request zone");
        }


        /// <summary>
        /// Throttle the flow given for 'context' at the 'scope' level
        /// </summary>
        /// <param name="zoneName">The name of the zone to use</param>
        /// <param name="scope">The scope upon which to apply throttling</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task completing when after throttling</returns>
        public async Task<bool> Throttle(string zoneName, object scope = null, CancellationToken cancellationToken = default)
        {
            if(zoneName == null)
                throw new ArgumentNullException(nameof(zoneName));
            zoneName = zoneName.Trim().ToLowerInvariant();
            BucketHandle handle = null;
            var bucketKey = (zoneName, scope);

            while(true)
            {
                if(_BucketHandles.TryGetValue(bucketKey, out handle))
                {
                    if(handle.Ref())
                    {
                        break;
                    }
                }
                else
                {
                    if(!_Zones.TryGetValue(zoneName, out var zone))
                        throw new KeyNotFoundException($"{zoneName} is not found");
                    handle = new BucketHandle();
                    handle.Bucket = new LeakyBucket(zone, _Delay);
                    handle.Ref();
                    if(_BucketHandles.TryAdd(bucketKey, handle))
                    {
                        handle.Drain = DrainBucket(handle).ContinueWith((t) =>
                        {
                            _BucketHandles.TryRemove(bucketKey, out var unused);
                        }, TaskContinuationOptions.ExecuteSynchronously);
                        break;
                    }
                }
            }

            return await handle.Bucket.Throttle();
        }

        private async Task DrainBucket(BucketHandle handle)
        {
            while(await handle.Bucket.DrainNext())
            {
                handle.Release(_Delay);
            }
        }

        public int BucketsCount
        {
            get
            {
                return _BucketHandles.Count;
            }
        }
    }
}
