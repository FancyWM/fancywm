using System;

using WinMan;
using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using System.Collections.Generic;

namespace FancyWM
{
    internal class TilingFailedEventArgs(TilingError reason, IWindow? window = null) : EventArgs
    {
        public TilingError FailReason { get; } = reason;
        public IWindow? FailSource { get; } = window;
    }

    internal interface ITilingService : IDisposable
    {
        event EventHandler<TilingFailedEventArgs> PlacementFailed;

        event EventHandler<EventArgs> PendingIntentChanged;

        bool Active { get; }

        IWorkspace Workspace { get; }

        IReadOnlyCollection<IWindowMatcher> ExclusionMatchers { get; set; }

        bool ShowFocus { get; set; }

        bool AutoCollapse { get; set; }

        int AutoSplitCount { get; set; }

        bool DelayReposition { get; set; }

        ITilingServiceIntent? PendingIntent { get; set; }

        bool CanSplit(bool vertical);
        void Split(bool vertical);
        bool CanStack();
        void Stack();
        bool DiscoverWindows();
        void Refresh();
        bool CanFloat();
        void Float();
        void ToggleDesktop();
        bool CanMoveFocus(TilingDirection direction);
        void MoveFocus(TilingDirection direction);
        bool CanPullUp();
        void PullUp();
        bool CanSwapFocus(TilingDirection direction);
        void SwapFocus(TilingDirection direction);
        bool CanMoveWindow(TilingDirection direction);
        void MoveWindow(TilingDirection direction);
        bool CanResize(PanelOrientation orientation, double displayPercentage);
        void Resize(PanelOrientation orientation, double displayPercentage);

        void Stop();
        void Start();
        IWindow? GetFocus();
        Rectangle GetBounds();
        IWindow? FindClosest(Point center);
    }

    public interface ITilingServiceIntent
    {
        void Complete();
        void Cancel();
    }

    public class GroupWithIntent(GroupWithIntent.GroupType type, WindowNode source, Action complete, Action cancel) : ITilingServiceIntent
    {
        public enum GroupType
        {
            HorizontalPanel,
            VerticalPanel,
            StackPanel,
        }

        public GroupType Type { get; } = type;

        public WindowNode Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

        private Action? m_completeIntent = complete;

        private Action? m_cancelIntent = cancel;

        /// <summary>
        /// Releases the source from its panel and unregisters it.
        /// </summary>
        /// <returns></returns>
        public void Complete()
        {
            m_completeIntent?.Invoke();
            m_completeIntent = null;
        }


        /// <summary>
        /// Cancels the intent.
        /// </summary>
        /// <returns></returns>
        public void Cancel()
        {
            m_cancelIntent?.Invoke();
            m_cancelIntent = null;
        }
    }
}
