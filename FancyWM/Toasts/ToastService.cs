using System.Threading;
using System.Threading.Tasks;

using WinMan;

namespace FancyWM.Toasts
{
    internal class ToastService : IToastService
    {
        private readonly IWorkspace m_workspace;
        private readonly ToastWindow m_toastWindow;

        public ToastService(IWorkspace workspace)
        {
            m_workspace = workspace;
            m_toastWindow = App.Current.Dispatcher.Invoke(() => new ToastWindow(m_workspace));
        }

        public async Task ShowToastAsync(object content, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var tcs = new TaskCompletionSource();

            await m_toastWindow.Dispatcher.InvokeAsync(() => m_toastWindow.ShowToast(content, cancellationToken));

            using var completeRegistration = cancellationToken.Register(() => tcs.TrySetResult());
            await tcs.Task;
        }
    }
}
