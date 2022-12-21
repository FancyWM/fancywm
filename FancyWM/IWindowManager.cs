using System;

using WinMan;

namespace FancyWM
{
    public interface IWindowManager : IDisposable
    {
        IWorkspace Workspace { get; }

        bool Active { get; }

        void Start();

        void Stop();
    }
}
