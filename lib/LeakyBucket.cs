using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NicolasDorier.RateLimits
{
    public class LeakyBucket
    {
        class TaskToProcess
        {
            public TaskToProcess(System.Threading.Tasks.TaskCompletionSource<bool> cts, int slotNumber)
            {
                Cts = cts;
                SlotNumber = slotNumber;
            }
            public TaskCompletionSource<bool> Cts { get; }
            public int SlotNumber { get; }
        }
        private readonly IDelay _delay;
        private readonly Channel<TaskToProcess> _Queue;
        private readonly int _Slots;
        private readonly TimeSpan _secondsPerRequest;
        private int _UsedSlots;
        private Task _CurrentWait;

        public LeakyBucket(LimitRequestZone limitRequestZone, IDelay delay = null)
        {
            if(limitRequestZone == null)
                throw new ArgumentNullException(nameof(limitRequestZone));
            LimitRequestZone = limitRequestZone;
            _delay = delay ?? TaskDelay.Instance;
            _secondsPerRequest = LimitRequestZone.RequestRate.TimePerRequest;
            var burst = limitRequestZone.Burst.HasValue ? limitRequestZone.Burst.Value : 1;
            _Queue = Channel.CreateBounded<TaskToProcess>(burst);
            _Slots = burst;
        }

        object l = new object();
        /// <summary>
        /// A call which will throttle requests to the service as defined by LimitRequestZone
        /// </summary>
        /// <returns>A task completing when after throttling</returns>
        public async Task<bool> Throttle()
        {
            TaskCompletionSource<bool> cts = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int slotNumber;
            lock (l)
            {
                if (_UsedSlots >= _Slots)
                    return false;
                slotNumber = _UsedSlots;
                _UsedSlots++;
            }
            if(!_Queue.Writer.TryWrite(new TaskToProcess(cts, slotNumber)))
            {
                if(IsClosed)
                {
                    // If the bucket is closed, execute immediately
                    return true;
                }
                throw new NotSupportedException("Bug in RateLimitQueue");
            }
            await cts.Task;
            return true;

        }

        /// <summary>
        /// Drain the next drop from the bucket
        /// </summary>
        /// <returns>A task completing only when the bucket is ready to leak the next drop. True if other drops are expected, false if the bucket is empty and close.</returns>
        public async Task<bool> DrainNext()
        {
            if(await _Queue.Reader.WaitToReadAsync() &&
                        _Queue.Reader.TryRead(out var req))
            {
                req.Cts.TrySetResult(true);

                var wait = !LimitRequestZone.NoDelay;
                if (wait && LimitRequestZone.Delay is int delay)
                    wait = req.SlotNumber + 1 >= delay;
                if (wait)
                {
                    await Wait();
                }
                else
                {
                    if(_CurrentWait == null || _CurrentWait.IsCompleted)
                    {
                        _CurrentWait = Wait();
                    }
                    else
                    {
                        _CurrentWait = WaitAfter(_CurrentWait);
                    }
                }
            }
            return !IsClosed;
        }

        public bool IsClosed => _Queue.Reader.Completion.IsCompleted;

        public void Close()
        {
            _Queue.Writer.TryComplete();
        }

        private async Task WaitAfter(Task currentWait)
        {
            await currentWait;
            currentWait = null;
            await Wait();
        }

        public int UsedSlots
        {
            get
            {
                lock (l)
                {
                    return _UsedSlots;
                }
            }
        }

        public int RemainingSlots
        {
            get
            {
                lock (l)
                {
                    return _Slots - _UsedSlots;
                }
            }
        }

        public LimitRequestZone LimitRequestZone
        {
            get;
        }

        private async Task Wait()
        {
            await _delay.Wait(_secondsPerRequest);
            lock (l)
            {
                _UsedSlots--;
            }
        }
    }
}
