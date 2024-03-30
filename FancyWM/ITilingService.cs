using System;

using WinMan;
using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using System.Collections.Generic;

namespace FancyWM
{
    internal class TilingFailedEventArgs : EventArgs
    {
        public TilingError FailReason { get; }
        public IWindow? FailSource { get; }

        public TilingFailedEventArgs(TilingError reason, IWindow? window = null)
        {
            FailReason = reason;
            FailSource = window;
        }
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

        ITilingServiceIntent PendingIntent { get; set; }

        bool CanSplit(bool vertical);
        void Split(bool vertical);
        bool CanStack();
        void Stack();
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

    public class GroupWithIntent : ITilingServiceIntent
    {
        public enum GroupType
        {
            HorizontalPanel,
            VerticalPanel,
            StackPanel,
        }

        public GroupType Type { get; }

        public WindowNode Source { get; }

        private Action? m_completeIntent;

        private Action? m_cancelIntent;

        public GroupWithIntent(GroupType type, WindowNode source, Action complete, Action cancel)
        {
            Type = type;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            m_completeIntent = complete;
            m_cancelIntent = cancel;
        }

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
