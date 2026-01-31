using System;
using System.Threading;

namespace FancyWM.Utilities
{
    public sealed class UnmanagedResourceGuard : IDisposable
    {
        private Action? _cleanupAction;
        private int _disposed = 0;

        public UnmanagedResourceGuard(Action cleanupAction)
        {
            _cleanupAction = cleanupAction ?? throw new ArgumentNullException(nameof(cleanupAction));
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        ~UnmanagedResourceGuard()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

            try
            {
                _cleanupAction?.Invoke();
            }
            catch
            {
            }

            _cleanupAction = null;
        }
    }
}
