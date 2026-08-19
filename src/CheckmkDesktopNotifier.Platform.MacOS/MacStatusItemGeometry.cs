using System.Runtime.InteropServices;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// NSRect / CGRect is 32 bytes. On Intel x86_64 the Objective-C runtime returns
/// that struct through <c>objc_msgSend_stret</c>, not <c>objc_msgSend</c>. Calling
/// <c>objc_msgSend</c> for <c>frame</c> or <c>convertRectToScreen:</c> SIGSEGVs.
/// ARM64 uses <c>objc_msgSend</c> for the same selectors.
/// </summary>
public static class MacStatusItemGeometry
{
    public static bool CanQueryButtonFrame =>
        RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.Arm;
}
