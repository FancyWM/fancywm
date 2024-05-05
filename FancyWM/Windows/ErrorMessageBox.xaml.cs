using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

using Windows.System;

namespace FancyWM.Windows
{
    /// <summary>
    /// Interaction logic for ErrorMessageBox.xaml
    /// </summary>
    public partial class ErrorMessageBox : Window
    {
        public static readonly DependencyProperty IsSubmitLogEnabledProperty = DependencyProperty.Register(
            nameof(IsSubmitLogEnabled),
            typeof(bool),
            typeof(ErrorMessageBox),
            new PropertyMetadata(true));

        public bool IsSubmitLogEnabled
        {
            get => (bool)GetValue(IsSubmitLogEnabledProperty);
            set => SetValue(IsSubmitLogEnabledProperty, value);
        }

        public static readonly DependencyProperty DetailsVisibilityProperty = DependencyProperty.Register(
            nameof(DetailsVisibility),
            typeof(Visibility),
            typeof(ErrorMessageBox),
            new PropertyMetadata(Visibility.Collapsed));

        public Visibility DetailsVisibility
        {
            get => (Visibility)GetValue(DetailsVisibilityProperty);
            set => SetValue(DetailsVisibilityProperty, value);
        }

        public static readonly DependencyProperty ExceptionObjectProperty = DependencyProperty.Register(
            nameof(ExceptionObject),
            typeof(Exception),
            typeof(ErrorMessageBox),
            new PropertyMetadata(null));

        public Exception? ExceptionObject
        {
            get => (Exception?)GetValue(ExceptionObjectProperty);
            set => SetValue(ExceptionObjectProperty, value);
        }

        public string? ExceptionMessage => ExceptionObject != null
            ? $"\n{ExceptionObject.GetType()}: {ExceptionObject.Message}"
            : null;

        public string ExceptionText => ExceptionObject?.ToString() ?? "null";

        public static readonly DependencyProperty IsRestartEnabledProperty = DependencyProperty.Register(
            nameof(IsRestartEnabled),
            typeof(bool),
            typeof(ErrorMessageBox),
            new PropertyMetadata(true));

        public bool IsRestartEnabled
        {
            get => (bool)GetValue(IsRestartEnabledProperty);
            set => SetValue(IsRestartEnabledProperty, value);
        }

        public ErrorMessageBox()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OnDetailsButtonClick(object sender, RoutedEventArgs e)
        {
            DetailsVisibility = DetailsVisibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void OnViewLogButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OpenIssue()
        {
            var title = Uri.EscapeDataString(ExceptionMessage ?? "");
            var body = Uri.EscapeDataString(@$"**Describe the bug**
Observing the following error:
```
{ExceptionText}
```

**To Reproduce**
Steps to reproduce the behavior:
1. Open '...'
2. Resize '....'
3. Enable '....'
4. See error

**Expected behavior**
A clear and concise description of what you expected to happen.

**Desktop (please complete the following information):**
 - OS: {Environment.OSVersion.VersionString}
 - FancyWM Version: {App.Current.VersionString}

**Additional context**
Add any other context about the problem here.
");
            var uri = new Uri($"https://github.com/fancywm/fancywm/issues/new?labels=bug&title={title}&body={body}");
            _ = Launcher.LaunchUriAsync(uri);
        }

        private void OnSubmitLogButtonClick(object sender, RoutedEventArgs e)
        {
            OpenIssue();
            DialogResult = false;
            Close();
        }

        private void OnQuitButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (IsRestartEnabled)
            {
                App.Current.RestartOnClose = true;
            }
        }

        private void OnRequestNavigate(object sender, MouseButtonEventArgs e)
        {
            OpenIssue();
        }
    }
}
