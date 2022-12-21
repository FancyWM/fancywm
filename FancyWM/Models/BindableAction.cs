using System.Windows.Input;

namespace FancyWM.Models
{
    public enum BindableAction
    {
        // Group: FancyWM
        [DefaultKeybinding(Key.F11)]
        ToggleManager,
        [DefaultKeybinding(Key.R)]
        RefreshWorkspace,
        [DefaultKeybinding(Key.Escape)]
        Cancel,

        // Group: Focus
        [DefaultKeybinding(Key.Left)]
        MoveFocusLeft,
        [DefaultKeybinding(Key.Up)]
        MoveFocusUp,
        [DefaultKeybinding(Key.Right)]
        MoveFocusRight,
        [DefaultKeybinding(Key.Down)]
        MoveFocusDown,
        [DefaultKeybinding(Key.D)]
        ShowDesktop,

        // Group: Panels
        [DefaultKeybinding(Key.H)]
        CreateHorizontalPanel,
        [DefaultKeybinding(Key.V)]
        CreateVerticalPanel,
        [DefaultKeybinding(Key.S)]
        CreateStackPanel,

        // Group: Windows
        [DefaultKeybinding(Key.Enter)]
        PullWindowUp,
        [DefaultKeybinding(Key.F)]
        ToggleFloatingMode,
        [DefaultKeybinding(Key.LeftCtrl, Key.Left)]
        MoveLeft,
        [DefaultKeybinding(Key.LeftCtrl, Key.Up)]
        MoveUp,
        [DefaultKeybinding(Key.LeftCtrl, Key.Right)]
        MoveRight,
        [DefaultKeybinding(Key.LeftCtrl, Key.Down)]
        MoveDown,
        [DefaultKeybinding(Key.LeftShift, Key.Left)]
        SwapLeft,
        [DefaultKeybinding(Key.LeftShift, Key.Up)]
        SwapUp,
        [DefaultKeybinding(Key.LeftShift, Key.Right)]
        SwapRight,
        [DefaultKeybinding(Key.LeftShift, Key.Down)]
        SwapDown,

        // Group: Sizing
        [DefaultKeybinding(Key.OemCloseBrackets)]
        IncreaseWidth,
        [DefaultKeybinding(Key.OemQuotes)]
        IncreaseHeight,
        [DefaultKeybinding(Key.OemOpenBrackets)]
        DecreaseWidth,
        [DefaultKeybinding(Key.OemSemicolon)]
        DecreaseHeight,

        // Group: Virtual Desktops
        [DefaultKeybinding(Key.Q)]
        SwitchToPreviousDesktop,
        [DefaultKeybinding(Key.D1)]
        SwitchToDesktop1,
        [DefaultKeybinding(Key.D2)]
        SwitchToDesktop2,
        [DefaultKeybinding(Key.D3)]
        SwitchToDesktop3,
        [DefaultKeybinding(Key.D4)]
        SwitchToDesktop4,
        [DefaultKeybinding(Key.D5)]
        SwitchToDesktop5,
        [DefaultKeybinding(Key.D6)]
        SwitchToDesktop6,
        [DefaultKeybinding(Key.D7)]
        SwitchToDesktop7,
        [DefaultKeybinding(Key.D8)]
        SwitchToDesktop8,
        [DefaultKeybinding(Key.D9)]
        SwitchToDesktop9,
        [DefaultKeybinding(Key.LeftShift, Key.Q)]
        MoveToPreviousDesktop,
        [DefaultKeybinding(Key.LeftShift, Key.D1)]
        MoveToDesktop1,
        [DefaultKeybinding(Key.LeftShift, Key.D2)]
        MoveToDesktop2,
        [DefaultKeybinding(Key.LeftShift, Key.D3)]
        MoveToDesktop3,
        [DefaultKeybinding(Key.LeftShift, Key.D4)]
        MoveToDesktop4,
        [DefaultKeybinding(Key.LeftShift, Key.D5)]
        MoveToDesktop5,
        [DefaultKeybinding(Key.LeftShift, Key.D6)]
        MoveToDesktop6,
        [DefaultKeybinding(Key.LeftShift, Key.D7)]
        MoveToDesktop7,
        [DefaultKeybinding(Key.LeftShift, Key.D8)]
        MoveToDesktop8,
        [DefaultKeybinding(Key.LeftShift, Key.D9)]
        MoveToDesktop9,

        // Group: Multiple Displays
        [DefaultKeybinding(Key.E)]
        SwitchToPreviousDisplay,
        [DefaultKeybinding(Key.F1)]
        SwitchToDisplay1,
        [DefaultKeybinding(Key.F2)]
        SwitchToDisplay2,
        [DefaultKeybinding(Key.F3)]
        SwitchToDisplay3,
        [DefaultKeybinding(Key.F4)]
        SwitchToDisplay4,
        [DefaultKeybinding(Key.F5)]
        SwitchToDisplay5,
        [DefaultKeybinding(Key.F6)]
        SwitchToDisplay6,
        [DefaultKeybinding(Key.F7)]
        SwitchToDisplay7,
        [DefaultKeybinding(Key.F8)]
        SwitchToDisplay8,
        [DefaultKeybinding(Key.F9)]
        SwitchToDisplay9,
        [DefaultKeybinding(Key.LeftShift, Key.E)]
        MoveToPreviousDisplay,
        [DefaultKeybinding(Key.LeftShift, Key.F1)]
        MoveToDisplay1,
        [DefaultKeybinding(Key.LeftShift, Key.F2)]
        MoveToDisplay2,
        [DefaultKeybinding(Key.LeftShift, Key.F3)]
        MoveToDisplay3,
        [DefaultKeybinding(Key.LeftShift, Key.F4)]
        MoveToDisplay4,
        [DefaultKeybinding(Key.LeftShift, Key.F5)]
        MoveToDisplay5,
        [DefaultKeybinding(Key.LeftShift, Key.F6)]
        MoveToDisplay6,
        [DefaultKeybinding(Key.LeftShift, Key.F7)]
        MoveToDisplay7,
        [DefaultKeybinding(Key.LeftShift, Key.F8)]
        MoveToDisplay8,
        [DefaultKeybinding(Key.LeftShift, Key.F9)]
        MoveToDisplay9,
    }
}
