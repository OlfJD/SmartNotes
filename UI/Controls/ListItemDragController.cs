using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace SmartNotes.UI.Controls;

/// <summary>
/// Physical drag-to-reorder for the rows of an ItemsControl (checklist tasks, copy items).
///
/// The grabbed row is lifted into a small borderless, top-most "ghost" window, so it can follow the
/// cursor anywhere on screen, outside the note too. It trails the cursor on a stiff spring and tilts
/// with its sideways speed. The other rows slide out of the way (FLIP animation) while the row's own
/// slot stays open as a gap. On release the ghost springs back into that slot; released outside the
/// note, the reorder is cancelled and it flies home to where it came from.
///
/// Start a drag from the six-dot grip, from the row's background, or from its
/// text once the mouse clearly moves up / down (sideways movement in text stays a text selection).
/// </summary>
public sealed class ListItemDragController<T> where T : class
{

    private enum State { Idle, Pending, Dragging, Snapping }

    private readonly ItemsControl _list;
    private readonly ScrollViewer? _scroll;
    private readonly Func<ObservableCollection<T>> _items;
    private readonly Func<bool> _canDrag;
    private readonly Action _onReordered;

    private State _state = State.Idle;

    // Pending (pressed, not yet moved far enough)
    private T? _pendingItem;
    private Point _pendingStart;
    private bool _pendingAnyDirection;

    // Active drag / snap-back
    private T? _item;
    private int _originalIndex;
    private Vector _grabOffset;       // cursor - row top-left, physical px
    private Vector _padPx;            // ghost window padding (shadow / tilt room), physical px
    private DragGhostWindow? _ghost;
    private Vector _pos, _vel;        // ghost row top-left, physical px / px per s
    private double _angle, _scale = 1, _opacity = 1;
    private bool _outside;
    private TimeSpan _lastFrame;
    private DateTime _snapStarted;
    private bool _releasingCapture;

    public ListItemDragController(ItemsControl list, ScrollViewer? scroll, Func<ObservableCollection<T>> items, Func<bool> canDrag, Action onReordered)
    {
        _list = list;
        _scroll = scroll;
        _items = items;
        _canDrag = canDrag;
        _onReordered = onReordered;

        _list.PreviewMouseLeftButtonDown += OnPreviewDown;
        _list.PreviewMouseMove += OnPreviewMove;
        _list.PreviewMouseLeftButtonUp += OnPreviewUp;
        _list.LostMouseCapture += OnLostCapture;
        _list.IsVisibleChanged += (_, _) => { if (!_list.IsVisible) Finish(); };
        _list.Unloaded += (_, _) => Finish();
    }

    public bool IsBusy => _state is State.Dragging or State.Snapping;

    // ------------------------------------------------------------------ input

    private void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (_state == State.Snapping) Finish(); // grabbing again mid-flight: land the last one first
        _state = State.Idle;
        _pendingItem = null;
        if (!_canDrag() || e.OriginalSource is not DependencyObject src) return;

        if (ItemsControl.ContainerFromElement(_list, src) is not FrameworkElement container) return;
        if (_list.ItemContainerGenerator.ItemFromContainer(container) is not T item) return;

        bool onText = false; // the grip and the row background have no text box
        for (DependencyObject? d = src; d != null && d != container; d = Parent(d))
        {
            if (d is ButtonBase) return; // delete / copy buttons, the checkbox
            if (d is TextBoxBase) onText = true;
        }

