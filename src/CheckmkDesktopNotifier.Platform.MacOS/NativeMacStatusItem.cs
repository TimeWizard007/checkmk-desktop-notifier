using System.Runtime.InteropServices;
using System.Text;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// NSStatusItem with a compact title. Left-click toggles the problem panel;
/// Control/right-click shows Problems / Settings / Open Checkmk / Quit.
/// </summary>
public sealed class NativeMacStatusItem : IMacStatusItem
{
    private readonly IntPtr _statusItem;
    private readonly IntPtr _button;
    private readonly IntPtr _menu;
    private readonly IntPtr _target;
    private readonly GCHandle _selfHandle;
    private bool _disposed;

    public NativeMacStatusItem()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The macOS status item is only available on macOS.");
        }

        ObjC.EnsureAppKit();
        _selfHandle = GCHandle.Alloc(this);
        _target = ObjC.CreateTarget(GCHandle.ToIntPtr(_selfHandle));
        _statusItem = ObjC.CreateStatusItem();
        _button = ObjC.MsgSend(_statusItem, "button");
        ObjC.SetTargetAction(_button, _target, "activate:");
        ObjC.SendActionOnLeftClick(_button);
        _menu = ObjC.CreateMenu(_target);
        TrySetSymbolImage();
        SetTitle("Checkmk");
        SetToolTip("Checkmk Desktop Notifier");
    }

    public event EventHandler? Activated;

    public event EventHandler? OpenProblemsRequested;

    public event EventHandler? OpenSettingsRequested;

    public event EventHandler? OpenCheckmkRequested;

    public event EventHandler? QuitRequested;

    public void SetTitle(string title)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjC.SetTitle(_button, string.IsNullOrWhiteSpace(title) ? "Checkmk" : title);
    }

    public void SetToolTip(string toolTip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjC.SetToolTip(_button, toolTip ?? string.Empty);
    }

    public bool TryGetAnchor(out MacStatusItemAnchor anchor)
    {
        anchor = default;
        if (_disposed)
        {
            return false;
        }

        try
        {
            return ObjC.TryGetButtonFrame(_button, out anchor);
        }
        catch (Exception ex)
        {
            MacNativeCallbackGuard.Report(ex);
            anchor = default;
            return false;
        }
    }

    internal void HandleSelector(string selector)
    {
        switch (selector)
        {
            case "activate:":
                if (ObjC.CurrentEventIsMenuGesture())
                {
                    ObjC.PopUpMenu(_statusItem, _menu);
                    return;
                }

                Activated?.Invoke(this, EventArgs.Empty);
                return;
            case "problems:":
                OpenProblemsRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "settings:":
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "openCheckmk:":
                OpenCheckmkRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "quit:":
                QuitRequested?.Invoke(this, EventArgs.Empty);
                return;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            ObjC.RemoveStatusItem(_statusItem);
        }
        catch (Exception)
        {
        }

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    private void TrySetSymbolImage()
    {
        try
        {
            ObjC.SetSystemSymbol(_button, "bell.fill");
        }
        catch (Exception)
        {
        }
    }
}

