using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace KoalaFileExplorer.Services;

public static class ThumbnailService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, [In] int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    public static async Task<BitmapSource?> GetAsync(string path, int size = 100)
        => await Task.Run(() => Get(path, size));

    private static BitmapSource? Get(string path, int size)
    {
        try
        {
            var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var obj);
            if (obj is not IShellItemImageFactory factory) return null;
            if (factory.GetImage(new SIZE { cx = size, cy = size }, 0, out var hbm) != 0) return null;
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHBitmap(
                    hbm, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally { DeleteObject(hbm); }
        }
        catch { return null; }
    }
}
