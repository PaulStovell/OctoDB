using System;
using System.Linq;

namespace OctoDB.Diagnostics
{
    public static class StatisticsExtensions
    {
        public static void IncrementLoaded(this IStatistics statistics)
        {
            statistics.Increment("Documents loaded");
        }

        public static void IncrementReadSessionsOpened(this IStatistics statistics)
        {
            statistics.Increment("Read sessions created");
        }

        public static void IncrementHistoricalReadSessionsOpened(this IStatistics statistics)
        {
            statistics.Increment("Historical read sessions created");
        }

        public static void IncrementWriteSessionsOpened(this IStatistics statistics)
        {
            statistics.Increment("Write sessions created");
        }

        public static void IncrementReadSessionsClosed(this IStatistics statistics)
        {
            statistics.Increment("Read sessions disposed");
        }

        public static void IncrementHistoricalReadSessionsClosed(this IStatistics statistics)
        {
            statistics.Increment("Historical read sessions disposed");
        }

        public static void IncrementWriteSessionsClosed(this IStatistics statistics)
        {
            statistics.Increment("Write sessions disposed");
        }

        public static void IncrementStored(this IStatistics statistics)
        {
            statistics.Increment("Documents written");
        }

        public static void IncrementDeleted(this IStatistics statistics)
        {
            statistics.Increment("Documents deleted");
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

        public static IDisposable MeasureGitStaging(this IStatistics statistics)
        {
            return statistics.Measure("Git Staging");
        }

        public static IDisposable MeasureGitCommit(this IStatistics statistics)
        {
            return statistics.Measure("Git Commit");
        }

        public static IDisposable MeasureGitReset(this IStatistics statistics)
        {
            return statistics.Measure("Git Reset");
        }

        public static void Print(this IStatistics statistics)
        {
            var snapshot = statistics.SnapshotAndReset();

            if (snapshot.Counts.Count + snapshot.Timings.Count == 0)
                return;

            var keyLength = snapshot.Counts.Keys.Concat(snapshot.Timings.Keys).Max(m => m.Length) + 10;

            foreach (var stat in snapshot.Counts.OrderBy(o => o.Key))
            {
                Console.WriteLine((stat.Key + ": ").PadRight(keyLength, ' ') + stat.Value.ToString("n0"));
            }
            foreach (var stat in snapshot.Timings.OrderBy(o => o.Key))
            {
                Console.WriteLine((stat.Key + ": ").PadRight(keyLength, ' ') + stat.Value.ToString("g"));
            }
        }
    }
}