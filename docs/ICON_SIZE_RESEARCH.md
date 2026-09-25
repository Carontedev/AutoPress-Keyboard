# Windows taskbar icon sizes

Research date: 2026-09-24. Scope: official Microsoft guidance; no application changes.

## Current Windows 11 guidance

Microsoft documents these rendered sizes, which depend on display scaling:

| Display scale | Taskbar | Title bar / system tray |
| --- | --- | --- |
| 100% | 24 x 24 px | 16 x 16 px |
| 125% | 30 x 30 px | 20 x 20 px |
| 150% | 36 x 36 px | 24 x 24 px |
| 200% | 48 x 48 px | 32 x 32 px |
| 250% | 60 x 60 px | 40 x 40 px |
| 300% | 72 x 72 px | 48 x 48 px |
| 400% | 96 x 96 px | 64 x 64 px |

These are Windows' documented target sizes, not a survey of what most third-party applications ship. A screenshot alone does not establish the user's display scale or exact icon-resource selection.

Microsoft recommends, at minimum, 16, 24, 32, 48, and 256 px square assets. Windows looks for an exact size, otherwise the next larger size to downscale. Providing 30 and 36 px variants additionally targets the common 125% and 150% taskbar sizes directly. A 512 px source image is an authoring asset, not the on-screen taskbar size.

Source: [Construct your Windows App's Icon](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction), sections "Icon sizes" and "Icon scaling". Retrieved live on the research date.

## WPF and older documentation caveat

WPF distinguishes the assembly icon (`ApplicationIcon`) from the window icon (`Window.Icon`). The window icon is used for the title bar, taskbar button, and Alt+Tab entry. ICO files can contain multiple image sizes, allowing selection of a suitable representation.

The WPF API remarks also contain older generic examples describing 16 px taskbar and 32 px Alt+Tab icons. Do not mistake those examples for the current Windows 11 shell size table above.

Source: [Window.Icon property](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.icon?view=windowsdesktop-10.0), "Remarks".

The older Win32 icon-design page explicitly says it was created for Windows 7 and has not been updated for newer Windows versions. Its 32 px examples therefore should not override the current Windows 11 taskbar table.

Source: [Icons (Design basics)](https://learn.microsoft.com/en-us/windows/win32/uxguide/vis-icons), opening note and "Size requirements".

## Visual footprint

Microsoft initially aligns its icon designs on a 48 x 48 design grid, but that grid is not the taskbar's rendered size. Its current guidance emphasizes a balanced silhouette, legibility at small sizes, few shapes/corners, and avoiding extreme thick/thin shapes. It does not prescribe a universal 85%, 90%, or 94% fill rule for every logo.

Source: [Design guidelines for Windows app icons](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-design), "The grid and rounded corners", "Silhouette", and "Detail".

## Recommendation for AutoPress Keyboard

Keep the user's original high-resolution artwork. Deliver a multi-resolution ICO, at least covering Microsoft's minimum sizes; adding 30 and 36 px versions can avoid resampling at common display scales. Check the actual taskbar at the user's display scale, separately from native 32 px icon-handle tests. A correct 32 px native icon is not proof that Windows 11 renders the taskbar at 32 px.

This recommendation does not require changing the logo design. Resource loading, selected frame, and actual rendered footprint should be verified before attributing a size problem to the artwork.
