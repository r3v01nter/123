using System.Runtime.InteropServices;

namespace WindowsIsland.Core.Positioning;

public enum IslandDockMode
{
    TopCenter = 0,
    TaskbarAdjacent = 1
}

public enum TaskbarEdge : uint
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

public readonly record struct WindowPlacementBounds(
    int X, int Y, int Width, int Height, uint Dpi, bool IsFallback);

public static class TaskbarDetector
{
    private const uint ABM_GETSTATE = 0x00000004;
    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const uint ABS_AUTOHIDE = 0x00000001;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const int MDT_EFFECTIVE_DPI = 0;

    public static WindowPlacementBounds CalculateIslandBounds(
        double logicalWidth,
        double logicalHeight,
        IslandDockMode mode = IslandDockMode.TopCenter,
        double logicalMargin = 10.0)
    {
        if (!double.IsFinite(logicalWidth) || !double.IsFinite(logicalHeight) ||
            !double.IsFinite(logicalMargin) || logicalWidth <= 0 || logicalHeight <= 0 ||
            logicalMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalWidth),
                "Dimensions must be finite and positive; margin must be finite and non-negative.");
        }

        var taskbarData = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
        };

        bool hasTaskbar = SHAppBarMessage(ABM_GETTASKBARPOS, ref taskbarData) != 0 &&
                          taskbarData.rc.Width > 0 &&
                          taskbarData.rc.Height > 0;

        // Keep state query separate: do not rely on ABM_GETSTATE preserving the position structure.
        var stateData = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
        };
        uint state = (uint)SHAppBarMessage(ABM_GETSTATE, ref stateData);
        bool isAutoHide = (state & ABS_AUTOHIDE) != 0;

        nint hMonitor = hasTaskbar
            ? MonitorFromRect(ref taskbarData.rc, MONITOR_DEFAULTTOPRIMARY)
            : MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY);

        var monitorInfo = new MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };

        if (hMonitor == 0 ||
            !GetMonitorInfoW(hMonitor, ref monitorInfo) ||
            monitorInfo.rcMonitor.Width <= 0 ||
            monitorInfo.rcMonitor.Height <= 0)
        {
            return CreateFallbackBounds(logicalWidth, logicalHeight);
        }

        uint dpi = GetMonitorDpiSafe(hMonitor);
        double scale = dpi / 96.0;
        int physicalWidth = Math.Max(1, (int)Math.Round(logicalWidth * scale));
        int physicalHeight = Math.Max(1, (int)Math.Round(logicalHeight * scale));
        int margin = (int)Math.Round(logicalMargin * scale);

        RECT work = monitorInfo.rcWork.Width > 0 && monitorInfo.rcWork.Height > 0
            ? monitorInfo.rcWork
            : monitorInfo.rcMonitor;

        bool useFallback = mode == IslandDockMode.TopCenter || !hasTaskbar || isAutoHide;

        int x = work.Left + (work.Width - physicalWidth) / 2;
        int y = work.Top + margin;

        if (!useFallback)
        {
            switch ((TaskbarEdge)taskbarData.uEdge)
            {
                case TaskbarEdge.Bottom:
                    y = taskbarData.rc.Top - physicalHeight - margin;
                    break;
                case TaskbarEdge.Top:
                    y = taskbarData.rc.Bottom + margin;
                    break;
                case TaskbarEdge.Left:
                    x = taskbarData.rc.Right + margin;
                    y = work.Bottom - physicalHeight - margin;
                    break;
                case TaskbarEdge.Right:
                    x = taskbarData.rc.Left - physicalWidth - margin;
                    y = work.Bottom - physicalHeight - margin;
                    break;
                default:
                    useFallback = true;
                    x = work.Left + (work.Width - physicalWidth) / 2;
                    y = work.Top + margin;
                    break;
            }
        }

        // Clamp to the available work area. Negative monitor coordinates remain valid.
        int minX = work.Left + margin;
        int minY = work.Top + margin;
        int maxX = Math.Max(minX, work.Right - physicalWidth - margin);
        int maxY = Math.Max(minY, work.Bottom - physicalHeight - margin);
        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);

        return new WindowPlacementBounds(x, y, physicalWidth, physicalHeight, dpi, useFallback);
    }

    private static WindowPlacementBounds CreateFallbackBounds(double logicalWidth, double logicalHeight)
    {
        int width = Math.Max(1, (int)Math.Round(logicalWidth));
        int height = Math.Max(1, (int)Math.Round(logicalHeight));
        return new WindowPlacementBounds(100, 12, width, height, 96, true);
    }

    private static uint GetMonitorDpiSafe(nint hMonitor)
    {
        if (hMonitor != 0 &&
            GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
            dpiX >= 96)
        {
            return dpiX;
        }

        return 96;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("shell32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nuint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromRect(ref RECT lprc, uint dwFlags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll", ExactSpelling = true)]
    private static extern int GetDpiForMonitor(
        nint hmonitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
