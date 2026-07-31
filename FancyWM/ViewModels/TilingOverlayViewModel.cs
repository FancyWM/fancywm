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
        private bool m_isDropZonePreviewVisible;
        private string m_dropZoneActiveKind = "";
        private Rectangle m_dropZoneOutlineRect;
        private Rectangle m_dropZoneCenterRect;
        private Rectangle m_dropZoneLeftRect;
        private Rectangle m_dropZoneTopRect;
        private Rectangle m_dropZoneRightRect;
        private Rectangle m_dropZoneBottomRect;
        private double m_displayScaling;
        private double m_fontSize;
        private double m_iconSize;
        private double m_tabWidth;
        private bool m_showTabCloseButton;

        public bool ShowTabCloseButton { get => m_showTabCloseButton; set => SetField(ref m_showTabCloseButton, value); }

        public double DisplayScaling { get => m_displayScaling; set => SetField(ref m_displayScaling, value); }
        public double FontSize { get => m_fontSize; set => SetField(ref m_fontSize, value); }
        public double IconSize { get => m_iconSize; set => SetField(ref m_iconSize, value); }
        public double TabWidth { get => m_tabWidth; set => SetField(ref m_tabWidth, value); }

        public Visibility OverlayVisibility { get => m_overlayVisibility; set => SetField(ref m_overlayVisibility, value); }

        public ObservableCollection<TilingPanelViewModel> PanelElements { get => m_panelElements; set => SetField(ref m_panelElements, value); }

        public ObservableCollection<TilingWindowViewModel> WindowElements { get => m_windowElements; set => SetField(ref m_windowElements, value); }

        public Rectangle FocusRectangle { get => m_focusRectangle; set => SetField(ref m_focusRectangle, value); }

        [DerivedProperty(nameof(FocusRectangle))]
        public bool IsFocusRectangleVisible => m_focusRectangle.Width != 0;

        public Rectangle PreviewRectangle { get => m_previewRectangle; set => SetField(ref m_previewRectangle, value); }

        [DerivedProperty(nameof(PreviewRectangle))]
        public bool IsPreviewRectangleVisible => m_previewRectangle.Width != 0;

        public bool IsDropZonePreviewVisible
        {
            get => m_isDropZonePreviewVisible;
            set => SetField(ref m_isDropZonePreviewVisible, value);
        }

        /// <summary>Center, Left, Right, Top, Bottom, Neutral, or empty when hidden.</summary>
        public string DropZoneActiveKind
        {
            get => m_dropZoneActiveKind;
            set => SetField(ref m_dropZoneActiveKind, value);
        }

        public Rectangle DropZoneOutlineRect { get => m_dropZoneOutlineRect; set => SetField(ref m_dropZoneOutlineRect, value); }

        public Rectangle DropZoneCenterRect { get => m_dropZoneCenterRect; set => SetField(ref m_dropZoneCenterRect, value); }

        public Rectangle DropZoneLeftRect { get => m_dropZoneLeftRect; set => SetField(ref m_dropZoneLeftRect, value); }

        public Rectangle DropZoneTopRect { get => m_dropZoneTopRect; set => SetField(ref m_dropZoneTopRect, value); }

        public Rectangle DropZoneRightRect { get => m_dropZoneRightRect; set => SetField(ref m_dropZoneRightRect, value); }

        public Rectangle DropZoneBottomRect { get => m_dropZoneBottomRect; set => SetField(ref m_dropZoneBottomRect, value); }
    }
}
