using System.Collections.ObjectModel;

using FancyWM.Layouts.Tiling;

using WinMan;

namespace FancyWM.ViewModels
{
    public class TilingPanelViewModel : TilingNodeViewModel
    {
        private ObservableCollection<TilingNodeViewModel> m_childNodes = [];
        private Rectangle m_bounds;
        private bool m_isHeaderObscured;
        private bool m_isMoving;
        private bool m_childHasDirectFocus;
        private double m_tabWidth;
        private TilingNodeType m_panelType;
        private PanelOrientation m_panelOrientation;

        public double TabWidth { get => m_tabWidth; set => SetField(ref m_tabWidth, value); }

        public ObservableCollection<TilingNodeViewModel> ChildNodes { get => m_childNodes; set => SetField(ref m_childNodes, value); }

        public Rectangle HeaderBounds { get => m_bounds; set => SetField(ref m_bounds, value); }

        public bool IsHeaderVisible { get => m_isHeaderObscured; set => SetField(ref m_isHeaderObscured, value); }

        public bool IsMoving { get => m_isMoving; set => SetField(ref m_isMoving, value); }

        public bool ChildHasDirectFocus { get => m_childHasDirectFocus; set => SetField(ref m_childHasDirectFocus, value); }

        public TilingNodeType PanelType { get => m_panelType; set => SetField(ref m_panelType, value); }

        public PanelOrientation PanelOrientation { get => m_panelOrientation; set => SetField(ref m_panelOrientation, value); }
    }
}
