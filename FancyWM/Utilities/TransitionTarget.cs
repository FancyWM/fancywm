
using System;

using WinMan;

namespace FancyWM.Utilities
{
    internal class TransitionTarget
    {
        public IWindow Window { get; set; }
        public Rectangle OriginalPosition { get; set; }
        public Rectangle ComputedPosition { get; set; }

        public TransitionTarget(IWindow window, Rectangle originalPosition, Rectangle computedPosition)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            OriginalPosition = originalPosition;
            ComputedPosition = computedPosition;
        }
    }
}
