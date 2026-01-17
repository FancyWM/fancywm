namespace FancyWM.Layouts.Tiling
{
    public abstract class GridLikeNode : PanelNode
    {
        public abstract bool CanResizeInOrientation(PanelOrientation orientation);

        public abstract bool ResizeTo(TilingNode node, double newLength, GrowDirection direction);

        public abstract bool ResizeBy(TilingNode node, double delta, GrowDirection direction);
    }
}
