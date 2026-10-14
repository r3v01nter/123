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
    private const uint WM_SETCURSOR = 0x0020;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const nint IDC_ARROW = 32512;

    public const string VersionTag = "v0.001";

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);
    private static WndProc? _wndProcDelegate;
    private static WindowStyleService? _styleService;
    private static IMediaSessionService? _mediaService;
    private static nint _hWnd;
    private static nint _btnBrush;
    private static nint _classBrush;
    private static bool _messageLoopStarted;
    private static string _className = string.Empty;
    private static string _playLabel = "Play";
    private static string _songText = "[v0.001] Ready";

    public static void Run(IMediaSessionService media, IslandDockMode dockMode)
    {
        _mediaService = media;
        _wndProcDelegate = HostWndProc;
        _className = $"WIMC_Overlay_{Guid.NewGuid():N}";

        _btnBrush = CreateSolidBrush(0x00D8D8D8);
        _classBrush = CreateSolidBrush(0x00202020);
        if (_btnBrush == 0 || _classBrush == 0)
        {
            CleanupBrushes();
            Console.Error.WriteLine("[WIMC] Failed to create GDI brushes.");
            return;
        }

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandleW(null),
            hCursor = LoadCursorW(0, IDC_ARROW),
            hbrBackground = _classBrush,
            lpszClassName = _className
        };

        if (RegisterClassExW(ref wc) == 0)
        {
            Console.Error.WriteLine($"[WIMC] RegisterClassExW failed: {Marshal.GetLastPInvokeError()}");
            CleanupBrushes();
            ResetRunState();
            return;
        }

        var bounds = TaskbarDetector.CalculateIslandBounds(500, 44, dockMode);
        _hWnd = CreateWindowExW(
            WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            _className, "WIMC v0.001", WS_POPUP | WS_VISIBLE,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            0, 0, wc.hInstance, 0);

        if (_hWnd == 0)
        {
            Console.Error.WriteLine($"[WIMC] CreateWindowExW failed: {Marshal.GetLastPInvokeError()}");
            UnregisterClassW(_className, wc.hInstance);
            CleanupBrushes();
            ResetRunState();
            return;
        }

        _styleService = new WindowStyleService();
        if (!_styleService.AttachNonActivatingOverlay(_hWnd))
        {
            Console.Error.WriteLine("[WIMC] Failed to attach non-activating window styles.");
            DestroyWindow(_hWnd);
            _styleService.Dispose();
            _styleService = null;
            UnregisterClassW(_className, wc.hInstance);
            CleanupBrushes();
            ResetRunState();
            return;
        }

        EventHandler<MediaTrackInfo?> handler = (_, info) =>
        {
            _playLabel = info is { IsPlaying: true } ? "Pause" : "Play";
            _songText = info is null ? "[v0.001] No Media" : $"[v0.001] {info.Title} - {info.Artist}";
            if (_hWnd != 0) InvalidateRect(_hWnd, 0, true);
        };
        media.MediaStateChanged += handler;
        _ = Task.Run(() => media.RefreshCurrentSessionAsync());

        Console.WriteLine("=== WIMC Overlay [v0.001] Started ===");
        _messageLoopStarted = true;
        int messageResult;
        while ((messageResult = GetMessageW(out MSG msg, 0, 0, 0)) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        _messageLoopStarted = false;
        if (messageResult == -1)
            Console.Error.WriteLine($"[WIMC] GetMessageW failed: {Marshal.GetLastPInvokeError()}");

        media.MediaStateChanged -= handler;
        if (_hWnd != 0)
            DestroyWindow(_hWnd);

        _styleService?.Dispose();
        _styleService = null;
        UnregisterClassW(_className, wc.hInstance);
        CleanupBrushes();
        ResetRunState();
    }

    private static nint HostWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_SETCURSOR) { SetCursor(LoadCursorW(0, IDC_ARROW)); return 1; }
        if (msg == WM_LBUTTONDOWN) return 0;

        if (msg == WM_LBUTTONUP && _mediaService is not null)
        {
            int x = (short)(lParam & 0xFFFF);
            nint fgBefore = GetForegroundWindow();
            if (x >= 166 && x <= 198) { DestroyWindow(hWnd); return 0; }

            string act = "None";
            Func<Task<bool>>? op = null;
            if (x >= 8 && x <= 48) { act = "Prev"; op = _mediaService.SkipPreviousAsync; }
            else if (x >= 54 && x <= 114) { act = "PlayPause"; op = _mediaService.TogglePlayPauseAsync; }
            else if (x >= 120 && x <= 160) { act = "Next"; op = _mediaService.SkipNextAsync; }

            if (op is not null)
            {
                _ = Task.Run(async () =>
                {
                    bool ok = await op();
                    await Task.Delay(50);
                    nint fgAfter = GetForegroundWindow();
                    Console.WriteLine($"[v0.001] Click={act}(X={x}) | Exec={ok} | FocusKept={fgBefore == fgAfter}");
                });
            }
            return 0;
        }

        if (msg == WM_PAINT)
        {
            nint hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);
            SetBkMode(hdc, 1);
            SetTextColor(hdc, 0x00202020);
            DrawBtn(hdc, 8, 6, 48, 38, "|<");
            DrawBtn(hdc, 54, 6, 114, 38, _playLabel);
            DrawBtn(hdc, 120, 6, 160, 38, ">|");
            DrawBtn(hdc, 166, 6, 198, 38, "X");
            var rc = new RECT { Left = 208, Top = 6, Right = 492, Bottom = 38 };
            DrawTextW(hdc, _songText, -1, ref rc, 0x24);
            EndPaint(hWnd, ref ps);
            return 0;
        }

        if (msg == WM_DESTROY)
        {
            _styleService?.Dispose();
            _styleService = null;
            _hWnd = 0;
            CleanupBrushes();
            if (_messageLoopStarted)
                PostQuitMessage(0);
            return 0;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void DrawBtn(nint hdc, int l, int t, int r, int b, string txt)
    {
        var rc = new RECT { Left = l, Top = t, Right = r, Bottom = b };
        if (_btnBrush != 0)
            FillRect(hdc, ref rc, _btnBrush);
        DrawTextW(hdc, txt, -1, ref rc, 0x25);
    }

    private static void CleanupBrushes()
    {
        if (_btnBrush != 0)
        {
            DeleteObject(_btnBrush);
            _btnBrush = 0;
        }

        if (_classBrush != 0)
        {
            DeleteObject(_classBrush);
            _classBrush = 0;
        }
    }

    private static void ResetRunState()
    {
        _hWnd = 0;
        _mediaService = null;
        _wndProcDelegate = null;
        _className = string.Empty;
        _messageLoopStarted = false;
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClassW(string lpClassName, nint hInstance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint LoadCursorW(nint hInstance, nint lpCursorName);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint SetCursor(nint hCursor);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InvalidateRect(nint hWnd, nint lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int DrawTextW(nint hdc, string lpchText, int cchText, ref RECT lprc, uint format);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint min, uint max);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern nint CreateSolidBrush(uint crColor);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint hObject);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int FillRect(nint hDC, ref RECT lprc, nint hbr);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern int SetBkMode(nint hdc, int mode);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern uint SetTextColor(nint hdc, uint color);
}
