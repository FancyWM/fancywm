using System;
using System.Windows;

namespace FancyWM.Utilities
{
    class NotifyUnhandledObserver<T> : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnNext(T value)
        {
        }

        public void OnError(Exception error)
        {
            Application.Current.Dispatcher.RethrowOnDispatcher(error);
        }
    }
}
