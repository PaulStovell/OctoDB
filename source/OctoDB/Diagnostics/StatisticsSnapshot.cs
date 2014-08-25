using System;
using System.Collections.Generic;

namespace OctoDB.Diagnostics
{
    public class StatisticsSnapshot
    {
        readonly IDictionary<string, long> counts;
        readonly IDictionary<string, TimeSpan> timings;

        public StatisticsSnapshot(IDictionary<string, long> counts, IDictionary<string, TimeSpan> timings)
        {
            this.counts = new Dictionary<string, long>(counts, StringComparer.OrdinalIgnoreCase);
            this.timings = new Dictionary<string, TimeSpan>(timings, StringComparer.OrdinalIgnoreCase);
        }

        public IDictionary<string, long> Counts
        {
            get { return counts; }
        }

        public IDictionary<string, TimeSpan> Timings
        {
            get { return timings; }
        }
    }
}
