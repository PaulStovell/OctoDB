using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OctoDB.Diagnostics
{
    public static class PerformanceCounters
    {
        static readonly Dictionary<string, Stopwatch> stopwatches = new Dictionary<string, Stopwatch>();

        public static IDisposable Serialization()
        {
            return Start("Serialization");
        }

        public static IDisposable Deserialization()
        {
            return Start("Deserialization");
        }

        public static IDisposable Attachments()
        {
            return Start("Attachments");
        }

        public static IDisposable GitStaging()
        {
            return Start("Git Staging");
        }

        public static IDisposable GitReset()
        {
            return Start("Git Reset");
        }

        public static IDisposable GitCommit()
        {
            return Start("Git Commit");
        }

        public static IDisposable Other()
        {
            return Start("Other");
        }

        static IDisposable Start(string name)
        {
            if (!stopwatches.ContainsKey(name))
            {
                stopwatches.Add(name, new Stopwatch());
            }

            var watch = stopwatches[name];
            watch.Start();
            return new WatchStopper(watch);
        }

        public static void Print()
        {
            foreach (var watch in stopwatches)
            {
                watch.Value.Stop();
                Console.WriteLine(watch.Key + ": " + watch.Value.ElapsedMilliseconds + "ms");
            }
        }

        class WatchStopper : IDisposable
        {
            private readonly Stopwatch watch;

            public WatchStopper(Stopwatch watch)
            {
                this.watch = watch;
            }

            public void Dispose()
            {
                watch.Stop();
            }
        }
    }
}
