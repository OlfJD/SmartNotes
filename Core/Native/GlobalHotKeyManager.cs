using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Interop;

namespace SmartNotes.Core.Native;

public class GlobalHotKeyManager : IDisposable
{
    private readonly HwndSource? _source;
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 9000;

    public GlobalHotKeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);
    }

    public int Register(uint modifiers, Keys key, Action callback)
    {
        int id = ++_currentId;
        bool registered = Win32Api.RegisterHotKey(_hwnd, id, modifiers | Win32Api.MOD_NOREPEAT, (uint)key);
        if (registered)
        {
            _hotkeyActions[id] = callback;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            Win32Api.UnregisterHotKey(_hwnd, id);
            _hotkeyActions.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys)
        {
            Win32Api.UnregisterHotKey(_hwnd, id);
        }
        _hotkeyActions.Clear();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
        GC.SuppressFinalize(this);
    }
}
