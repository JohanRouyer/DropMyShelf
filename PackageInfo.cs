using System.Runtime.InteropServices;

namespace DropMyShelf;

/// <summary>Tells the Microsoft Store (MSIX) build apart from the version installed by DropMyShelf's own installer.</summary>
internal static class PackageInfo
{
    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// <summary>
    /// True when running from an MSIX package. The Store then handles updates and startup is a
    /// package startup task managed in Windows Settings, instead of a registry Run entry.
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackage();

    private static bool DetectPackage()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }
}
