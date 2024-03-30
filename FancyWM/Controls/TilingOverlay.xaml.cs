using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TilingOverlay.xaml
    /// </summary>
    public partial class TilingOverlay : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TilingOverlayViewModel),
            typeof(TilingOverlay),
            new PropertyMetadata(null));

        public TilingOverlayViewModel ViewModel
        {
            get => (TilingOverlayViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public TilingOverlay()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ViewModelProperty)
            {
                if (e.OldValue is ViewModelBase oldViewModel)
                {
                    oldViewModel.PropertyChanged -= OnDataContextPropertyChanged;
                }
                DataContext = ViewModel;
                ViewModel.PropertyChanged += OnDataContextPropertyChanged;
            }
        }

        private void OnDataContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Duration duration = new(TimeSpan.FromMilliseconds(200));
            var ease = new SineEase
            {
                //Bounces = 2,
                EasingMode = EasingMode.EaseOut,
                //Bounciness = 2
            };

            if (e.PropertyName == nameof(TilingOverlayViewModel.OverlayVisibility))
            {
                BeginAnimation(OpacityProperty, null);

                DoubleAnimation opacityAnimation = new(1, duration)
                {
                    EasingFunction = ease,
                };

                if (ViewModel.OverlayVisibility == Visibility.Visible)
                {
                    opacityAnimation.From = 0;
                    opacityAnimation.To = 1;
                    IsHitTestVisible = true;
                }
                else
                {
                    opacityAnimation.From = 1;
                    opacityAnimation.To = 0;
                    IsHitTestVisible = false;
                }

                BeginAnimation(OpacityProperty, opacityAnimation);
            }
        }
    }
}