        _pendingItem = item;
        _pendingStart = e.GetPosition(_list);
        _pendingAnyDirection = !onText;
        _state = State.Pending;
        if (!onText)
        {
            // Grip / row background: hold the mouse so a quick flick off the row still becomes a drag
            e.Handled = true; // no text focus / window drag
            Mouse.Capture(_list, CaptureMode.SubTree);
        }
    }

    private void OnPreviewMove(object sender, MouseEventArgs e)
    {
        if (_state != State.Pending || _pendingItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _state = State.Idle; _pendingItem = null; ReleaseCapture(); return; }

        Vector d = e.GetPosition(_list) - _pendingStart;
        bool go = _pendingAnyDirection
            ? d.Length >= 4
            : Math.Abs(d.Y) >= 6 && Math.Abs(d.Y) >= Math.Abs(d.X);
        if (!go) return;

        var item = _pendingItem;
        _pendingItem = null;
        StartDrag(item);
    }

    private void OnPreviewUp(object sender, MouseButtonEventArgs e)
    {
        if (_state == State.Pending) { _state = State.Idle; _pendingItem = null; ReleaseCapture(); return; }
        if (_state != State.Dragging) return;
        Drop();
        e.Handled = true;
    }

    // LostMouseCapture bubbles: a text box handing the mouse to the list (drag start) is not the end of a drag.
    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (_releasingCapture || !ReferenceEquals(e.OriginalSource, _list)) return;
        if (_state == State.Dragging) Drop();
    }

    // ------------------------------------------------------------------ drag

    private void StartDrag(T item)
    {
        var items = _items();
        int index = items.IndexOf(item);
        if (index < 0 || Container(item) is not FrameworkElement container || Face(container) is not FrameworkElement row
            || row.ActualWidth < 1 || row.ActualHeight < 1)
        {
            _state = State.Idle;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(row);
        Point rowScreen = row.PointToScreen(new Point(0, 0));
        Point cursor = CursorPx();

        _item = item;
        _originalIndex = index;
        _grabOffset = cursor - rowScreen;
        _padPx = new Vector(DragGhostWindow.Pad * dpi.DpiScaleX, DragGhostWindow.Pad * dpi.DpiScaleY);
        _pos = (Vector)rowScreen;
        _vel = new Vector();
        _angle = 0;
        _scale = 1;
        _opacity = 1;
        _outside = false;

        var snap = Snapshot(container, row, dpi);
        _ghost = new DragGhostWindow(snap, row.ActualWidth, row.ActualHeight)
        {
            Left = (rowScreen.X - _padPx.X) / dpi.DpiScaleX,
            Top = (rowScreen.Y - _padPx.Y) / dpi.DpiScaleY,
        };
        _ghost.Show();
        _ghost.MoveTo(_pos - _padPx);
        container.Opacity = 0; // its slot stays open as a gap

        _state = State.Dragging;
        Mouse.Capture(_list, CaptureMode.SubTree); // takes the mouse from a text box, ending its selection
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void Drop()
    {
        if (_state != State.Dragging || _item == null) return;
        _state = State.Snapping;
        _snapStarted = DateTime.UtcNow;
        ReleaseCapture();

        var items = _items();
        int now = items.IndexOf(_item);
        if (_outside && now >= 0 && now != _originalIndex) MoveAnimated(now, _originalIndex);
        now = items.IndexOf(_item);
        if (Container(_item) is FrameworkElement row) row.BringIntoView();
        if (now >= 0 && now != _originalIndex) _onReordered();
    }

    /// <summary>Ends everything at once: ghost gone, row visible again.</summary>
    private void Finish()
    {
        if (_state == State.Pending) { _state = State.Idle; _pendingItem = null; ReleaseCapture(); }
        if (_state is not (State.Dragging or State.Snapping)) return;
        bool wasDragging = _state == State.Dragging;
        _state = State.Idle;
        CompositionTarget.Rendering -= OnFrame;
        ReleaseCapture();
        if (_item != null && Container(_item) is UIElement row) row.Opacity = 1;
        _ghost?.Close();
        _ghost = null;
        if (wasDragging && _item != null && _items().IndexOf(_item) is int i && i >= 0 && i != _originalIndex) _onReordered();
        _item = null;
    }

    private void ReleaseCapture()
    {
        if (Mouse.Captured != _list) return;
        _releasingCapture = true;
        try { Mouse.Capture(null); } finally { _releasingCapture = false; }
    }

    // ------------------------------------------------------------------ per frame

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_ghost == null || _item == null) { Finish(); return; }

        TimeSpan t = e is RenderingEventArgs r ? r.RenderingTime : TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
        if (t == _lastFrame) return; // Rendering can fire more than once per frame
        double dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : Math.Clamp((t - _lastFrame).TotalSeconds, 0.001, 0.05);
        _lastFrame = t;

        Vector target;
        double stiffness, damping;
        if (_state == State.Dragging)
        {
            if (!LeftButtonDown()) { Drop(); return; }
            Point cursor = CursorPx();
            target = (Vector)cursor - _grabOffset;
            UpdateOutside(cursor);
            if (!_outside) { UpdateTargetIndex(cursor); AutoScroll(cursor); }
            stiffness = 1800; damping = Damping(stiffness, 0.85);
            _scale = Approach(_scale, _outside ? 0.96 : 1.04, dt, 14);
            _opacity = Approach(_opacity, _outside ? 0.82 : 1.0, dt, 14);
        }
        else // snapping home
        {
            if (Container(_item) is not FrameworkElement c || Face(c) is not FrameworkElement slot || !slot.IsVisible) { Finish(); return; }
            target = (Vector)slot.PointToScreen(new Point(0, 0));
            stiffness = 340; damping = Damping(stiffness, 0.5); // under-damped: a little bounce as it lands
            _scale = Approach(_scale, 1.0, dt, 10);
            _opacity = Approach(_opacity, 1.0, dt, 10);
            bool settled = (target - _pos).Length < 0.6 && _vel.Length < 30 && Math.Abs(_scale - 1) < 0.004;
            if (settled || (DateTime.UtcNow - _snapStarted).TotalSeconds > 1.5) { Finish(); return; }
        }

        // Spring, a few sub-steps for stability
        const int steps = 4;
        double h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector acc = stiffness * (target - _pos) - damping * _vel;
            _vel += acc * h;
            _pos += _vel * h;
        }

        // Tilt with sideways speed, like a card held at the grab point
        double tiltTarget = Math.Clamp(_vel.X * 0.0045, -6, 6);
        _angle = Approach(_angle, tiltTarget, dt, 16);

        _ghost.Apply(_angle, _scale, _opacity);
        _ghost.MoveTo(_pos - _padPx);
    }

    private static double Damping(double k, double zeta) => 2 * zeta * Math.Sqrt(k);
    private static double Approach(double v, double to, double dt, double rate) => v + (to - v) * Math.Min(1, dt * rate);

    /// <summary>Outside = the cursor left the note window (plus a small margin).</summary>
    private void UpdateOutside(Point cursor)
    {
        if (PresentationSource.FromVisual(_list) is not HwndSource src || !GetWindowRect(src.Handle, out DragNative.RECT wr)) return;
        double m = 16 * VisualTreeHelper.GetDpi(_list).DpiScaleX;
        bool outside = cursor.X < wr.Left - m || cursor.X > wr.Right + m || cursor.Y < wr.Top - m || cursor.Y > wr.Bottom + m;
        if (outside == _outside) return;
        _outside = outside;
        if (_outside && _item != null)
        {
            // Out of the note: the rows close back up into the original order
            int now = _items().IndexOf(_item);
            if (now >= 0 && now != _originalIndex) MoveAnimated(now, _originalIndex);
        }
    }

    /// <summary>Target slot = how many of the other rows have their middle above the cursor
    /// (layout positions, so rows still sliding don't make the target flicker).</summary>
    private void UpdateTargetIndex(Point cursor)
    {
        var items = _items();
        if (_item == null) return;
        int from = items.IndexOf(_item);
        if (from < 0 || Container(_item) is not FrameworkElement self || VisualTreeHelper.GetParent(self) is not Panel panel) return;

        double y = panel.PointFromScreen(cursor).Y;
        int to = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (i == from || Container(items[i]) is not FrameworkElement row) continue;
            double mid = VisualTreeHelper.GetOffset(row).Y + row.ActualHeight / 2;
            if (y > mid) to++;
        }
        if (to != from) MoveAnimated(from, to);
    }

    private void AutoScroll(Point cursor)
    {
        if (_scroll == null || _scroll.ScrollableHeight <= 0) return;
        double y = _scroll.PointFromScreen(cursor).Y;
        const double edge = 22;
        if (y < edge) _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset - Math.Min(14, (edge - y) * 0.5 + 2));
        else if (y > _scroll.ActualHeight - edge) _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset + Math.Min(14, (y - (_scroll.ActualHeight - edge)) * 0.5 + 2));
    }

    /// <summary>Moves the item and lets the displaced rows glide to their new places.</summary>
    private void MoveAnimated(int from, int to)
    {
        var items = _items();
        var before = new Dictionary<T, double>();
        foreach (var it in items)
            if (Container(it) is FrameworkElement c)
                before[it] = VisualTreeHelper.GetOffset(c).Y + ((c.RenderTransform as TranslateTransform)?.Y ?? 0);

        items.Move(from, to);
        _list.UpdateLayout();

        foreach (var it in items)
        {
            if (Container(it) is not FrameworkElement c) continue;
            if (ReferenceEquals(it, _item)) { c.Opacity = 0; c.RenderTransform = Transform.Identity; continue; }
            if (!before.TryGetValue(it, out double oldY)) continue;
            double delta = oldY - VisualTreeHelper.GetOffset(c).Y;
            if (Math.Abs(delta) < 0.5) continue;
            var tt = new TranslateTransform(0, delta);
            c.RenderTransform = tt;
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(delta, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }

    // ------------------------------------------------------------------ helpers

    private FrameworkElement? Container(T item) => _list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;

    private static DependencyObject? Parent(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    /// <summary>The visible card of a row: the container's first child (the row's margin stays out of the ghost).</summary>
    private static FrameworkElement Face(FrameworkElement container) =>
        VisualTreeHelper.GetChildrenCount(container) > 0 && VisualTreeHelper.GetChild(container, 0) is FrameworkElement f ? f : container;

    /// <summary>Picture of the row's card at screen resolution. Renders the whole items panel (transparent
    /// background, so cut / rounded corners stay see-through) and crops the card out of it. A VisualBrush
    /// snapshot only worked for the first drag of a session and came out blank afterwards.</summary>
    private static BitmapSource Snapshot(FrameworkElement container, FrameworkElement face, DpiScale dpi)
    {
        double sx = dpi.DpiScaleX, sy = dpi.DpiScaleY;
        if (VisualTreeHelper.GetParent(container) is not FrameworkElement panel) panel = container;

        // RenderTargetBitmap draws a visual at its offset inside its parent, so add that in
        Rect r = face.TransformToAncestor(panel).TransformBounds(new Rect(0, 0, face.ActualWidth, face.ActualHeight));
        Vector off = VisualTreeHelper.GetOffset(panel);
        r.Offset(off);

        int w = Math.Max(1, (int)Math.Ceiling((Math.Max(r.Right, off.X + panel.ActualWidth)) * sx));
        int h = Math.Max(1, (int)Math.Ceiling((Math.Max(r.Bottom, off.Y + panel.ActualHeight)) * sy));
        var full = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        full.Render(panel);

        int x0 = Math.Clamp((int)Math.Floor(r.X * sx), 0, w - 1), y0 = Math.Clamp((int)Math.Floor(r.Y * sy), 0, h - 1);
        int cw = Math.Clamp((int)Math.Round(r.Width * sx), 1, w - x0), ch = Math.Clamp((int)Math.Round(r.Height * sy), 1, h - y0);
        var crop = new CroppedBitmap(full, new Int32Rect(x0, y0, cw, ch));
        crop.Freeze();
        return crop;
    }

    private static Point CursorPx() => DragNative.GetCursorPos(out DragNative.POINT p) ? new Point(p.X, p.Y) : new Point();

    private static bool LeftButtonDown()
    {
        int vk = DragNative.GetSystemMetrics(DragNative.SM_SWAPBUTTON) != 0 ? DragNative.VK_RBUTTON : DragNative.VK_LBUTTON;
        return (DragNative.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private static bool GetWindowRect(IntPtr hwnd, out DragNative.RECT r) => DragNative.GetWindowRect(hwnd, out r);
}

internal static class DragNative
{
    public const int SM_SWAPBUTTON = 23, VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}

/// <summary>Borderless, click-through, never-activated top-most window that carries the lifted row.</summary>
internal sealed class DragGhostWindow : Window
{
    /// <summary>Room around the row for its shadow, tilt and lift (DIPs).</summary>
    public const double Pad = 34;

    private readonly RotateTransform _rotate = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Border _host;
    private IntPtr _hwnd;

    public DragGhostWindow(ImageSource snapshot, double width, double height)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = width + 2 * Pad;
        Height = height + 2 * Pad;

        var image = new System.Windows.Controls.Image { Source = snapshot, Width = width, Height = height, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_rotate);
        _host = new Border
        {
            Child = image,
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Pad),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = group,
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 9, Direction = 270, Opacity = 0.5, Color = Colors.Black },
        };
        Content = _host;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void Apply(double angle, double scale, double opacity)
    {
        _rotate.Angle = angle;
        _scale.ScaleX = _scale.ScaleY = scale;
        _host.Opacity = opacity;
    }

    /// <summary>Places the window's top-left at a physical screen position.</summary>
    public void MoveTo(Vector px)
    {
        if (_hwnd == IntPtr.Zero) return;
        SetWindowPos(_hwnd, HWND_TOPMOST, (int)Math.Round(px.X), (int)Math.Round(px.Y), 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
