using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ParrotPet.Services;

/// <summary>Окно другой программы, по которому может лазить попугай (границы в DIP).</summary>
public record WindowSurface(IntPtr Handle, Rect Bounds);

/// <summary>
/// Находит видимые окна рабочего стола: на их верхнюю кромку попугай садится,
/// за боковую — цепляется. Список в порядке z (сверху вниз), обновляется ~4 раза в секунду.
/// </summary>
public class WindowSurfaceService
{
    private const double MinWidth = 160;   // слишком мелкие окна пропускаем
    private const double MinHeight = 90;
    private const long CacheMs = 250;

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int DwmwaCloaked = 14;
    private const int DwmwaExtendedFrameBounds = 9;

    private readonly int _ownProcessId = Environment.ProcessId;
    private List<WindowSurface> _cache = new();
    private long _cacheStamp = -100000;

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out NativeRect rect, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);

    /// <summary>Подходящие окна в порядке z: верхнее первым.</summary>
    public IReadOnlyList<WindowSurface> GetWindows()
    {
        long now = Environment.TickCount64;
        if (now - _cacheStamp < CacheMs) return _cache;

        var list = new List<WindowSurface>();
        double k = GetDpiForSystem() / 96.0;
        EnumWindows((hwnd, _) =>
        {
            if (TryGetBounds(hwnd, k, out var r, strict: true)) list.Add(new WindowSurface(hwnd, r));
            return true;
        }, IntPtr.Zero);

        _cache = list;
        _cacheStamp = now;
        return list;
    }

    /// <summary>Свежие границы конкретного окна; false — окно закрыли, свернули или оно пропало.</summary>
    public bool TryGetBounds(IntPtr hwnd, out Rect bounds) =>
        TryGetBounds(hwnd, GetDpiForSystem() / 96.0, out bounds, strict: false);

    /// <summary>Закрыта ли точка (x, y) окном выше по z, чем окно с индексом index.</summary>
    public bool IsCovered(IReadOnlyList<WindowSurface> windows, int index, double x, double y)
    {
        for (int i = 0; i < index; i++)
        {
            if (windows[i].Bounds.Contains(x, y)) return true;
        }
        return false;
    }

    private bool TryGetBounds(IntPtr hwnd, double k, out Rect bounds, bool strict)
    {
        bounds = Rect.Empty;
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
        if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out NativeRect nr, Marshal.SizeOf<NativeRect>()) != 0) return false;

        bounds = new Rect(nr.Left / k, nr.Top / k, (nr.Right - nr.Left) / k, (nr.Bottom - nr.Top) / k);

        if (!strict) return !IsZoomed(hwnd);

        // Дальше — фильтры для первичного поиска, чтобы не лазить по служебным окнам
        if (bounds.Width < MinWidth || bounds.Height < MinHeight) return false;
        if (IsZoomed(hwnd)) return false; // у развёрнутого окна кромки совпадают с краями экрана
        if (GetWindowTextLength(hwnd) == 0) return false;
        if ((GetWindowLong(hwnd, GwlExStyle) & WsExToolWindow) != 0) return false;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownProcessId) return false; // собственные окна (попугай, зерно, настройки)

        var cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        string name = cls.ToString();
        return name is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }
}
