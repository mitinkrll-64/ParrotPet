using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ParrotPet.Services;

/// <summary>Глобальный хоткей Ctrl+Shift+D через RegisterHotKey (невидимое message-only окно).</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModNoRepeat = 0x4000;
    private const int HotkeyId = 0x5050;
    private const uint KeyD = 0x44;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>false — комбинация занята другим приложением.</summary>
    public bool IsRegistered { get; }

    public HotkeyService(Action onPressed)
    {
        _source = new HwndSource(new HwndSourceParameters("ParrotPetHotkeys") { ParentWindow = HwndMessage });
        _source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                onPressed();
            }
            return IntPtr.Zero;
        });
        IsRegistered = RegisterHotKey(_source.Handle, HotkeyId, ModControl | ModShift | ModNoRepeat, KeyD);
    }

    public void Dispose()
    {
        if (IsRegistered) UnregisterHotKey(_source.Handle, HotkeyId);
        _source.Dispose();
    }
}