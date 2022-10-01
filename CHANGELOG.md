# Changelog

## v2.10.0
- **Move windows by holding the Alt key**
- Fix GIMP compatibility #133
- Fix Snipping Tool window mode not working #42
- Performance improvements

## v2.9.1
- Keybindings page fixes

## v2.9.0
- UI refresh (new icons, tweaks)
- Close button in panels
- Preserve focused windows when switching to non-tiled windows
- Bug fixes

## v2.8.2
- Show desktop shortcut (default: D)
- Bug fixes

## v2.8.1
- Bug fixes

## v2.8.0
- **New shortcuts for moving between displays**
- Switch to previous display shortcut (default: E)
- Move to previous display shortcut (default: Shift + E)
- Fixed some scaling and multi-monitor issues
- Fixed regular expressions when used in the process name in the Rules tab

## v2.7.0
- (Experimental) New panel gestures: Drag the panel buttons from the window actions bar over another window to group in a new panel
- Automatically collapse panels when only a single window is left
- Allow left/right modifier keys to be used interchangeably
- Switch to previous desktop shortcut (default: Q)
- Move to previous desktop shortcut (default: Shift + Q)

## v2.6.7
- Allow the license to be used offline

## v2.6.6
- Bug fixes

## v2.6.5
- Bug fixes

## v2.6.4
- Fix window focus when using Switch Desktop
- Bug fixes

## v2.6.3
- Fix memory leak after a monitor is disconnected

## v2.6.2
- Fix floating rules for UWP apps

## v2.6.1
- Fix window movement not detected when using Windows 10 monitor hotkeys
- Fix crashes with experimental direct hotkeys feature

## v2.6.0
- Added an option to run FancyWM as Administrator on startup #10
- Added experimental support for direct keybindings (which do not use the Activation hotkey) #19
- Fixed stuck overlay when tiling is disabled #33
- Fixed Focus Assist sometimes enabled automatically #46
- Removed colourful Emoji support (this reduced idle memory usage by ~25 MiB) #41
- Made window animations smoother

## v2.5.0
- Added floating window rules based on window class
- Added "More options" to window actions
- Added shortcuts to "More options" for quick addition of floating rules #38
- Fix maximized applications pushed back on secondary display #29
- Fix crashes for windows which do not support WM_GETMINMAXINFO #25

## v2.4.1
- Fix crashes for some apps with custom-drawn frame

## v2.4.0
- Scripting support (preview) #20
- Added dedicated keybindings per virtual desktop
- Fixed lost window size when restored #27
- Fixed "Open in Text Editor"
- Fixed incorrect font scaling on multiple monitors
- Fixed broken process ignore list
- Fixed text cutoff in settings #22

## v2.3.5
- Multiple monitor fixes

## v2.3.4
- Bug fixes

## v2.3.3
- More Mica material
- Bug fixes

## v2.3.2
- Bug fixes

## v2.3.0
- Added window resizing keybindings!
- Mica is now used in the overlay and settings (faux Mica)
- FancyWM will now try to automatically stack new windows instead of giving up and enabling floating mode
- FancyWM will now try to pull up windows beyond their parent panel when the window cannot fit in the parent
- The focused window highlight now disappears more quickly
- Improved responsiveness when resizing windows
- Miscellaneous bug fixes

## v2.2.0
- Window padding not duplicated (#13)
- Highlight the focused window during command pop-up (on by default) (#15)
- Disable rounded corners on Windows 10
- Improved tie-breaking when no permissible arrangement can be computed

## v2.1.0
- Added new "Move window" keybindings
- Window items in the panel overlays can be scrolled with the mouse wheel
- The Cancel action (default: [Esc]) is now reassignable in the Settings
- "Move focus left" now focuses the first window in the panel, instead of the last (more intuitive)
