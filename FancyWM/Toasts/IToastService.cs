using System;
using System.Threading;
using System.Threading.Tasks;

namespace FancyWM.Toasts
{
    internal interface IToastService
    {
        Task ShowToastAsync(object content, CancellationToken cancellationToken);
    }
}
