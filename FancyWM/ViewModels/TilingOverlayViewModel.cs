using System.Collections.ObjectModel;
using System.Windows;

using WinMan;

namespace FancyWM.ViewModels
{
    public class TilingOverlayViewModel : ViewModelBase
    {
        private Visibility m_overlayVisibility;
        private ObservableCollection<TilingPanelViewModel> m_panelElements = [];
        private ObservableCollection<TilingWindowViewModel> m_windowElements = [];
        private Rectangle m_focusRectangle;
        private Rectangle m_previewRectangle;

        public Visibility OverlayVisibility { get => m_overlayVisibility; set => SetField(ref m_overlayVisibility, value); }

        public ObservableCollection<TilingPanelViewModel> PanelElements { get => m_panelElements; set => SetField(ref m_panelElements, value); }

        public ObservableCollection<TilingWindowViewModel> WindowElements { get => m_windowElements; set => SetField(ref m_windowElements, value); }

        public Rectangle FocusRectangle { get => m_focusRectangle; set => SetField(ref m_focusRectangle, value); }

        [DerivedProperty(nameof(FocusRectangle))]
        public bool IsFocusRectangleVisible => m_focusRectangle.Width == 0;

        public Rectangle PreviewRectangle { get => m_previewRectangle; set => SetField(ref m_previewRectangle, value); }

        [DerivedProperty(nameof(PreviewRectangle))]
        public bool IsPreviewRectangleVisible => m_previewRectangle.Width == 0;
    }
}
