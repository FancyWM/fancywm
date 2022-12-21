using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        void Update(double progress);
        void Cancel();

        void OnCompleted();
        void OnCancelled();
    }

    internal static class AnimationJob
    {
        private class DelegateAnimationJob : IAnimationJob
        {
            public bool IsCancelled => m_isCancelled;

            public TimeSpan Duration => m_duration;

            public Task Task => m_tcs.Task;

            private readonly Action<IAnimationJob, double> m_animate;
            private readonly TimeSpan m_duration;
            private bool m_isCancelled;
            private readonly TaskCompletionSource<object?> m_tcs = new();

            public DelegateAnimationJob(Action<IAnimationJob, double> animate, TimeSpan duration)
            {
                m_animate = animate;
                m_duration = duration;
            }

            public void Cancel()
            {
                m_isCancelled = true;
            }

            public void Update(double progress)
            {
                m_animate(this, progress);
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

        public static IAnimationJob Create(Action<IAnimationJob, double> animate, TimeSpan duration)
        {
            return new DelegateAnimationJob(animate, duration);
        }
    }

    internal interface IAnimationThread : IDisposable
    {
        void Start(IAnimationJob job);
    }

    internal class AnimationThread : IAnimationThread
    {
        private class WorkItem
        {
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public IAnimationJob Job { get; set; }

            public WorkItem(IAnimationJob job, TimeSpan startTime, TimeSpan endTime)
            {
                Job = job ?? throw new ArgumentNullException(nameof(job));
                StartTime = startTime;
                EndTime = endTime;
            }
        }

        private readonly Thread m_thread;
        private readonly TimeSpan m_targetFrameTime;
        private readonly BlockingCollection<IAnimationJob> m_queue = new();
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
            int sleepAmount = (int)(m_targetFrameTime / 2.0).TotalMilliseconds;

            var jobs = new List<WorkItem>();
            var completedJobs = new List<WorkItem>();
            TimeSpan lastFrameTime = m_sw.Elapsed;

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

                while ((m_sw.Elapsed - lastFrameTime) < m_targetFrameTime)
                {
                    NanoSleep(m_sw.Elapsed - lastFrameTime);
                }

                completedJobs.Clear();
                foreach (var job in jobs)
                {
                    var progress = Math.Min(1.0, (m_sw.Elapsed - job.StartTime).TotalMilliseconds / job.Job.Duration.TotalMilliseconds);
                    job.Job.Update(progress);
                    if (progress >= 1.0)
                    {
                        completedJobs.Add(job);
                        job.Job.OnCompleted();
                    }
                    else if (job.Job.IsCancelled)
                    {
                        completedJobs.Add(job);
                        job.Job.OnCancelled();
                    }
                }

                foreach (var completedJob in completedJobs)
                {
                    jobs.Remove(completedJob);
                }

                lastFrameTime = m_sw.Elapsed;
            }
        }

        public void Dispose()
        {
            m_queue.CompleteAdding();
            m_thread.Join(1000);
        }

        private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x2;
        private const uint TIMER_ALL_ACCESS = 0x001f0003;

        private static bool NanoSleep(TimeSpan timeSpan)
        {
            unsafe 
            {
                HANDLE hTimer = PInvoke.CreateWaitableTimerEx(null,new PCWSTR(), Constants.CREATE_WAITABLE_TIMER_MANUAL_RESET | CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
                if (hTimer.Value == IntPtr.Zero)
                {
                    return false;
                }

                long timeout100ns = (long)(timeSpan.TotalMilliseconds / 1E4);
                long li = -timeout100ns;

                if (!PInvoke.SetWaitableTimerEx(hTimer, &li, 0, null, null, null, 0))
                {
                    PInvoke.CloseHandle(hTimer);
                    return false;
                }

                PInvoke.WaitForSingleObject(hTimer, Constants.INFINITE);
                PInvoke.CloseHandle(hTimer);
                return true;
            }
        }
    }
}
