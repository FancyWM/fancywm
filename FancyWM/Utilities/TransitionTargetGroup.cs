using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WinMan;

namespace FancyWM.Utilities
{
    internal class TransitionTargetGroup(IAnimationThread animationThread, IEnumerable<TransitionTarget> targets)
    {
        private readonly IAnimationThread m_animationThread = animationThread;
        private readonly List<TransitionTarget> m_targets = targets.ToList();

        private static async ValueTask RunOnThreadPool(Action action)
        {
            await await Task.Run(() =>
            {
                try
                {
                    action();
                    return ValueTask.CompletedTask;
                }
                catch (Exception e)
                {
                    return ValueTask.FromException(e);
                }
            });
        }

        public async Task PerformSmoothTransitionAsync(TimeSpan duration)
        {
            int Lerp(int x1, int x2, double t)
            {
                return (int)(x1 + (x2 - x1) * t);
            }

            Rectangle LerpRectPosition(Rectangle x1, Rectangle x2, double t)
            {
                bool isHalfway = t > 0.5;
                return Rectangle.OffsetAndSize(
                    Lerp(x1.Left, x2.Left, t),
                    Lerp(x1.Top, x2.Top, t),
                    isHalfway ? x2.Width : x1.Width,
                    isHalfway ? x2.Height : x1.Height);
            }

            var working = m_targets.Select(x => x.OriginalPosition).ToArray();
            var tasks = new List<Task>();

            var ease = EasingFunction.EaseInOutCirc;

            for (int i = 0; i < m_targets.Count; i++)
            {
                // Capture index
                var index = i;
                IAnimationJob job = AnimationJob.Create(async (job, progress) =>
                {
                    try
                    {
                        if (working[index] == m_targets[index].Window.Position)
                        {
                            Rectangle newCurrent = LerpRectPosition(m_targets[index].OriginalPosition, m_targets[index].ComputedPosition, ease.Evaluate(progress));
                            await RunOnThreadPool(() => m_targets[index].Window.SetPosition(newCurrent));
                            if (newCurrent == m_targets[index].ComputedPosition)
                            {
                                job.Cancel();
                                return;
                            }
                            working[index] = newCurrent;
                        }
                        else
                        {
                            await RunOnThreadPool(() => m_targets[index].Window.SetPosition(m_targets[index].ComputedPosition));
                            job.Cancel();
                        }
                    }
                    catch (InvalidWindowReferenceException)
                    {
                        job.Cancel();
                    }
                    catch (InvalidOperationException) when (m_targets[index].Window.State != WindowState.Restored)
                    {
                        job.Cancel();
                    }
                }, duration);

                tasks.Add(job.Task);
                m_animationThread.Start(job);
            }

            // Wait for all animations to complete.
            await Tasks.WhenAllIgnoreCancelled(tasks);
        }

        public static void PerformTransition(List<TransitionTarget> targets)
        {
            foreach (var target in targets)
            {
                try
                {
                    target.Window.SetPosition(target.ComputedPosition);
                }
                catch (InvalidWindowReferenceException)
                {
                    continue;
                }
                catch (InvalidOperationException) when (target.Window.State != WindowState.Restored)
                {
                    continue;
                }
            }
        }
    }
}
