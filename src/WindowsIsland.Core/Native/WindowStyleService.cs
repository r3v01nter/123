using System.Runtime.InteropServices;

namespace WindowsIsland.Core.Native;

public sealed class WindowStyleService : IDisposable
{
    private const int GWL_EXSTYLE = -20;
    private static readonly nint WS_EX_TOPMOST = 0x00000008;
    private static readonly nint WS_EX_TOOLWINDOW = 0x00000080;
    private static readonly nint WS_EX_NOACTIVATE = 0x08000000;

    private const uint WM_MOUSEACTIVATE = 0x0021;
    private const uint WM_NCDESTROY = 0x0082;
    private static readonly nint MA_NOACTIVATE = 3;

    private static readonly nint HWND_TOPMOST = -1;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const nuint SUBCLASS_ID = 0x57494D43; // "WIMC"

    private delegate nint SubclassProc(
        nint hWnd,
        uint uMsg,
        nint wParam,
        nint lParam,
        nuint uIdSubclass,
        nint dwRefData);

    // Strong reference prevents the delegate from being collected while native code can call it.
    private readonly SubclassProc _subclassDelegate;
    private nint _hWnd;
    private bool _isSubclassed;
    private bool _disposed;

    public WindowStyleService()
    {
        _subclassDelegate = WndProcSubclass;
    }

    public bool AttachNonActivatingOverlay(nint hWnd)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (hWnd == 0 || !IsWindow(hWnd))
            return false;

        if (_isSubclassed && _hWnd == hWnd)
            return true;

        DetachSubclass();

        Marshal.SetLastPInvokeError(0);
        nint originalExStyle = GetWindowLongPtrW(hWnd, GWL_EXSTYLE);
        if (originalExStyle == 0 && Marshal.GetLastPInvokeError() != 0)
            return false;

        if (!SetWindowSubclass(hWnd, _subclassDelegate, SUBCLASS_ID, 0))
            return false;

        _hWnd = hWnd;
        _isSubclassed = true;

        nint newExStyle = originalExStyle | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        Marshal.SetLastPInvokeError(0);
        nint previousExStyle = SetWindowLongPtrW(hWnd, GWL_EXSTYLE, newExStyle);
        if (previousExStyle == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            DetachSubclass();
            return false;
        }

        if (!SetWindowPos(
                hWnd,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED))
        {
            Marshal.SetLastPInvokeError(0);
            SetWindowLongPtrW(hWnd, GWL_EXSTYLE, originalExStyle);
            DetachSubclass();
            return false;
        }

        return true;
    }

    private nint WndProcSubclass(
        nint hWnd,
        uint uMsg,
        nint wParam,
        nint lParam,
        nuint uIdSubclass,
        nint dwRefData)
    {
        if (uMsg == WM_MOUSEACTIVATE)
        {
            // Prevent activation without eating the mouse message. Click behavior must be verified in a WinUI 3 prototype.
            return MA_NOACTIVATE;
        }

        if (uMsg == WM_NCDESTROY)
        {
            // Remove the subclass before forwarding the final non-client-destroy message.
            DetachSubclass();
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void DetachSubclass()
    {
        if (!_isSubclassed || _hWnd == 0)
            return;

        nint hWnd = _hWnd;
        _isSubclassed = false;
        _hWnd = 0;

        if (IsWindow(hWnd))
            RemoveWindowSubclass(hWnd, _subclassDelegate, SUBCLASS_ID);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DetachSubclass();
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("comctl32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass,
        nint dwRefData);

    [DllImport("comctl32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(
        nint hWnd,
        uint uMsg,
        nint wParam,
        nint lParam);
}
