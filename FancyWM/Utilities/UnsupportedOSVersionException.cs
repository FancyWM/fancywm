using System;

namespace FancyWM.Utilities
{
    internal class UnsupportedOSVersionException : Exception
    {
        public UnsupportedOSVersionException(string? message) : base(message)
        {
        }

        public UnsupportedOSVersionException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
