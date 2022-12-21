using System;
using System.Windows.Media;

namespace FancyWM.Utilities
{
    public class MicaOptionsChangedEventArgs : EventArgs
    {
        public MicaOptionsChangedEventArgs()
        {
        }
    }

    internal interface IMicaProvider : IDisposable
    {
        event EventHandler<MicaOptionsChangedEventArgs> PrimaryColorChanged;

        Color PrimaryColor { get; }
    }
}
