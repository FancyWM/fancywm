using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FancyWM.Utilities
{
    internal static class Tasks
    {
        public static async Task WhenAllIgnoreCancelled(IEnumerable<Task> enumerable)
        {
            var tasks = enumerable.ToList();
            while (tasks.Any())
            {
                try
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
                catch (TaskCanceledException)
                {
                    tasks.RemoveAll(task => task.IsCanceled);
                }
            }
        }
    }
}
