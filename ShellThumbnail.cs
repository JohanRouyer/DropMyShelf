using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DropMyShelf;

/// <summary>Gets the same thumbnail/icon Explorer shows for a file or folder.</summary>
internal static class ShellThumbnail
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits,
        ref BITMAPINFOHEADER bmi, uint usage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static ImageSource? Get(string path, int size)
    {
        IntPtr hbmp = IntPtr.Zero;
        IShellItemImageFactory? factory = null;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            if (factory.GetImage(new SIZE { cx = size, cy = size }, 0, out hbmp) != 0 || hbmp == IntPtr.Zero)
                return null;
            if (GetObject(hbmp, Marshal.SizeOf<BITMAP>(), out var bm) == 0)
                return null;

            // Copy the pixels by hand: Imaging.CreateBitmapSourceFromHBitmap drops the alpha channel.
            // Asking GetDIBits for a negative height gets top-down rows whatever the bitmap's own
            // layout: the shell's bitmaps don't reliably say which way up they are stored.
            int width = bm.bmWidth, height = Math.Abs(bm.bmHeight), stride = width * 4;
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new byte[stride * height];
            IntPtr screen = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(screen, hbmp, 0, (uint)height, pixels, ref header, 0) != height)
                    return null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
            }

            if (IsFullyTransparent(pixels))
                for (int i = 3; i < pixels.Length; i += 4)
                    pixels[i] = 255;

            var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbmp != IntPtr.Zero)
                DeleteObject(hbmp);
            if (factory != null)
                Marshal.ReleaseComObject(factory);
        }
    }

    // Photo thumbnails sometimes come back with an all-zero alpha channel.
    private static bool IsFullyTransparent(byte[] pixels)
    {
        for (int i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0)
                return false;
        return true;
    }
}
