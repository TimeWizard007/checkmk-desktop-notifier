using System.Runtime.InteropServices;
using CheckmkDesktopNotifier.Core.Notifications;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Plays mixed PCM WAV through AppKit <c>NSSound</c>. Not Windows <c>SoundPlayer</c>.
/// </summary>
public sealed class MacAlertSoundPlayer
{
    public static bool TryPlay(ReadOnlySpan<byte> wav)
    {
        if (!OperatingSystem.IsMacOS() || wav.Length == 0)
        {
            return false;
        }

        try
        {
            return SoundObjC.Play(wav.ToArray());
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal static class SoundObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    private static IntPtr _playing;

    public static bool Play(byte[] wav)
    {
        if (wav.Length == 0 || !PcmWavParser.TryParse(wav, out _, out _))
        {
            return false;
        }

        NativeLibrary.Load(AppKit);
        var handle = GCHandle.Alloc(wav, GCHandleType.Pinned);
        try
        {
            var data = objc_msgSend_PtrNuint(
                objc_getClass("NSData"),
                sel_registerName("dataWithBytes:length:"),
                handle.AddrOfPinnedObject(),
                (nuint)wav.Length);
            if (data == IntPtr.Zero)
            {
                return false;
            }

            var sound = objc_msgSend_IntPtr(
                objc_msgSend(objc_getClass("NSSound"), sel_registerName("alloc")),
                sel_registerName("initWithData:"),
                data);
            if (sound == IntPtr.Zero)
            {
                return false;
            }

            ReleasePlaying();
            _playing = sound;
            objc_msgSend_Void(sound, sel_registerName("play"));
            return true;
        }
        finally
        {
            handle.Free();
        }
    }

    private static void ReleasePlaying()
    {
        if (_playing == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_Void(_playing, sel_registerName("release"));
        _playing = IntPtr.Zero;
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
    private static extern IntPtr objc_msgSend_PtrNuint(IntPtr receiver, IntPtr selector, IntPtr arg1, nuint arg2);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void(IntPtr receiver, IntPtr selector);
}
