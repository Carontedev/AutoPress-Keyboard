using System.Text;

namespace AutoPress.Windows;

public sealed record WindowTarget(string Name, string Path)
{
    public override string ToString() => Name;
}

public static class WindowTargets
{
    public static string? ForegroundPath() => PathForWindow(NativeMethods.GetForegroundWindow());

    public static IReadOnlyList<WindowTarget> List()
    {
        var targets = new Dictionary<string, WindowTarget>(StringComparer.OrdinalIgnoreCase);
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window)) return true;
            var title = new StringBuilder(256);
            if (NativeMethods.GetWindowText(window, title, title.Capacity) == 0) return true;
            var path = PathForWindow(window);
            if (path is not null && !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                targets.TryAdd(path, new($"{System.IO.Path.GetFileNameWithoutExtension(path)} · {title}", path));
            return true;
        }, 0);
        return targets.Values.OrderBy(t => t.Name).ToArray();
    }

    private static string? PathForWindow(nint window)
    {
        if (window == 0) return null;
        NativeMethods.GetWindowThreadProcessId(window, out uint id);
        nint process = NativeMethods.OpenProcess(0x1000, false, id); // metadata only
        if (process == 0) return null;
        try
        {
            uint capacity = 32768;
            var path = new StringBuilder((int)capacity);
            return NativeMethods.QueryFullProcessImageName(process, 0, path, ref capacity) ? path.ToString() : null;
        }
        finally { NativeMethods.CloseHandle(process); }
    }
}
