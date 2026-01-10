# FancyWM


[![Gitter](https://badges.gitter.im/FancyWM/community.svg)](https://gitter.im/FancyWM/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

**FancyWM** is a dynamic tiling window manager for Windows 10/11 that brings the productivity of tiling windows to the Windows desktop - **without complex configuration** and without sacrificing the familiar **mouse-and-keyboard interactions** most users expect.

<img align="right" src="https://store-images.s-microsoft.com/image/apps.53415.14517052119257390.d950e654-2004-4878-b902-94902f8f7a45.af24879e-636a-494c-ba1d-6ff7f858630b?background=transparent&w=175&h=175&format=jpg">

Instead of manually arranging overlapping windows, FancyWM automatically organizes your applications into vertical, horizontal and stack panels. Windows flow into the focused panel as they open, resize gracefully when siblings close, and can be rearranged with simple keyboard shortcuts or mouse drag-and-drop. The result is a clutter-free workspace where every application has a deliberate place.

## Why Tiling on Windows?

Tiling fundamentally changes how you interact with a desktop. New windows stop overlapping; instead, they automatically claim their space alongside existing windows. Keyboard navigation replaces constant mousing. For developers, researchers, and power users managing multiple applications simultaneously - whether coding with reference docs, multi-tasking across projects, or orchestrating complex workflows - tiling reclaims significant time and cognitive load.

Windows offers Snap Layouts and Virtual Desktops, which are useful, but lack the control and dynamic behavior that tiling provides. FancyWM fills that gap: it's a practical entry point for users curious about tiling without abandoning the Windows ecosystem.

## Core Features

### Panels: Horizontal, Vertical, and Stacked

FancyWM organizes windows into three panel types:

These panels can be **nested arbitrarily** - a horizontal panel containing vertical sub-panels, with stack panels embedded inside them. Build custom layouts by combining primitives, not by editing configuration files.

### No Configuration Needed

Forget predefined layout templates and complex configuration. FancyWM creates layouts as you work. The available keybingins are shown after you press **[⇧ Shift] + [⊞ Win]**.

1. Open your first window - it automatically fills the workspace.
2. Open a second window - FancyWM creates a horizontal split; both windows now share the space.
3. Create a vertical panel with `[⇧ Shift]` + `[⊞ Win]`, then `[V]` - the focused window moves into a new vertical panel.
4. Create a stack panel with `[⇧ Shift]` + `[⊞ Win]`, then `[S]` - windows can now overlap as tabs.
5. Drag windows between panels, or embed panels into other panels, refining the layout in real-time.

### Hybrid Input: Keyboard and Mouse

FancyWM respects that Windows users value both input methods:

This balances power-user efficiency with the approachability expected on Windows.

### Layout Constraints

Applications declare minimum and maximum window sizes via the Windows API (`WM_GETMINMAXINFO`). If an application refuses to resize smaller than its declared minimum, FancyWM respects that constraint - windows never shrink below their declared minimums. Dialogs, tooltips, and transient windows are automatically floated (removed from tiling) so they don't interfere with layouts. If a new window would cause your columns to shrink below the minimum size, that window will be automatically floated too.

The consequence: **No overlapping tiles, no broken layouts** - the tiler adapts to application constraints rather than forcing applications into unsuitable sizes.

### Native Virtual Desktops

FancyWM integrates with Windows' native Virtual Desktops. Jump to desktop 1, 2, or 3 with `[⇧ Shift]` + `[⊞ Win]`, then `[1]`, `[2]`, `[3]`, or move a window to a different desktop with `[⇧ Shift]` + `[⊞ Win]`, `[⇧ Shift]` + `[1]`, `[2]`, `[3]`. Panel layouts can be per-monitor or global - your choice. This fits multi-project workflows: "Desktop 1 for coding, Desktop 2 for research, Desktop 3 for meetings," each with its own panel arrangement.

### Low Resource Footprint

FancyWM runs as a lightweight userland process, not a system service. Typical CPU usage is **<1%** in idle, with optional animation toggles to reduce power consumption on laptops. The C# implementation with .NET runtime is efficient enough for background window management without noticeable system impact.

### Additional Controls

☑ Toggle tiling on/off entirely with `[⇧ Shift]` + `[⊞ Win]`, then `[F11]` to briefly use floating windows

☑ Maximize and restore individual windows without breaking the tiling layout

☑ Auto-collapse single-window panels to reduce visual clutter

☑ Disable animations for battery-conscious workflows

☑ Window focus highlighting (brief blink) for visibility

☑ Customizable keybindings - rebind everything to match your habits

☑ Remap the activation hotkey to `[Ctrl]`+`[Win]` or `[Alt]`+`[Win]` if `[Shift]`+`[Win]` conflicts with other software

## Design Philosophy

FancyWM was built with specific criteria in mind, many of which aren't implemented together elsewhere:

The result is a tiler that feels like a natural extension of Windows, not a Linux port.

## Two-Pass Layout Algorithm

Behind the scenes, FancyWM uses a two-pass algorithm to calculate window positions:

**Pass 1: Constraint Collection**
Query each window's minimum and maximum sizes (via `WM_GETMINMAXINFO`). Identify all panels in the hierarchy and their constraints.

**Pass 2: Position Calculation**
Given the panel tree and constraints, calculate pixel positions for each window such that no window is forced below its minimum. If insufficient space exists for all windows at their minimum sizes, FancyWM gracefully degrades - respecting minimums and potentially pushing windows beyond the visible workspace boundary rather than overlapping them.

The consequence: **Layouts never break due to application constraints.** Each window gets equal space; closing a window simply redistributes that space equally among remaining windows.

Binary Space Partitioning (BSP) divides space recursively into two regions with each split, which can create inefficient layouts or require manual rebalancing. FancyWM's equal-space approach is simpler to reason about and automatically adapts to changing window counts.

## [Downloads](https://github.com/FancyWM/fancywm/releases)

Pre-built binaries can be downloaded from [Releases](https://github.com/FancyWM/fancywm/releases).

These are built by an automated GitHub Action and you can see all of the [build steps](https://github.com/FancyWM/fancywm/blob/main/.github/workflows/dotnet-desktop.yml) and [previous runs](https://github.com/FancyWM/fancywm/actions/workflows/dotnet-desktop.yml).

### Install via winget (Recommended)
```powershell
winget install fancywm
```

### Install from the Microsoft Store

<a href='//www.microsoft.com/store/apps/9p1741lkhqs9?cid=storebadge&ocid=badge'><img src='https://developer.microsoft.com/store/badges/images/English_get-it-from-MS.png' alt='English badge' width="138" height="50"/></a>

### Install .msixbundle (Not Recommended)
You can test the Microsoft Store packages by installing them using PowerShell.

#### PowerShell (as Administrator)
```powershell
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
