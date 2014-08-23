using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace OctoDB.Diagnostics
{
    public interface IStatistics
    {
        IDisposable Measure(string operation);
        void Increment(string operation);
        void IncrementBy(string operation, long value);

        StatisticsSnapshot Snapshot();
        StatisticsSnapshot SnapshotAndReset();
    }

    public class StatisticsSnapshot : Dictionary<string, long>
    {
        public StatisticsSnapshot(IDictionary<string, long> values) : base(values, StringComparer.OrdinalIgnoreCase)
        {
            
        }
    }

    public static class StatisticsExtensions
    {
        public static void IncrementLoaded(this IStatistics statistics)
        {
            statistics.Increment("Documents loaded");
        }

        public static void IncrementReadSessionsOpened(this IStatistics statistics)
        {
            statistics.Increment("Read sessions opened");
        }

        public static void IncrementWriteSessionsOpened(this IStatistics statistics)
        {
            statistics.Increment("Write sessions opened");
        }

        public static void IncrementReadSessionsClosed(this IStatistics statistics)
        {
            statistics.Increment("Read sessions closed");
        }

        public static void IncrementWriteSessionsClosed(this IStatistics statistics)
        {
            statistics.Increment("Write sessions closed");
        }

        public static void IncrementStored(this IStatistics statistics)
        {
            statistics.Increment("Documents written");
        }

        public static void IncrementSnapshotReuse(this IStatistics statistics)
        {
            statistics.Increment("Read-only snapshot reuse");
        }

        public static void IncrementSnapshotRebuild(this IStatistics statistics)
        {
            statistics.Increment("Read-only snapshot rebuilds");
        }

        public static IDisposable MeasureSerialization(this IStatistics statistics)
        {
            return statistics.Measure("Serialization");
        }

        public static IDisposable MeasureDeserialization(this IStatistics statistics)
        {
            return statistics.Measure("Deserialization");
        }

        public static IDisposable MeasureAttachments(this IStatistics statistics)
        {
            return statistics.Measure("Attachments");
        }

        public static IDisposable MeasureGitStaging(this IStatistics statistics)
        {
            return statistics.Measure("Git Staging");
        }

        public static IDisposable MeasureGitCommit(this IStatistics statistics)
        {
            return statistics.Measure("Git Commit");
        }

        public static void Print(this IStatistics statistics)
        {
            var snapshot = statistics.SnapshotAndReset();
            foreach (var watch in snapshot.OrderBy(o => o.Key))
            {
                Console.WriteLine(watch.Key + ": " + watch.Value.ToString("n0"));
            }
        }
    }

    public class Statistics : IStatistics
    {
        readonly Dictionary<string, long> values = new Dictionary<string, long>();
        readonly object sync = new object();

        public IDisposable Measure(string name)
        {
            var watch = Stopwatch.StartNew();
            return new WatchStopper(() =>
            {
                watch.Stop();
                lock (sync)
                {
                    long original;
                    if (!values.TryGetValue(name, out original))
                    {
                        values[name] = watch.ElapsedMilliseconds;
                    }
                    else
                    {
                        values[name] = original + watch.ElapsedMilliseconds;
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
                if (!values.TryGetValue(operation, out original))
                {
                    values[operation] = value;
                }
                else
                {
                    values[operation] = original + value;
                }
            }
        }

        public StatisticsSnapshot Snapshot()
        {
            lock (sync)
            {
                var result = new StatisticsSnapshot(values);
                return result;
            }
        }

        public StatisticsSnapshot SnapshotAndReset()
        {
            lock (sync)
            {
                var snapshot = Snapshot();
                values.Clear();
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
