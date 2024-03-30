
using System;

using WinMan;

namespace FancyWM.Utilities
{
    internal class TransitionTarget(IWindow window, Rectangle originalPosition, Rectangle computedPosition)
    {
        public IWindow Window { get; set; } = window ?? throw new ArgumentNullException(nameof(window));
        public Rectangle OriginalPosition { get; set; } = originalPosition;
        public Rectangle ComputedPosition { get; set; } = computedPosition;
    }
}
