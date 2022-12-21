using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TilingPanel.xaml
    /// </summary>
    public partial class TilingPanel : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TilingPanelViewModel),
            typeof(TilingPanel),
            new PropertyMetadata(null));

        public TilingPanelViewModel ViewModel
        {
            get => (TilingPanelViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public TilingPanel()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ViewModelProperty)
            {
                DataContext = ViewModel;
            }
        }

        private void OnHandleMouseEnter(object sender, MouseEventArgs e)
        {
            foreach (var node in EnumerateTree(ViewModel))
            {
                if (node is TilingWindowViewModel windowVm)
                {
                    windowVm.IsPreviewVisible = true;
                }
            }
        }

        private void OnHandleMouseLeave(object sender, MouseEventArgs e)
        {
            foreach (var node in EnumerateTree(ViewModel))
            {
                if (node is TilingWindowViewModel windowVm)
                {
                    windowVm.IsPreviewVisible = false;
                }
            }
        }

        private IEnumerable<TilingNodeViewModel> EnumerateTree(TilingNodeViewModel vm)
        {
            yield return vm;
            if (vm is TilingPanelViewModel panelVn)
            {
                foreach (var childVm in ViewModel.ChildNodes.SelectMany(x => x is TilingPanelViewModel p
                    ? Enumerable.Repeat(p, 1).Concat(p.ChildNodes)
                    : Enumerable.Repeat(x, 1)))
                {
                    yield return childVm;
                }
            }
        }

        private void OnPanelMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel is TilingPanelViewModel panelVn)
            {
                if (!panelVn.ChildHasDirectFocus)
                {
                    if (panelVn.ChildNodes.First().Node?.Windows.FirstOrDefault() is WindowNode windowNode)
                    {
                        FocusHelper.ForceActivate(windowNode.WindowReference.Handle);
                    }
                }
            }
        }
    }
}
