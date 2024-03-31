using System;
using System.Diagnostics;

namespace FancyWM.Utilities
{
    public class BindingErrorListener(Action<string?> logAction) : TraceListener
    {
        public static void Listen(Action<string?> logAction)
        {
            PresentationTraceSources.DataBindingSource.Listeners
                .Add(new BindingErrorListener(logAction));
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            logAction?.Invoke(message);
        }
    }
}
