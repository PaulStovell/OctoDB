using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OctoDB.Diagnostics
{
    public class Statistics : IStatistics
    {
        readonly Dictionary<string, long> counts = new Dictionary<string, long>();
        readonly Dictionary<string, TimeSpan> timings = new Dictionary<string, TimeSpan>();
        readonly object sync = new object();

        public IDisposable Measure(string name)
        {
            var watch = Stopwatch.StartNew();
            return new WatchStopper(() =>
            {
                watch.Stop();
                lock (sync)
                {
                    TimeSpan original;
                    if (!timings.TryGetValue(name, out original))
                    {
                        timings[name] = watch.Elapsed;
                    }
                    else
                    {
                        timings[name] = original + watch.Elapsed;
                    }
                }
            });
        }

        public void Increment(string operation)
        {
            IncrementBy(operation, 1);
        }

        public void IncrementBy(string operation, long value)
        {
            lock (sync)
            {
                long original;
                if (!counts.TryGetValue(operation, out original))
                {
                    counts[operation] = value;
                }
                else
                {
                    counts[operation] = original + value;
                }
            }
        }

        public StatisticsSnapshot Snapshot()
        {
            lock (sync)
            {
                var result = new StatisticsSnapshot(counts, timings);
                return result;
            }
        }

        public StatisticsSnapshot SnapshotAndReset()
        {
            lock (sync)
            {
                var snapshot = Snapshot();
                counts.Clear();
                return snapshot;
            }
        }

        class WatchStopper : IDisposable
        {
            private readonly Action callback;

            public WatchStopper(Action callback)
            {
                this.callback = callback;
            }

            public void Dispose()
            {
                callback();
            }
        }
    }
}