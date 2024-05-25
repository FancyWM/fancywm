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
        private Rectangle m_previewRectangle;

        public Visibility OverlayVisibility { get => m_overlayVisibility; set => SetField(ref m_overlayVisibility, value); }

        public ObservableCollection<TilingPanelViewModel> PanelElements { get => m_panelElements; set => SetField(ref m_panelElements, value); }

        public ObservableCollection<TilingWindowViewModel> WindowElements { get => m_windowElements; set => SetField(ref m_windowElements, value); }

        public Rectangle PreviewRectangle { get => m_previewRectangle; set => SetField(ref m_previewRectangle, value); }

        [DerivedProperty(nameof(PreviewRectangle))]
        public bool IsPreviewRectangleVisible => m_previewRectangle.Width == 0;
    }
}
