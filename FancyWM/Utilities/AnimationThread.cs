using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal interface IAnimationJob
    {
        bool IsCancelled { get; }
        TimeSpan Duration { get; }
        Task Task { get; }
        ValueTask Update(double progress);
        void Cancel();

        void OnCompleted();
        void OnCancelled();
    }

    internal static class AnimationJob
    {
        private class DelegateAnimationJob(Func<IAnimationJob, double, ValueTask> animate, TimeSpan duration) : IAnimationJob
        {
            public bool IsCancelled => m_isCancelled;

            public TimeSpan Duration => m_duration;

            public Task Task => m_tcs.Task;

            private readonly Func<IAnimationJob, double, ValueTask> m_animate = animate;
            private readonly TimeSpan m_duration = duration;
            private bool m_isCancelled;
            private readonly TaskCompletionSource<object?> m_tcs = new();

            public void Cancel()
            {
                m_isCancelled = true;
            }

            public ValueTask Update(double progress)
            {
                return m_animate(this, progress);
            }

            public void OnCompleted()
            {
                m_tcs.TrySetResult(null);
            }

            public void OnCancelled()
            {
                m_tcs.TrySetCanceled();
            }
        }

        public static IAnimationJob Create(Func<IAnimationJob, double, ValueTask> animate, TimeSpan duration)
        {
            return new DelegateAnimationJob(animate, duration);
        }
    }

    internal interface IAnimationThread : IDisposable
    {
        void Start(IAnimationJob job);
    }

    internal partial class AnimationThread : IAnimationThread
    {
        internal partial class Compositor
        {
            [LibraryImport("dcomp")]
            internal static partial uint DCompositionWaitForCompositorClock(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[]? handles, uint timeoutInMs);

            [LibraryImport("dcomp")]
            internal static partial int DCompositionBoostCompositorClock(int enable);

            internal static bool s_isDCompositionCompositorAPIAvailable = true;

            internal static int BoostClock(bool enable)
            {
                int hr = 0;
                if (s_isDCompositionCompositorAPIAvailable)
                {
                    try
                    {
                        hr = DCompositionBoostCompositorClock(enable ? 1 : 0);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        s_isDCompositionCompositorAPIAvailable = false;
                    }
                }
                return hr;
            }

            internal static uint Wait(uint timeoutInMs)
            {
                uint hr = 0;
                if (s_isDCompositionCompositorAPIAvailable)
                {
                    try
                    {
                        hr = DCompositionWaitForCompositorClock(0, null, timeoutInMs);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        s_isDCompositionCompositorAPIAvailable = false;
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
                return hr;
            }
        }


        private class WorkItem(IAnimationJob job, TimeSpan startTime, TimeSpan endTime)
        {
            public TimeSpan StartTime { get; set; } = startTime;
            public TimeSpan EndTime { get; set; } = endTime;
            public IAnimationJob Job { get; set; } = job ?? throw new ArgumentNullException(nameof(job));
        }

        private readonly Thread m_thread;
        private readonly TimeSpan m_targetFrameTime;
        private readonly BlockingCollection<IAnimationJob> m_queue = [];
        private readonly Stopwatch m_sw = new();

        public AnimationThread(int targetFrameRate)
        {
            m_targetFrameTime = TimeSpan.FromSeconds(1.0 / targetFrameRate);
            m_thread = new Thread(ThreadStart)
            {
                Name = "AnimationThread"
            };
            m_thread.Start();
        }

        public void Start(IAnimationJob job)
        {
            m_queue.Add(job);
        }

        private void ThreadStart(object? obj)
        {
            m_sw.Restart();

            var jobs = new List<WorkItem>();
            var completedJobs = new List<WorkItem>();

            while (true)
            {
                try
                {
                    while (m_queue.TryTake(out IAnimationJob? job))
                    {
                        jobs.Add(new WorkItem(job, m_sw.Elapsed, m_sw.Elapsed + job.Duration));
                    }
                }
                catch (InvalidOperationException) when (m_queue.IsCompleted)
                {
                    break;
                }

                if (jobs.Count == 0)
                {
                    IAnimationJob? job = null;
                    try
                    {
                        while (job == null)
                        {
                            job = m_queue.Take();
                        }
                    }
                    catch (InvalidOperationException) when (m_queue.IsCompleted)
                    {
                        break;
                    }
                    jobs.Add(new WorkItem(job, m_sw.Elapsed, m_sw.Elapsed + job.Duration));
                }

                _ = Compositor.BoostClock(true);
                try
                {
                    uint waitResult = Compositor.Wait((uint)m_targetFrameTime.TotalMilliseconds);
                    Debug.Assert(waitResult >= 0);

                    completedJobs.Clear();
                    Task.WaitAll(jobs.Select(async (job) =>
                    {
                        var progress = Math.Min(1.0, (m_sw.Elapsed - job.StartTime).TotalMilliseconds / job.Job.Duration.TotalMilliseconds);
                        await job.Job.Update(progress);
                        if (progress >= 1.0)
                        {
                            lock (completedJobs)
                            {
                                completedJobs.Add(job);
                            }
                            job.Job.OnCompleted();
                        }
                        else if (job.Job.IsCancelled)
                        {
                            lock (completedJobs)
                            {
                                completedJobs.Add(job);
                            }
                            job.Job.OnCancelled();
                        }
                    }).ToArray());

                    foreach (var completedJob in completedJobs)
                    {
                        jobs.Remove(completedJob);
                    }
                }
                finally
                {
                    if (m_queue.Count == 0)
                    {
                        _ = Compositor.BoostClock(false);
                    }
                }
            }
        }

        public void Dispose()
        {
            m_queue.CompleteAdding();
            m_thread.Join(1000);
        }
    }
}
