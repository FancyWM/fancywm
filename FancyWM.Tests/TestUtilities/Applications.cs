using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FancyWM.Tests.TestUtilities
{
    internal static class Applications
    {
        public static async Task WithAppContextAsync(Func<Task> action)
        {
            var tcs = new TaskCompletionSource();
            Application app = null!;

            var t = new Thread(() =>
            {
                app = new();
                app.Startup += async delegate
                {
                    try
                    {
                        await action();
                        tcs.SetResult();
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                };
                app.Run(new Window());
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            try
            {
                await tcs.Task;
            }
            finally
            {
                await app.Dispatcher.InvokeAsync(() => app.Shutdown());
            }
        }
    }
}
