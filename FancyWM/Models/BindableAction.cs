
using FancyWM.Utilities;

namespace FancyWM.Models
{
    public enum BindableAction
    {
        // Group: FancyWM
        [DefaultKeybinding(KeyCode.F11)]
        ToggleManager,
        [DefaultKeybinding(KeyCode.R)]
        RefreshWorkspace,
        [DefaultKeybinding(KeyCode.Escape)]
        Cancel,

        // Group: Focus
        [DefaultKeybinding(KeyCode.Left)]
        MoveFocusLeft,
        [DefaultKeybinding(KeyCode.Up)]
        MoveFocusUp,
        [DefaultKeybinding(KeyCode.Right)]
        MoveFocusRight,
        [DefaultKeybinding(KeyCode.Down)]
        MoveFocusDown,
        [DefaultKeybinding(KeyCode.D)]
        ShowDesktop,

        // Group: Panels
        [DefaultKeybinding(KeyCode.H)]
        CreateHorizontalPanel,
        [DefaultKeybinding(KeyCode.V)]
        CreateVerticalPanel,
        [DefaultKeybinding(KeyCode.S)]
        CreateStackPanel,

        // Group: Windows
        [DefaultKeybinding(KeyCode.Enter)]
        PullWindowUp,
        [DefaultKeybinding(KeyCode.F)]
        ToggleFloatingMode,
        [DefaultKeybinding(KeyCode.LeftCtrl, KeyCode.Left)]
        MoveLeft,
        [DefaultKeybinding(KeyCode.LeftCtrl, KeyCode.Up)]
        MoveUp,
        [DefaultKeybinding(KeyCode.LeftCtrl, KeyCode.Right)]
        MoveRight,
        [DefaultKeybinding(KeyCode.LeftCtrl, KeyCode.Down)]
        MoveDown,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Left)]
        SwapLeft,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Up)]
        SwapUp,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Right)]
        SwapRight,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Down)]
        SwapDown,

        // Group: Sizing
        [DefaultKeybinding(KeyCode.OemCloseBrackets)]
        IncreaseWidth,
        [DefaultKeybinding(KeyCode.OemQuotes)]
        IncreaseHeight,
        [DefaultKeybinding(KeyCode.OemOpenBrackets)]
        DecreaseWidth,
        [DefaultKeybinding(KeyCode.OemSemicolon)]
        DecreaseHeight,

        // Group: Virtual Desktops
        [DefaultKeybinding(KeyCode.Q)]
        SwitchToPreviousDesktop,
        [DefaultKeybinding(KeyCode.Z)]
        SwitchToLeftDesktop,
        [DefaultKeybinding(KeyCode.X)]
        SwitchToRightDesktop,
        [DefaultKeybinding(KeyCode.D1)]
        SwitchToDesktop1,
        [DefaultKeybinding(KeyCode.D2)]
        SwitchToDesktop2,
        [DefaultKeybinding(KeyCode.D3)]
        SwitchToDesktop3,
        [DefaultKeybinding(KeyCode.D4)]
        SwitchToDesktop4,
        [DefaultKeybinding(KeyCode.D5)]
        SwitchToDesktop5,
        [DefaultKeybinding(KeyCode.D6)]
        SwitchToDesktop6,
        [DefaultKeybinding(KeyCode.D7)]
        SwitchToDesktop7,
        [DefaultKeybinding(KeyCode.D8)]
        SwitchToDesktop8,
        [DefaultKeybinding(KeyCode.D9)]
        SwitchToDesktop9,

        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Q)]
        MoveToPreviousDesktop,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.Z)]
        MoveToLeftDesktop,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.X)]
        MoveToRightDesktop,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D1)]
        MoveToDesktop1,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D2)]
        MoveToDesktop2,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D3)]
        MoveToDesktop3,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D4)]
        MoveToDesktop4,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D5)]
        MoveToDesktop5,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D6)]
        MoveToDesktop6,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D7)]
        MoveToDesktop7,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D8)]
        MoveToDesktop8,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.D9)]
        MoveToDesktop9,

        // Group: Multiple Displays
        [DefaultKeybinding(KeyCode.E)]
        SwitchToPreviousDisplay,
        [DefaultKeybinding(KeyCode.F1)]
        SwitchToDisplay1,
        [DefaultKeybinding(KeyCode.F2)]
        SwitchToDisplay2,
        [DefaultKeybinding(KeyCode.F3)]
        SwitchToDisplay3,
        [DefaultKeybinding(KeyCode.F4)]
        SwitchToDisplay4,
        [DefaultKeybinding(KeyCode.F5)]
        SwitchToDisplay5,
        [DefaultKeybinding(KeyCode.F6)]
        SwitchToDisplay6,
        [DefaultKeybinding(KeyCode.F7)]
        SwitchToDisplay7,
        [DefaultKeybinding(KeyCode.F8)]
        SwitchToDisplay8,
        [DefaultKeybinding(KeyCode.F9)]
        SwitchToDisplay9,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.E)]
        MoveToPreviousDisplay,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F1)]
        MoveToDisplay1,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F2)]
        MoveToDisplay2,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F3)]
        MoveToDisplay3,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F4)]
        MoveToDisplay4,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F5)]
        MoveToDisplay5,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F6)]
        MoveToDisplay6,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F7)]
        MoveToDisplay7,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F8)]
        MoveToDisplay8,
        [DefaultKeybinding(KeyCode.LeftShift, KeyCode.F9)]
        MoveToDisplay9,
    }
}
