using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace StmInstaller.Services;

/// <summary>Creates .lnk shortcuts via the Windows shell.</summary>
internal static class ShortcutService
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

    public static void Create(string targetExe, string lnkPath, string description)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkClass();
            var persist = (IPersistFile)link;

            link.SetPath(targetExe);
            link.SetDescription(description);
            link.SetIconLocation(targetExe, 0);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetExe) ?? string.Empty);

            persist.Save(lnkPath, true);
        }
        catch
        {
            // Shortcut is best-effort; install continues.
        }
    }

    /// <summary>Resolves an .lnk's target path, or null on failure.</summary>
    public static string? TargetOf(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkClass();
            ((IPersistFile)link).Load(lnkPath, 0);
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
}
