using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using OctoDB.Tests.Fixtures;
using OctoDB.Tests.SampleModel;

namespace OctoDB.Tests
{
    public class Concurrency : StorageFixture
    {
        static List<Exception> exceptions;
        static int reads;
        static int writes;
        static readonly TimeSpan maxExecutionTime = TimeSpan.FromSeconds(5);

        [Test]
        [TestCase("One reader, one writer", 1, 1)]
        [TestCase("Ten readers, one writer", 10, 1)]
        [TestCase("Ten readers, two writers", 10, 2)]
        [TestCase("Ten readers, ten writers", 10, 10)]
        [TestCase("One reader, ten writers", 1, 10)]
        [TestCase("Two readers, ten writers", 2, 10)]
        public void Test(string description, int readers, int writers)
        {
            reads = 0;
            writes = 0;
            exceptions = new List<Exception>();
            var startSignal = new ManualResetEvent(false);
            var finishedSignals = new List<WaitHandle>();

            for (var i = 0; i < readers; i++)
            {
                finishedSignals.Add(CreateReader(startSignal));
            }

            for (var i = 0; i < writers; i++)
            {
                finishedSignals.Add(CreateCommitter(startSignal));
            }

            var watch = Stopwatch.StartNew();
            startSignal.Set();

            WaitHandle.WaitAll(finishedSignals.ToArray());
            watch.Stop();

            Console.WriteLine("Time taken: " + watch.ElapsedMilliseconds.ToString("n0") + "ms");
            Console.WriteLine("Reads: " + reads);
            Console.WriteLine("Writes: " + writes);

            if (exceptions.Count <= 0) 
                return;

            foreach (var ex in exceptions)
            {
                Console.WriteLine(ex);
            }

            Assert.Fail("One or more exceptions encountered");
        }

        WaitHandle CreateReader(WaitHandle signalWhenReadyToStart)
        {
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                using (var session = DocumentStore.OpenReadSession())
                {
                    var projects = session.Query<Project>();

                    foreach (var project in projects)
                    {
                        Assert.That(project.Name, Is.StringStarting("Project "));
                    }

                    if (projects.Count > 0)
                    {
                        Interlocked.Add(ref reads, projects.Count);
                    }
                }
            });
        }

        WaitHandle CreateCommitter(WaitHandle signalWhenReadyToStart)
        {
            var i = int.Parse(Math.Abs(Guid.NewGuid().GetHashCode()).ToString().Substring(5));
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {

                using (var session = DocumentStore.OpenWriteSession())
                {
                    for (var j = 0; j < 20; j++)
                    {
                        i++;
                        var project = new Project()
                        {
                            Id = i.ToString(),
                            Name = "Project " + i,
                            Description = "Special project " + i
                        };
                        session.Store(project);
                    }

                    session.Commit("Added 20 projects from: " + i);
                }

                Interlocked.Add(ref writes, 25);

                Thread.Sleep(200);
            });
        }

        static WaitHandle StartOnMySignal(WaitHandle signalWhenReadyToStart, Action callback)
        {
            var exitHandle = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                signalWhenReadyToStart.WaitOne();

                try
                {
                    var watch = Stopwatch.StartNew();

                    while (watch.Elapsed < maxExecutionTime)
                    {
                        callback();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                finally
                {
                    exitHandle.Set();
                }
            });

            return exitHandle;
        }
    }
}