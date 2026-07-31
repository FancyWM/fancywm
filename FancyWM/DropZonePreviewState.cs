using WinMan;

namespace FancyWM
{
    public enum DropZonePreviewKind
    {
        Center,
        Left,
        Right,
        Top,
        Bottom,
        Neutral,
    }

    public sealed record DropZonePreviewState(
        bool IsActive,
        DropZonePreviewKind ActiveZone,
        Rectangle Center,
        Rectangle Left,
        Rectangle Top,
        Rectangle Right,
        Rectangle Bottom,
        Rectangle TargetOutline);
}
