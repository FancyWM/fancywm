using System.Threading.Tasks;
using System.Windows.Input;

using WinMan;

namespace FancyWM.Utilities
{
    internal static class WorkspaceExtensions
    {
        public static Task WaitForLeftMouseButtonState(this IWorkspace workspace, MouseButtonState state)
        {
            var tcs = new TaskCompletionSource();
            void eventHandler(object? sender, CursorLocationChangedEventArgs e)
            {
                if (Mouse.LeftButton == state)
                {
                    workspace.CursorLocationChanged -= eventHandler;
                    tcs.SetResult();
                }
            }
            workspace.CursorLocationChanged += eventHandler;
            return tcs.Task;
        }

        public static int Index(this IDisplay display)
        {
            return display.Workspace.DisplayManager.Displays.IndexOf(display);
        }
    }
}
