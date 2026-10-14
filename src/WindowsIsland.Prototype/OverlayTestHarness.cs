using System.Runtime.InteropServices;
using WindowsIsland.Core.Media;
using WindowsIsland.Core.Native;
using WindowsIsland.Core.Positioning;

namespace WindowsIsland.Prototype;

public static class OverlayTestHarness
{
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_LBUTTONUP = 0x0202;

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);
    private static WndProc? _wndProcDelegate;
    private static WindowStyleService? _styleService;
    private static IMediaSessionService? _mediaService;
    private static nint _hWnd;
    private static string _statusText = "[|<] [Play] [>|] [X] | WIMC Ready";

    public static void Run(IMediaSessionService media, IslandDockMode dockMode)
    {
        _mediaService = media;
        _wndProcDelegate = HostWndProc;

        string className = "WIMC_NonActivating_Overlay";
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandleW(null),
            hbrBackground = CreateSolidBrush(0x00282828),
            lpszClassName = className
        };
        RegisterClassExW(ref wc);

        var bounds = TaskbarDetector.CalculateIslandBounds(440, 44, dockMode);
        _hWnd = CreateWindowExW(
            WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            className, "WIMC Overlay", WS_POPUP | WS_VISIBLE,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            0, 0, wc.hInstance, 0);

        _styleService = new WindowStyleService();
        _styleService.AttachNonActivatingOverlay(_hWnd);

        EventHandler<MediaTrackInfo?> handler = (_, info) =>
        {
            _statusText = info is null
                ? "[|<] [Play] [>|] [X] | No Active Media"
                : $"[|<] [{(info.IsPlaying ? "Pause" : "Play")}] [>|] [X] | {info.Title} - {info.Artist}";
            if (_hWnd != 0) InvalidateRect(_hWnd, 0, true);
        };
        media.MediaStateChanged += handler;
        _ = media.RefreshCurrentSessionAsync();

        Console.WriteLine("[TV-01 Test] Open Notepad, type 'abcdef', click overlay buttons, then type 'ghijk'.");
        while (GetMessageW(out MSG msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        media.MediaStateChanged -= handler;
    }

    private static nint HostWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_LBUTTONUP && _mediaService is not null)
        {
            int x = (short)(lParam & 0xFFFF);
            nint fgBefore = GetForegroundWindow();
            string action = "None";

            if (x >= 8 && x < 48) { action = "Prev"; _ = _mediaService.SkipPreviousAsync(); }
            else if (x >= 50 && x < 105) { action = "PlayPause"; _ = _mediaService.TogglePlayPauseAsync(); }
            else if (x >= 108 && x < 148) { action = "Next"; _ = _mediaService.SkipNextAsync(); }
            else if (x >= 150 && x < 185) { action = "Close"; DestroyWindow(hWnd); }

            _ = Task.Delay(50).ContinueWith(_ =>
            {
                nint fgAfter = GetForegroundWindow();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Click={action} (X={x}) | FG_Before=0x{fgBefore:X} FG_After=0x{fgAfter:X} | FocusKept={fgBefore == fgAfter}");
            });
            return 0;
        }
        if (msg == WM_PAINT)
        {
            nint hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);
            SetBkMode(hdc, 1);
            SetTextColor(hdc, 0x00FFFFFF);
            var rc = new RECT { Left = 10, Top = 12, Right = 800, Bottom = 40 };
            DrawTextW(hdc, _statusText, -1, ref rc, 0x00000020);
            EndPaint(hWnd, ref ps);
            return 0;
        }
        if (msg == WM_DESTROY)
        {
            _styleService?.Dispose();
            _styleService = null;
            _hWnd = 0;
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public nint hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore, fIncUpdate;
        private long _r1, _r2, _r3, _r4;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW { public uint cbSize, style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra; public nint hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName, lpszClassName; public nint hIconSm; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int pt_x, pt_y; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern nint GetModuleHandleW(string? lpModuleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InvalidateRect(nint hWnd, nint lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int DrawTextW(nint hdc, string lpchText, int cchText, ref RECT lprc, uint format);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint min, uint max);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern nint CreateSolidBrush(uint crColor);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern int SetBkMode(nint hdc, int mode);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern uint SetTextColor(nint hdc, uint color);
}
