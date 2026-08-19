using System.Runtime.InteropServices;
using System.Text;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Generic-password Keychain via Security.framework. Not available off macOS;
/// does not fall back to plaintext or in-memory storage.
/// </summary>
public sealed class SecurityFrameworkKeychain : IMacKeychain
{
    public void SetPassword(string service, string account, string secret)
    {
        ValidateIdentity(service, account);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        EnsureMacOS();
        NativeKeychain.SetPassword(service, account, secret);
    }

    public string? GetPassword(string service, string account)
    {
        ValidateIdentity(service, account);
        EnsureMacOS();
        return NativeKeychain.GetPassword(service, account);
    }

    public void DeletePassword(string service, string account)
    {
        ValidateIdentity(service, account);
        EnsureMacOS();
        NativeKeychain.DeletePassword(service, account);
    }

    private static void ValidateIdentity(string service, string account)
    {
        if (string.IsNullOrWhiteSpace(service))
        {
            throw new ArgumentException("Service must not be empty.", nameof(service));
        }

        if (string.IsNullOrWhiteSpace(account))
        {
            throw new ArgumentException("Account must not be empty.", nameof(account));
        }
    }

    private static void EnsureMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "macOS Keychain is only available on macOS. The macOS host does not use plaintext or in-memory secret storage.");
        }
    }
}

internal static class NativeKeychain
{
    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint KCfStringEncodingUtf8 = 0x08000100;
    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;
    private const int ErrSecDuplicateItem = -25299;

    public static void SetPassword(string service, string account, string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        using var session = new CfSession();
        var add = session.CreateDictionary(
            (session.Sec("kSecClass"), session.Sec("kSecClassGenericPassword")),
            (session.Sec("kSecAttrService"), session.String(service)),
            (session.Sec("kSecAttrAccount"), session.String(account)),
            (session.Sec("kSecValueData"), session.Data(bytes)),
            (session.Sec("kSecAttrAccessible"), session.Sec("kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly")));

        var status = SecItemAdd(add, IntPtr.Zero);
        if (status == ErrSecDuplicateItem)
        {
            var query = session.CreateDictionary(
                (session.Sec("kSecClass"), session.Sec("kSecClassGenericPassword")),
                (session.Sec("kSecAttrService"), session.String(service)),
                (session.Sec("kSecAttrAccount"), session.String(account)));
            var update = session.CreateDictionary(
                (session.Sec("kSecValueData"), session.Data(bytes)));
            status = SecItemUpdate(query, update);
        }

        if (status != ErrSecSuccess)
        {
            throw new InvalidOperationException("The macOS Keychain could not save the automation secret.");
        }
    }

    public static string? GetPassword(string service, string account)
    {
        using var session = new CfSession();
        var query = session.CreateDictionary(
            (session.Sec("kSecClass"), session.Sec("kSecClassGenericPassword")),
            (session.Sec("kSecAttrService"), session.String(service)),
            (session.Sec("kSecAttrAccount"), session.String(account)),
            (session.Sec("kSecReturnData"), session.CfBooleanTrue()),
            (session.Sec("kSecMatchLimit"), session.Sec("kSecMatchLimitOne")));

        var status = SecItemCopyMatching(query, out var result);
        if (status == ErrSecItemNotFound)
        {
            return null;
        }

        if (status != ErrSecSuccess || result == IntPtr.Zero)
        {
            throw new InvalidOperationException("The macOS Keychain could not read the automation secret.");
        }

        try
        {
            var length = (int)CFDataGetLength(result);
            if (length <= 0)
            {
                return null;
            }

            var pointer = CFDataGetBytePtr(result);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CFRelease(result);
        }
    }

    public static void DeletePassword(string service, string account)
    {
        using var session = new CfSession();
        var query = session.CreateDictionary(
            (session.Sec("kSecClass"), session.Sec("kSecClassGenericPassword")),
            (session.Sec("kSecAttrService"), session.String(service)),
            (session.Sec("kSecAttrAccount"), session.String(account)));

        var status = SecItemDelete(query);
        if (status is not ErrSecSuccess and not ErrSecItemNotFound)
        {
            throw new InvalidOperationException("The macOS Keychain could not delete the automation secret.");
        }
    }

    [DllImport(SecurityLibrary)]
    private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemDelete(IntPtr query);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFStringCreateWithBytes(
        IntPtr allocator,
        byte[] bytes,
        nint numBytes,
        uint encoding,
        [MarshalAs(UnmanagedType.U1)] bool isExternalRepresentation);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataGetLength(IntPtr data);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr keys,
        IntPtr values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(IntPtr cf);

    private sealed class CfSession : IDisposable
    {
        private readonly List<IntPtr> _owned = [];
        private readonly IntPtr _security;
        private readonly IntPtr _coreFoundation;
        private readonly IntPtr _keyCallBacks;
        private readonly IntPtr _valueCallBacks;
        private readonly IntPtr _cfBooleanTrue;

        public CfSession()
        {
            _security = NativeLibrary.Load(SecurityLibrary);
            _coreFoundation = NativeLibrary.Load(CoreFoundationLibrary);
            _keyCallBacks = NativeLibrary.GetExport(_coreFoundation, "kCFTypeDictionaryKeyCallBacks");
            _valueCallBacks = NativeLibrary.GetExport(_coreFoundation, "kCFTypeDictionaryValueCallBacks");
            _cfBooleanTrue = Marshal.ReadIntPtr(NativeLibrary.GetExport(_coreFoundation, "kCFBooleanTrue"));
        }

        public IntPtr Sec(string exportName)
        {
            var address = NativeLibrary.GetExport(_security, exportName);
            var value = Marshal.ReadIntPtr(address);
            if (value == IntPtr.Zero)
            {
                throw new InvalidOperationException("The macOS Keychain API is unavailable.");
            }

            return value;
        }

        public IntPtr CfBooleanTrue() => _cfBooleanTrue;

        public IntPtr String(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var cf = CFStringCreateWithBytes(IntPtr.Zero, bytes, bytes.Length, KCfStringEncodingUtf8, false);
            if (cf == IntPtr.Zero)
            {
                throw new InvalidOperationException("The macOS Keychain API is unavailable.");
            }

            _owned.Add(cf);
            return cf;
        }

        public IntPtr Data(byte[] bytes)
        {
            var cf = CFDataCreate(IntPtr.Zero, bytes, bytes.Length);
            if (cf == IntPtr.Zero)
            {
                throw new InvalidOperationException("The macOS Keychain API is unavailable.");
            }

            _owned.Add(cf);
            return cf;
        }

        public IntPtr CreateDictionary(params (IntPtr Key, IntPtr Value)[] pairs)
        {
            var keys = pairs.Select(p => p.Key).ToArray();
            var values = pairs.Select(p => p.Value).ToArray();
            var keysHandle = GCHandle.Alloc(keys, GCHandleType.Pinned);
            var valuesHandle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var dict = CFDictionaryCreate(
                    IntPtr.Zero,
                    keysHandle.AddrOfPinnedObject(),
                    valuesHandle.AddrOfPinnedObject(),
                    pairs.Length,
                    _keyCallBacks,
                    _valueCallBacks);
                if (dict == IntPtr.Zero)
                {
                    throw new InvalidOperationException("The macOS Keychain API is unavailable.");
                }

                _owned.Add(dict);
                return dict;
            }
            finally
            {
                keysHandle.Free();
                valuesHandle.Free();
            }
        }

        public void Dispose()
        {
            for (var i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != IntPtr.Zero)
                {
                    CFRelease(_owned[i]);
                }
            }

            _owned.Clear();
        }
    }
}
