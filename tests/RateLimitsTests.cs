using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NicolasDorier.RateLimits;

namespace NicolasDorier.RateLimits.Tests
{
    public class RateLimitsTests
    {
        public class MockDelay : IDelay
        {
            class WaitObj
            {
                public DateTimeOffset Expiration;
                public TaskCompletionSource<bool> CTS;
            }

            List<WaitObj> waits = new List<WaitObj>();
            DateTimeOffset _Now = new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero);
            public async Task Wait(TimeSpan delay)
            {
                WaitObj w = new WaitObj();
                w.Expiration = _Now + delay;
                w.CTS = new TaskCompletionSource<bool>();
                lock(waits)
                {
                    waits.Add(w);
                }
                await w.CTS.Task;
            }

            public void Advance(TimeSpan time)
            {
                _Now += time;
                lock(waits)
                {
                    foreach(var wait in waits.ToArray())
                    {
                        if(_Now >= wait.Expiration)
                        {
                            wait.CTS.TrySetResult(true);
                            waits.Remove(wait);
                        }
                    }
                }
            }

            public void AdvanceMilliseconds(long milli)
            {
                Advance(TimeSpan.FromMilliseconds(milli));
            }

            public override string ToString()
            {
                return _Now.Millisecond.ToString(CultureInfo.InvariantCulture);
            }
        }

