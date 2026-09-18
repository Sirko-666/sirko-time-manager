using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace TimerApp.Services;

/// <summary>Resolves .lnk shortcuts to their target executables.</summary>
public static class ShortcutResolver
{
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short pwHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComImport]
    private class ShellLinkClass;

    public static string? Resolve(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkClass();
            var persist = (IPersistFile)link;
            persist.Load(shortcutPath, 0);

            var target = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            string resolved = target.ToString();

            return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns the exe behind an .lnk, or the original path when it is not resolvable.</summary>
    public static string ResolveOrSelf(string path) =>
        path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            ? Resolve(path) ?? path
            : path;
}
