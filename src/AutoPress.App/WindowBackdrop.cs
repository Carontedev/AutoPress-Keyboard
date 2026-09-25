using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AutoPress.App;

/// <summary>Documented Windows 11 Desktop Acrylic, with an opaque fallback.</summary>
internal static class WindowBackdrop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);

    public static bool Apply(nint window, bool dark)
    {
        if (window == 0 || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return false;
        int immersive = dark ? 1 : 0;
        DwmSetWindowAttribute(window, 20, ref immersive, sizeof(int));
        int rounded = 2;
        DwmSetWindowAttribute(window, 33, ref rounded, sizeof(int));
        int border = unchecked((int)0xFFFFFFFE);
        DwmSetWindowAttribute(window, 34, ref border, sizeof(int));
        bool transparency = !SystemParameters.HighContrast;
        try
        {
            transparency &= Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1) is not int setting || setting != 0;
        }
        catch (System.Security.SecurityException) { transparency = false; }
        int backdrop = transparency ? 3 : 1;
        if (DwmSetWindowAttribute(window, 38, ref backdrop, sizeof(int)) != 0 || !transparency) return false;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        if (DwmExtendFrameIntoClientArea(window, ref margins) != 0) return false;
        if (HwndSource.FromHwnd(window)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        return true;
    }
}
