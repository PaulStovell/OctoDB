using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OctoDB.Storage;
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
        public void MultipleCommittersDoNotOverwriteEachOther()
        {
            reads = 0;
            writes = 0;
            exceptions = new List<Exception>();

            var startSignal = new ManualResetEvent(false);

            var exits = new[]
            {
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateReader(startSignal),
                CreateCommitter(startSignal),
                CreateCommitter(startSignal),
                CreateCommitter(startSignal),
                CreateCommitter(startSignal),
                CreateReaderWriter(startSignal),
                CreateReaderWriter(startSignal),
                CreateReaderWriter(startSignal),
                CreateReaderWriter(startSignal)
            };

            var watch = Stopwatch.StartNew();
            startSignal.Set();

            WaitHandle.WaitAll(exits);
            watch.Stop();

            Console.WriteLine("Time taken: " + watch.ElapsedMilliseconds.ToString("n0") + "ms");
            Console.WriteLine("Reads: " + reads);
            Console.WriteLine("Writes: " + writes);

            if (exceptions.Count <= 0) return;
            foreach (var ex in exceptions)
            {
                Console.WriteLine(ex);
            }

            Assert.Fail("One or more exceptions encountered: ");
        }

        [Test]
        public void MultipleReadersSingleWriter()
        {
            var startSignal = new ManualResetEvent(false);
            var celebrity = new Celebrity();
            var newspaper = new Newspaper();

            var exits = new[]
            {
                CreateCelebrity(celebrity, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal)
            };
            
            startSignal.Set();

            WaitHandle.WaitAll(exits);

            if (newspaper.DrunkenCelebritiesSpotted > 0)
            {
                Assert.Fail("Spotted {0} drunken celebrities", newspaper.DrunkenCelebritiesSpotted);
            }
        }

        [Test]
        public void MultipleReadersMultipleWriters()
        {
            var startSignal = new ManualResetEvent(false);
            var celebrity = new Celebrity();
            var newspaper = new Newspaper();

            var exits = new[]
            {
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal)
            };

            startSignal.Set();

            WaitHandle.WaitAll(exits);

            if (newspaper.DrunkenCelebritiesSpotted > 0)
            {
                Assert.Fail("Spotted {0} drunken celebrities", newspaper.DrunkenCelebritiesSpotted);
            }
        }

        [Test]
        public void MultipleReadersMultipleWritersAndReaderWriter()
        {
            var startSignal = new ManualResetEvent(false);
            var celebrity = new Celebrity();
            var newspaper = new Newspaper();

            var exits = new[]
            {
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateCelebrity(celebrity, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateReporter(celebrity, newspaper, startSignal),
                CreateCelebrityReporter(celebrity, newspaper, startSignal),
                CreateCelebrityReporter(celebrity, newspaper, startSignal)
            };

            startSignal.Set();

            WaitHandle.WaitAll(exits);

            if (newspaper.DrunkenCelebritiesSpotted > 0)
            {
                Assert.Fail("Spotted {0} drunken celebrities", newspaper.DrunkenCelebritiesSpotted);
            }
        }

        WaitHandle CreateReader(WaitHandle signalWhenReadyToStart)
        {
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                var projects = Storage.LoadAll<Project>("projects");

                foreach (var project in projects)
                {
                    Assert.That(project.Name, Is.StringStarting("Project "));
                }

                if (projects.Count > 0)
                {
                    Interlocked.Add(ref reads, projects.Count);
                }
            });
        }

        WaitHandle CreateCommitter(WaitHandle signalWhenReadyToStart)
        {
            var i = int.Parse(Math.Abs(Guid.NewGuid().GetHashCode()).ToString().Substring(5));
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                i++;

                var project = new Project()
                {
                    Name = "Project " + i,
                    Description = "Special project " + i,
                    ScriptModule = "Write-Host 'Hello " + i + "!'\r\n"
                };

                using (var batch = Storage.Batch())
                {
                    batch.Put("projects/project-" + i, project);
                    batch.Commit("Added project " + i);
                }

                Interlocked.Increment(ref writes);

                Thread.Sleep(100);
            });
        }

        WaitHandle CreateReaderWriter(WaitHandle signalWhenReadyToStart)
        {
            var i = int.Parse(Math.Abs(Guid.NewGuid().GetHashCode()).ToString().Substring(5));
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                i++;

                var project = new Project()
                {
                    Name = "Project " + i,
                    Description = "Special project " + i,
                    ScriptModule = "Write-Host 'Hello " + i + "!'\r\n"
                };

                using (var batch = Storage.Batch())
                {
                    batch.Put("projects/project-" + i, project);
                    batch.Commit("Added project " + i);
                }

                var loaded = Storage.Load<Project>("projects/project-" + i);
                Assert.That(loaded, Is.Not.Null);

                Interlocked.Increment(ref writes);
                Interlocked.Increment(ref reads);

                Thread.Sleep(100);
            });
        }

        static WaitHandle CreateCelebrity(Celebrity celebrity, ManualResetEvent signalWhenReadyToStart)
        {
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                celebrity.DoWork();
            });
        }


        static WaitHandle CreateCelebrityReporter(Celebrity celebrity, Newspaper newspaper, WaitHandle signalWhenReadyToStart)
        {
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                var state = celebrity.GetCurrentState();
                if (state != "sober")
                {
                    newspaper.DrunkenCelebritiesSpotted++;
                }

                celebrity.DoWork();

                state = celebrity.GetCurrentState();
                if (state != "sober")
                {
                    newspaper.DrunkenCelebritiesSpotted++;
                }
            });
        }

        static WaitHandle CreateReporter(Celebrity celebrity, Newspaper newspaper, WaitHandle signalWhenReadyToStart)
        {
            return StartOnMySignal(signalWhenReadyToStart, delegate
            {
                var state = celebrity.GetCurrentState();
                if (state != "sober")
                {
                    newspaper.DrunkenCelebritiesSpotted++;
                }
            });
        }

        class Newspaper
        {
            public int DrunkenCelebritiesSpotted { get; set; }
        }

        class Celebrity
        {
            string state = "sober";
            ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

            public string GetCurrentState()
            {
                sync.EnterReadLock();
                try
                {
                    Thread.Sleep(100);
                    return state;
                }
                finally
                {
                    sync.ExitReadLock();
                }
            }

            public void DoWork()
            {
                sync.EnterWriteLock();
                try
                {
                    state = "drunken";
                    Console.Write("D");
                    Thread.Sleep(300);
                    state = "sober";
                    Console.Write("S");
                    Thread.Sleep(300);
                    Console.Write(" ");
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }
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