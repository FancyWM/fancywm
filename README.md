# FancyWM

[![Gitter](https://badges.gitter.im/FancyWM/community.svg)](https://gitter.im/FancyWM/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![Donate via PayPal](https://shields.io/badge/Donate-gold?logo=paypal&style=flat)](https://www.paypal.com/donate/?hosted_button_id=NKQ6DKGFVN7S2)

FancyWM is a dynamic tiling window manager for Windows 10/11

<img align="right" src="https://store-images.s-microsoft.com/image/apps.53415.14517052119257390.d950e654-2004-4878-b902-94902f8f7a45.af24879e-636a-494c-ba1d-6ff7f858630b?background=transparent&w=175&h=175&format=jpg">

☑ Create dynamic tiling layouts with mouse or keyboard <br>
☑ Move window focus with keyboard ([⇧ Shift] + [⊞ Win], then [→]) <br>
☑ Swap windows with keyboard ([⇧ Shift] + [⊞ Win], then [⇧ Shift] + [→]) <br>
☑ Swap windows with mouse (hold [⇧ Shift] while dragging) <br>
☑ Horizontal panels ([⇧ Shift] + [⊞ Win], then [H]) <br>
☑ Vertical panels ([⇧ Shift] + [⊞ Win], then [V]) <br>
☑ Stack panels (tabbed layouts) ([⇧ Shift] + [⊞ Win], then [S]) <br>
☑ Panel embedding <br>
☑ Jump to virtual desktop ([⇧ Shift] + [⊞ Win], then [2]) <br>
☑ Move focused window to virtual desktop ([⇧ Shift] + [⊞ Win], then [⇧ Shift] + [2]) <br>
☑ Floating window mode ([⇧ Shift] + [⊞ Win], then [F] or rule-based) <br>
☑ Auto-float windows which cannot fit <br>
☑ Customizable keybindings <br>
☑ Support for multiple monitors <br>
☑ Support for virtual desktops <br>
☑ Allows window maximization <br>
☑ Toggle tiling on/off ([⇧ Shift] + [⊞ Win], then [F11]) <br>
☑ Low CPU usage (<1%) <br>
☑ Disable animations for longer battery life <br>
☑ Windows open in focused panel <br>
☑ Remap activation hotkey to [⇧ Shift] + [⊞ Win], [Ctrl] + [⊞ Win] or [Alt] + [⊞ Win] <br>

FancyWM uses [⇧ Shift] + [⊞ Win] as the start of a command sequence (Activation hotkey). To start a command sequence, press and release these keys simultaneously, then follow up by pressing one of the keybindings you have configured in the settings.

FancyWM only manages restored (not minimized, not maximized) top-level application windows, so it doesn't interfere with popups, and still allows you to use all of your available display area for when you need to focus on a window

## [Downloads](https://github.com/FancyWM/fancywm/releases)

Pre-built binaries can be downloaded from [Releases](https://github.com/FancyWM/fancywm/releases).

These are built by an automated GitHub Action and you can see all of the [build steps](https://github.com/FancyWM/fancywm/blob/main/.github/workflows/dotnet-desktop.yml) and [previous runs](https://github.com/FancyWM/fancywm/actions/workflows/dotnet-desktop.yml).

### Install .msixbundle (not recommended)
You can test the Microsoft Store packages by installing them using PowerShell.

#### PowerShell (as Administrator)
```
certutil.exe -addstore TrustedPeople .\FancyWM.Package_1.0.0.0.x64.cer
Add-AppxPackage -Path .\FancyWM.Package_1.0.0.0.x64.msixbundle
```

## [User's Guide](https://github.com/FancyWM/fancywm/wiki#users-guide)
Head over to the [Wiki](https://github.com/FancyWM/fancywm/wiki).

## [Issues](https://github.com/FancyWM/fancywm/issues)
Please, take the time to report any problems you experience by:
- Opening an issue on https://github.com/veselink1/fancywm/issues (feature requests also welcome)
In case of crashes, please also remember to save and attach the log file produced by the application.

## Building from source

Clone this repo, including submodules.

```bash
git clone --recursive https://github.com/FancyWM/fancywm.git
```

Open the .sln file with Visual Studio 2022 and build the FancyWM project.

## WinMan & WinMan.Windows
FancyWM is based on [WinMan](https://github.com/veselink1/winman) and [WinMan.Windows](https://github.com/veselink1/winman-windows).

## Screenshots
<img src="https://store-images.s-microsoft.com/image/apps.47394.14517052119257390.5224238b-c5af-4852-a39a-2732c3935e69.60fa12a6-ac5a-47cb-9501-2ca7964d972d?w=1280&h=720&q=90&mode=letterbox&format=jpg" width="640">
Light theme, Vertical panel on the left

---

<img src="https://store-images.s-microsoft.com/image/apps.11856.14517052119257390.5224238b-c5af-4852-a39a-2732c3935e69.81bfbc4c-0b20-4b1e-a1b5-b8e6fa13f8a6?w=1280&h=720&q=90&mode=letterbox&format=jpg" width="640">
Dark theme, Vertical panel on the left, Stack panel with 3 VS Code windows on the right

---

<img src="https://store-images.s-microsoft.com/image/apps.11856.14517052119257390.5224238b-c5af-4852-a39a-2732c3935e69.81bfbc4c-0b20-4b1e-a1b5-b8e6fa13f8a6?w=1280&h=720&q=90&mode=letterbox&format=jpg" width="640">
Vertical panel on the left, Edge in the middle, Vertical panel on the right

---