internal static class ObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const double VariableStatusItemLength = -1;
    private const nuint LeftMouseUpMask = (nuint)(1uL << 2);
    private const nuint RightMouseUpMask = (nuint)(1uL << 4);
    private const ulong ControlModifier = 1uL << 18;
    private const nint RightMouseUp = 4;
    private const nint RightMouseDown = 3;

    private static readonly ActivateCallback Callback = OnTargetAction;
    private static readonly IntPtr CallbackPtr = Marshal.GetFunctionPointerForDelegate(Callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateCallback(IntPtr self, IntPtr cmd, IntPtr sender);

    public static void EnsureAppKit()
    {
        if (NativeLibrary.Load(AppKit) == IntPtr.Zero)
        {
            throw new InvalidOperationException("AppKit is unavailable.");
        }
    }

    public static IntPtr CreateStatusItem()
    {
        var statusBar = MsgSend(GetClass("NSStatusBar"), "systemStatusBar");
        return MsgSendDouble(statusBar, "statusItemWithLength:", VariableStatusItemLength);
    }

    public static void RemoveStatusItem(IntPtr statusItem)
    {
        if (statusItem == IntPtr.Zero)
        {
            return;
        }

        var statusBar = MsgSend(GetClass("NSStatusBar"), "systemStatusBar");
        MsgSendVoidPtr(statusBar, "removeStatusItem:", statusItem);
    }

    public static IntPtr CreateTarget(IntPtr context)
    {
        var nsObject = GetClass("NSObject");
        var className = "CdnMacStatusTarget" + Guid.NewGuid().ToString("N");
        var cls = objc_allocateClassPair(nsObject, className, 0);
        if (cls == IntPtr.Zero)
        {
            throw new InvalidOperationException("The macOS status item target could not be created.");
        }

        foreach (var selector in new[] { "activate:", "problems:", "settings:", "openCheckmk:", "quit:" })
        {
            if (!class_addMethod(cls, sel_registerName(selector), CallbackPtr, "v@:@"))
            {
                throw new InvalidOperationException("The macOS status item target could not be created.");
            }
        }

        if (!class_addIvar(cls, "cdnContext", (nuint)IntPtr.Size, (byte)Math.Log2(IntPtr.Size), "@"))
        {
            throw new InvalidOperationException("The macOS status item target could not be created.");
        }

        objc_registerClassPair(cls);
        var instance = MsgSend(cls, "new");
        object_setIvar(instance, class_getInstanceVariable(cls, "cdnContext"), context);
        return instance;
    }

    public static IntPtr CreateMenu(IntPtr target)
    {
        var menu = MsgSend(MsgSend(GetClass("NSMenu"), "alloc"), "init");
        AddMenuItem(menu, "Problems", "problems:", target);
        AddMenuItem(menu, "Settings", "settings:", target);
        AddMenuItem(menu, "Open Checkmk", "openCheckmk:", target);
        MsgSendVoid(menu, "addItem:", MsgSend(GetClass("NSMenuItem"), "separatorItem"));
        AddMenuItem(menu, "Quit", "quit:", target);
        return menu;
    }

    public static void SetTargetAction(IntPtr button, IntPtr target, string selector)
    {
        MsgSendVoidPtr(button, "setTarget:", target);
        MsgSendVoidPtr(button, "setAction:", sel_registerName(selector));
    }

    public static void SendActionOnLeftClick(IntPtr button)
    {
        MsgSendUlong(button, "sendActionOn:", LeftMouseUpMask | RightMouseUpMask);
    }

    public static void SetTitle(IntPtr button, string title) =>
        MsgSendVoidPtr(button, "setTitle:", NsString(title));

    public static void SetToolTip(IntPtr button, string toolTip) =>
        MsgSendVoidPtr(button, "setToolTip:", NsString(toolTip));

    public static void SetSystemSymbol(IntPtr button, string symbolName)
    {
        var image = MsgSendPtrPtr(
            GetClass("NSImage"),
            "imageWithSystemSymbolName:accessibilityDescription:",
            NsString(symbolName),
            IntPtr.Zero);
        if (image == IntPtr.Zero)
        {
            return;
        }

        MsgSendVoidBool(image, "setTemplate:", true);
        MsgSendVoidPtr(button, "setImage:", image);
        MsgSendVoidUlong(button, "setImagePosition:", 2);
    }

    public static void PopUpMenu(IntPtr statusItem, IntPtr menu) =>
        MsgSendVoidPtr(statusItem, "popUpStatusItemMenu:", menu);

    public static bool CurrentEventIsMenuGesture()
    {
        var app = MsgSend(GetClass("NSApplication"), "sharedApplication");
        var current = MsgSend(app, "currentEvent");
        if (current == IntPtr.Zero)
        {
            return false;
        }

        var type = MsgSendNint(current, "type");
        if (type is RightMouseUp or RightMouseDown)
        {
            return true;
        }

        var modifiers = MsgSendUlongResult(current, "modifierFlags");
        return (modifiers & ControlModifier) != 0;
    }

    public static bool TryGetButtonFrame(IntPtr button, out MacStatusItemAnchor anchor)
    {
        anchor = default;
        if (!MacStatusItemGeometry.CanQueryButtonFrame)
        {
            return false;
        }

        var window = MsgSend(button, "window");
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var buttonFrame = MsgSendRect(button, "frame");
        var rect = MsgSendRectRect(window, "convertRectToScreen:", buttonFrame);
        anchor = new MacStatusItemAnchor(rect.X, rect.Y, rect.Width, rect.Height);
        return rect.Width > 0 || rect.Height > 0;
    }

    public static IntPtr MsgSend(IntPtr receiver, string selector) =>
        objc_msgSend(receiver, sel_registerName(selector));

    private static void AddMenuItem(IntPtr menu, string title, string selector, IntPtr target)
    {
        var item = MsgSendPtrPtrPtr(
            MsgSend(GetClass("NSMenuItem"), "alloc"),
            "initWithTitle:action:keyEquivalent:",
            NsString(title),
            sel_registerName(selector),
            NsString(string.Empty));
        MsgSendVoidPtr(item, "setTarget:", target);
        MsgSendVoidPtr(menu, "addItem:", item);
    }

    private static void OnTargetAction(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        MacNativeCallbackGuard.Run(() =>
        {
            var ivar = class_getInstanceVariable(object_getClass(self), "cdnContext");
            var context = object_getIvar(self, ivar);
            if (context == IntPtr.Zero)
            {
                return;
            }

            var handle = GCHandle.FromIntPtr(context);
            if (handle.Target is not NativeMacStatusItem item)
            {
                return;
            }

            item.HandleSelector(Marshal.PtrToStringAnsi(sel_getName(cmd)) ?? string.Empty);
        });
    }

    private static IntPtr GetClass(string name)
    {
        var cls = objc_getClass(name);
        if (cls == IntPtr.Zero)
        {
            throw new InvalidOperationException("The macOS status item API is unavailable.");
        }

        return cls;
    }

    private static IntPtr NsString(string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value + "\0");
        var handle = GCHandle.Alloc(utf8, GCHandleType.Pinned);
        try
        {
            return objc_msgSend_IntPtr(
                GetClass("NSString"),
                sel_registerName("stringWithUTF8String:"),
                handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "sel_getName")]
    private static extern IntPtr sel_getName(IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_Double(IntPtr receiver, IntPtr selector, double arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_VoidPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_VoidBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_VoidUlong(IntPtr receiver, IntPtr selector, nuint arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_Nint(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern ulong objc_msgSend_Ulong(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern NsRect objc_msgSend_Rect(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern NsRect objc_msgSend_RectRect(IntPtr receiver, IntPtr selector, NsRect arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_PtrPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_PtrPtrPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg1,
        IntPtr arg2,
        IntPtr arg3);

    [DllImport(LibObjC, EntryPoint = "objc_allocateClassPair")]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, nint extraBytes);

    [DllImport(LibObjC, EntryPoint = "objc_registerClassPair")]
    private static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(LibObjC, EntryPoint = "class_addMethod")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool class_addMethod(IntPtr cls, IntPtr selector, IntPtr impl, string types);

    [DllImport(LibObjC, EntryPoint = "class_addIvar")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool class_addIvar(IntPtr cls, string name, nuint size, byte alignment, string types);

    [DllImport(LibObjC, EntryPoint = "class_getInstanceVariable")]
    private static extern IntPtr class_getInstanceVariable(IntPtr cls, string name);

    [DllImport(LibObjC, EntryPoint = "object_getClass")]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport(LibObjC, EntryPoint = "object_setIvar")]
    private static extern void object_setIvar(IntPtr obj, IntPtr ivar, IntPtr value);

    [DllImport(LibObjC, EntryPoint = "object_getIvar")]
    private static extern IntPtr object_getIvar(IntPtr obj, IntPtr ivar);

    private static IntPtr MsgSendDouble(IntPtr receiver, string selector, double arg) =>
        objc_msgSend_Double(receiver, sel_registerName(selector), arg);

    private static void MsgSendVoid(IntPtr receiver, string selector, IntPtr arg) =>
        objc_msgSend_VoidPtr(receiver, sel_registerName(selector), arg);

    private static void MsgSendVoidPtr(IntPtr receiver, string selector, IntPtr arg) =>
        objc_msgSend_VoidPtr(receiver, sel_registerName(selector), arg);

    private static void MsgSendVoidBool(IntPtr receiver, string selector, bool arg) =>
        objc_msgSend_VoidBool(receiver, sel_registerName(selector), arg);

    private static void MsgSendVoidUlong(IntPtr receiver, string selector, nuint arg) =>
        objc_msgSend_VoidUlong(receiver, sel_registerName(selector), arg);

    private static void MsgSendUlong(IntPtr receiver, string selector, nuint arg) =>
        objc_msgSend_VoidUlong(receiver, sel_registerName(selector), arg);

    private static nint MsgSendNint(IntPtr receiver, string selector) =>
        objc_msgSend_Nint(receiver, sel_registerName(selector));

    private static ulong MsgSendUlongResult(IntPtr receiver, string selector) =>
        objc_msgSend_Ulong(receiver, sel_registerName(selector));

    private static NsRect MsgSendRect(IntPtr receiver, string selector) =>
        objc_msgSend_Rect(receiver, sel_registerName(selector));

    private static NsRect MsgSendRectRect(IntPtr receiver, string selector, NsRect arg) =>
        objc_msgSend_RectRect(receiver, sel_registerName(selector), arg);

    private static IntPtr MsgSendPtrPtr(IntPtr receiver, string selector, IntPtr arg1, IntPtr arg2) =>
        objc_msgSend_PtrPtr(receiver, sel_registerName(selector), arg1, arg2);

    private static IntPtr MsgSendPtrPtrPtr(
        IntPtr receiver,
        string selector,
        IntPtr arg1,
        IntPtr arg2,
        IntPtr arg3) =>
        objc_msgSend_PtrPtrPtr(receiver, sel_registerName(selector), arg1, arg2, arg3);

    [StructLayout(LayoutKind.Sequential)]
    private struct NsRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }
}
