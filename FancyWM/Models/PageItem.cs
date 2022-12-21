using System;

namespace FancyWM.ViewModels
{
    public class PageItem : ViewModelBase
    {
        public object? Header { get; set; }
        public Type? Page { get; set; }
    }
}
