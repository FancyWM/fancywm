using System;
using System.Windows;

namespace FancyWM.Utilities
{
    internal class GCHelper
    {
        public static void ScheduleCollection()
        {
            Application.Current.Dispatcher.BeginInvoke(Collect, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void Collect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        }
    }
}
