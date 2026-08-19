using System.Runtime.InteropServices;
using System.Text;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Delivers notifications through AppKit <c>NSUserNotificationCenter</c>.
/// Failures must not reach the poller. Click activation is optional.
/// </summary>
public sealed class NativeMacNotificationService : INotificationService, IDisposable
{
    private readonly Action<MonitoredObjectId>? _onActivate;
    private readonly GCHandle _selfHandle;
    private readonly IntPtr _target;
    private bool _disposed;

    public NativeMacNotificationService(
        Action<MonitoredObjectId>? onActivate = null,
        bool requestUserNotifications = false)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Native macOS notifications require macOS.");
        }

        _onActivate = onActivate;
        NotifyObjC.EnsureAppKit();
        _selfHandle = GCHandle.Alloc(this);
        _target = NotifyObjC.CreateTarget(GCHandle.ToIntPtr(_selfHandle));
        NotifyObjC.SetCenterDelegate(_target);
        if (requestUserNotifications)
        {
            NotifyObjC.RequestModernAuthorization();
        }
    }

    public void Show(IncidentAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ObjectDisposedException.ThrowIf(_disposed, this);
        NotifyObjC.Deliver(alert.Title, alert.Body, Encode(alert.ObjectId));
    }

    internal void HandleActivate(string? token)
    {
        if (!TryDecode(token, out var id) || id is null)
        {
            return;
        }

        try
        {
            _onActivate?.Invoke(id);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    public static string Encode(MonitoredObjectId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Kind == ObjectKind.Host
            ? "H|" + id.HostName
            : "S|" + id.HostName + "|" + (id.ServiceDescription ?? string.Empty);
    }

    public static bool TryDecode(string? token, out MonitoredObjectId? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('|');
        if (parts.Length < 2)
        {
            return false;
        }

        try
        {
            id = parts[0] == "H"
                ? MonitoredObjectId.Host(new SiteId("site"), parts[1])
                : MonitoredObjectId.Service(new SiteId("site"), parts[1], parts.Length > 2 ? parts[2] : string.Empty);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal static class NotifyObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string UserNotifications =
        "/System/Library/Frameworks/UserNotifications.framework/UserNotifications";

    private static readonly ActivateCallback Callback = OnActivate;
    private static readonly IntPtr CallbackPtr = Marshal.GetFunctionPointerForDelegate(Callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateCallback(IntPtr self, IntPtr cmd, IntPtr center, IntPtr notification);

    public static void EnsureAppKit() => NativeLibrary.Load(AppKit);

    /// <summary>
    /// <c>+[UNUserNotificationCenter currentNotificationCenter]</c> asserts and
    /// kills the process when the host is not a bundled app with a bundle
    /// identifier. Managed try/catch cannot contain that Objective-C exception.
    /// Never invoke the selector unless <see cref="HasMainBundleIdentifier"/> is true.
    /// </summary>
    public static bool HasMainBundleIdentifier()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            EnsureAppKit();
            var identifier = TryGetMainBundleIdentifier();
            return !string.IsNullOrWhiteSpace(identifier);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string? TryGetMainBundleIdentifier()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            EnsureAppKit();
            var bundleClass = objc_getClass("NSBundle");
            if (bundleClass == IntPtr.Zero)
            {
                return null;
            }

            var bundle = objc_msgSend(bundleClass, sel_registerName("mainBundle"));
            if (bundle == IntPtr.Zero)
            {
                return null;
            }

            var identifier = objc_msgSend(bundle, sel_registerName("bundleIdentifier"));
            return NsStringToUtf8(identifier);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool RequestModernAuthorization()
    {
        if (!HasMainBundleIdentifier())
        {
            return false;
        }

        try
        {
            NativeLibrary.Load(UserNotifications);
            var cls = objc_getClass("UNUserNotificationCenter");
            if (cls == IntPtr.Zero)
            {
                return false;
            }

            var center = objc_msgSend(cls, sel_registerName("currentNotificationCenter"));
            if (center == IntPtr.Zero)
            {
                return false;
            }

            objc_msgSend_UlongPtr(
                center,
                sel_registerName("requestAuthorizationWithOptions:completionHandler:"),
                7,
                IntPtr.Zero);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static IntPtr CreateTarget(IntPtr context)
    {
        var nsObject = objc_getClass("NSObject");
        var className = "CdnMacNotifyTarget" + Guid.NewGuid().ToString("N");
        var cls = objc_allocateClassPair(nsObject, className, 0);
        if (cls == IntPtr.Zero)
        {
            throw new InvalidOperationException("The macOS notification target could not be created.");
        }

        if (!class_addMethod(
                cls,
                sel_registerName("userNotificationCenter:didActivateNotification:"),
                CallbackPtr,
                "v@:@@"))
        {
            throw new InvalidOperationException("The macOS notification target could not be created.");
        }

        if (!class_addIvar(cls, "cdnContext", (nuint)IntPtr.Size, (byte)Math.Log2(IntPtr.Size), "@"))
        {
            throw new InvalidOperationException("The macOS notification target could not be created.");
        }

        objc_registerClassPair(cls);
        var instance = objc_msgSend(cls, sel_registerName("new"));
        object_setIvar(instance, class_getInstanceVariable(cls, "cdnContext"), context);
        return instance;
    }

    public static void SetCenterDelegate(IntPtr target)
    {
        var center = Center();
        if (center == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_VoidPtr(center, sel_registerName("setDelegate:"), target);
    }

    public static void Deliver(string title, string body, string token)
    {
        var cls = objc_getClass("NSUserNotification");
        if (cls == IntPtr.Zero)
        {
            return;
        }

        var notification = objc_msgSend(objc_msgSend(cls, sel_registerName("alloc")), sel_registerName("init"));
        objc_msgSend_VoidPtr(notification, sel_registerName("setTitle:"), NsString(title ?? string.Empty));
        objc_msgSend_VoidPtr(
            notification,
            sel_registerName("setInformativeText:"),
            NsString(body ?? string.Empty));
        objc_msgSend_VoidPtr(
            notification,
            sel_registerName("setIdentifier:"),
            NsString(token ?? string.Empty));
        var center = Center();
        if (center == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_VoidPtr(center, sel_registerName("deliverNotification:"), notification);
    }

    private static IntPtr Center()
    {
        var cls = objc_getClass("NSUserNotificationCenter");
        return cls == IntPtr.Zero
            ? IntPtr.Zero
            : objc_msgSend(cls, sel_registerName("defaultUserNotificationCenter"));
    }

    private static void OnActivate(IntPtr self, IntPtr cmd, IntPtr center, IntPtr notification)
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
            if (handle.Target is not NativeMacNotificationService service)
            {
                return;
            }

            var identifier = objc_msgSend(notification, sel_registerName("identifier"));
            service.HandleActivate(NsStringToUtf8(identifier));
        });
    }

    private static IntPtr NsString(string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value + "\0");
        var handle = GCHandle.Alloc(utf8, GCHandleType.Pinned);
        try
        {
            return objc_msgSend_IntPtr(
                objc_getClass("NSString"),
                sel_registerName("stringWithUTF8String:"),
                handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static string? NsStringToUtf8(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero)
        {
            return null;
        }

        var utf8 = objc_msgSend(nsString, sel_registerName("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_VoidPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_UlongPtr(IntPtr receiver, IntPtr selector, nuint arg1, IntPtr arg2);

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
}
