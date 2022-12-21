using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FancyWM.Utilities
{
    internal static class Dispatchers
    {
        public static async ValueTask RunAsync(this Dispatcher dispatcher, Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await dispatcher.InvokeAsync(action);
            }
        }

        public static async ValueTask<T> RunAsync<T>(this Dispatcher dispatcher, Func<T> action)
        {
            if (dispatcher.CheckAccess())
            {
                return action();
            }
            else
            {
                return await dispatcher.InvokeAsync(action);
            }
        }

        public static async ValueTask RunAsync(this Dispatcher dispatcher, Func<Task> action)
        {
            if (dispatcher.CheckAccess())
            {
                await action();
            }
            else
            {
                await await dispatcher.InvokeAsync(action);
            }
        }

        public static async ValueTask<T> RunAsync<T>(this Dispatcher dispatcher, Func<Task<T>> action)
        {
            if (dispatcher.CheckAccess())
            {
                return await action();
            }
            else
            {
                return await await dispatcher.InvokeAsync(action);
            }
        }

        public static void RethrowOnDispatcher(this Dispatcher dispatcher, Exception exception)
        {
            dispatcher.BeginInvoke((Action)(() =>
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }));
        }
    }
}
