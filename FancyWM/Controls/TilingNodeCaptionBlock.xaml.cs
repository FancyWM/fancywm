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
using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TilingNodeCaptionBlock.xaml
    /// </summary>
    public partial class TilingNodeCaptionBlock : UserControl
    {
        public static readonly DependencyProperty MaxCharactersProperty = DependencyProperty.Register(
            nameof(MaxCharacters), typeof(int), typeof(TilingNodeCaptionBlock));

        public int MaxCharacters
        {
            get => (int)GetValue(MaxCharactersProperty);
            set => SetValue(MaxCharactersProperty, value);
        }

        public static readonly DependencyProperty ExtraTextVisibilityProperty = DependencyProperty.Register(
            nameof(ExtraTextVisibility), typeof(Visibility), typeof(TilingNodeCaptionBlock));

        public Visibility ExtraTextVisibility
        {
            get => (Visibility)GetValue(ExtraTextVisibilityProperty);
            set => SetValue(ExtraTextVisibilityProperty, value);
        }

        public TilingNodeCaptionBlock()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == FontSizeProperty || e.Property == MaxCharactersProperty)
            {
                Resources["CalculatedMaxWidth"] = FontSize * MaxCharacters;
            }
        }
    }
}
