using System.Windows;
using System.Windows.Media;

namespace FancyWM.Utilities
{
    internal static class DependencyObjectExtensions
    {
        public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            T? parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        public static int IndexOf(this DependencyObject parent, DependencyObject child)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(parent, i) == child)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