        [Fact]
        public void CanParseRateLimit()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s", out var limitRequestZone));
            Assert.Equal("mylimit", limitRequestZone.Name);
            Assert.Equal("10r/s", limitRequestZone.RequestRate.ToString());
            Assert.Equal(TimeSpan.FromMilliseconds(100), limitRequestZone.RequestRate.TimePerRequest);
            Assert.Null(limitRequestZone.Burst);
            Assert.False(limitRequestZone.NoDelay);
            Assert.Equal("zone=mylimit rate=10r/s", limitRequestZone.ToString());

            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s burst=20 nodelay", out limitRequestZone));
            Assert.Equal("mylimit", limitRequestZone.Name);
            Assert.Equal("10r/s", limitRequestZone.RequestRate.ToString());
            Assert.Equal(TimeSpan.FromMilliseconds(100), limitRequestZone.RequestRate.TimePerRequest);
            Assert.NotNull(limitRequestZone.Burst);
            Assert.Equal(20, limitRequestZone.Burst.Value);
            Assert.True(limitRequestZone.NoDelay);
            Assert.Equal("zone=mylimit rate=10r/s burst=20 nodelay", limitRequestZone.ToString());
        }

        [Fact]
        [RateLimitsFilter(Limits.ActionArgument, Scope = RateLimitsScope.RemoteAddress)]
        public async void CanUseRateLimit()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s", out var limitRequestZone));
            var delay = new MockDelay();
            var queue = new LeakyBucket(limitRequestZone, delay);
            Assert.Equal(1, queue.RemainingSlots);
            Assert.Equal(0, queue.UsedSlots);

            // 1st request should be processed immediately
            var wait = queue.Throttle();
            var processing = queue.DrainNext();
            Assert.True(wait.Wait(10));
            Assert.Equal(0, queue.RemainingSlots);
            Assert.Equal(1, queue.UsedSlots);

            // 2nd request should be rejected
            Assert.False(await queue.Throttle());

            // Can't process, as we need to throttle
            Assert.False(processing.Wait(10));
            delay.AdvanceMilliseconds(99);
            Assert.False(processing.Wait(10));

            // Though after 100ms, it should works
            delay.AdvanceMilliseconds(1);
            Assert.True(processing.Wait(10));
            Assert.Equal(1, queue.RemainingSlots);
            Assert.Equal(0, queue.UsedSlots);
            processing = queue.DrainNext();
            await queue.Throttle();
        }

        [Fact]
        public void CanUseRateLimitWithBurst()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s burst=20", out var limitRequestZone));
            var delay = new MockDelay();
            var queue = new LeakyBucket(limitRequestZone, delay);
            Assert.Equal(20, queue.RemainingSlots);
            Assert.Equal(0, queue.UsedSlots);

            // 1st request should be processed immediately
            var wait = queue.Throttle();
            var processing = queue.DrainNext();
            Assert.True(wait.Wait(10));
            Assert.Equal(19, queue.RemainingSlots);
            Assert.Equal(1, queue.UsedSlots);

            // 2nd request should be queued
            wait = queue.Throttle();
            Assert.False(wait.Wait(10));
            Assert.Equal(18, queue.RemainingSlots);
            Assert.Equal(2, queue.UsedSlots);

            // Can't process, as we need to throttle
            Assert.False(processing.Wait(10));
            delay.AdvanceMilliseconds(99);
            Assert.False(processing.Wait(10));
            Assert.False(wait.Wait(10));

            // Though after 100ms, one slot should be freed, and the queued task executed
            delay.AdvanceMilliseconds(1);
            Assert.True(processing.Wait(10));
            processing = queue.DrainNext();
            Assert.True(wait.Wait(10));
            Assert.Equal(19, queue.RemainingSlots);
            Assert.Equal(1, queue.UsedSlots);

            // But still processing blocked...
            Assert.False(processing.Wait(10));
            // Until 100 ms passed
            delay.AdvanceMilliseconds(100);
            Assert.True(processing.Wait(10));
            Assert.Equal(20, queue.RemainingSlots);
        }

        [Fact]
        public void CanUseRateLimitWithBurstNoDelay()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s burst=20 nodelay", out var limitRequestZone));
            var delay = new MockDelay();
            var queue = new LeakyBucket(limitRequestZone, delay);
            Assert.Equal(20, queue.RemainingSlots);
            Assert.Equal(0, queue.UsedSlots);
            // 1st request should be processed immediately
            var wait = queue.Throttle();
            var processing = queue.DrainNext();
            Assert.True(wait.Wait(10));
            Assert.Equal(19, queue.RemainingSlots);
            Assert.Equal(1, queue.UsedSlots);
            Assert.True(processing.Wait(10)); // Thanks to nodelay processing do not have to wait

            // 2nd request should be processed immediately, but the slot kept
            wait = queue.Throttle();
            processing = queue.DrainNext();
            Assert.True(wait.Wait(10));
            Assert.True(processing.Wait(10)); // Thanks to nodelay processing do not have to wait
            Assert.Equal(18, queue.RemainingSlots); // But the slots should not be free
            Assert.Equal(2, queue.UsedSlots);

            // Can process, thanks to nodelay
            delay.AdvanceMilliseconds(99);
            Assert.Equal(18, queue.RemainingSlots);
            Assert.Equal(2, queue.UsedSlots);

            // Though after 100ms, one slot should be freed, and the queued task executed
            delay.AdvanceMilliseconds(1);
            Thread.Sleep(1); // Sleep necessary as the WaitAfter is running concurrently
            Assert.Equal(19, queue.RemainingSlots);
            Assert.Equal(1, queue.UsedSlots);
            Thread.Sleep(1); // Sleep necessary as the WaitAfter is running concurrently

            // +100 ms passed, second slot if released
            delay.AdvanceMilliseconds(99);
            Assert.Equal(19, queue.RemainingSlots);
            delay.AdvanceMilliseconds(1);

            Thread.Sleep(1); // Sleep necessary as the WaitAfter is running concurrently
            Assert.Equal(20, queue.RemainingSlots);
        }

        [Fact]
        public async void CanUseRateLimitWithBurstNoDelay2()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s burst=20 nodelay", out var limitRequestZone));
            var delay = new MockDelay();
            var queue = new LeakyBucket(limitRequestZone, delay);

            List<Task> waits = new List<Task>();
            for(int i = 0; i < 20; i++)
            {
                waits.Add(queue.Throttle());
            }
            Assert.False(await queue.Throttle());
            for(int i = 0; i < 20; i++)
            {
                delay.AdvanceMilliseconds(100);
                var processing = queue.DrainNext();
                Task.WaitAny(waits.ToArray());
                waits.RemoveAll(t => t.IsCompletedSuccessfully);
                Assert.Equal(20 - i - 1, waits.Count);
                Assert.True(processing.IsCompletedSuccessfully);
            }
        }


        [Fact]
        public async void CanThrottle()
        {
            Assert.True(LimitRequestZone.TryParse("zone=mylimit rate=10r/s burst=20", out var limitRequestZone));
            var delay = new MockDelay();
            var service = new RateLimitService(delay);
            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await service.Throttle("mylimit"));
            Assert.Equal(0, service.BucketsCount);

            service.SetZone(limitRequestZone);

            var throttling = service.Throttle("mylimit"); // Will execute immediately, slot reserved until +100
            Assert.True(throttling.Wait(10));
            Assert.Equal(1, service.BucketsCount);

            var throttling2 = service.Throttle("mylimit"); // Will execute at +100, slot reserved until +200
            Assert.False(throttling2.Wait(10));
            Assert.Equal(1, service.BucketsCount);
            delay.AdvanceMilliseconds(100);
            Assert.True(throttling2.Wait(10));
            Assert.True(throttling2.IsCompletedSuccessfully);
            Assert.Equal(1, service.BucketsCount); // Still bucket 1, because the slot used by throttling2 is still here

            delay.AdvanceMilliseconds(100);
            Assert.Equal(1, service.BucketsCount);
            // The bucket get "collected" after some time
            Thread.Sleep(limitRequestZone.RequestRate.TimePerRequest + TimeSpan.FromSeconds(0.3));
            Assert.Equal(0, service.BucketsCount);
        }
    }
}
