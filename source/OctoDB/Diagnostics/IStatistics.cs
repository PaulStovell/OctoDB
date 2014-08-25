using System;

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
}