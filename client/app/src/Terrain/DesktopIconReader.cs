using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Terrain;

/// <summary>
/// Reads desktop item positions through the shell's own view (IFolderView) on a dedicated STA thread.
/// Only positions and the folder flag are read; item names are never requested (design §5-1, §6-4).
/// Every failure becomes an IconReadResult with an HRESULT, never an exception.
/// </summary>
public sealed class DesktopIconReader : IDisposable
{
    readonly BlockingCollection<TaskCompletionSource<IconReadResult>> _requests = new();
    readonly Thread _thread;

    public DesktopIconReader()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "hangbar-shell-sta" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<IconReadResult> ReadAsync()
    {
        var tcs = new TaskCompletionSource<IconReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Add(tcs);
        return tcs.Task;
    }

    public void Dispose() => _requests.CompleteAdding();

    void Loop()
    {
        foreach (var tcs in _requests.GetConsumingEnumerable())
        {
            try { tcs.SetResult(ReadOnce()); }
            catch (COMException e) { tcs.SetResult(DesktopIconMapper.Failed($"0x{e.HResult:X8}")); }
            catch (Exception e) { tcs.SetResult(DesktopIconMapper.Failed($"0x{e.HResult:X8}")); }
        }
    }

    static IconReadResult ReadOnce()
    {
        var shellWindows = (IShellWindows)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellWindows)!)!;
        object loc = CSIDL_DESKTOP, root = new object();
        int hr = shellWindows.FindWindowSW(ref loc, ref root, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH, out var disp);
        if (hr != 0 || disp is null) return DesktopIconMapper.Failed($"0x{hr:X8}");

        var sp = (IServiceProvider)disp;
        Guid sid = SID_STopLevelBrowser, iid = typeof(IShellBrowser).GUID;
        hr = sp.QueryService(ref sid, ref iid, out var browserObj);
        if (hr != 0) return DesktopIconMapper.Failed($"0x{hr:X8}");
        var browser = (IShellBrowser)browserObj;

        hr = browser.QueryActiveShellView(out var view);
        if (hr != 0 || view is null) return DesktopIconMapper.Failed($"0x{hr:X8}");
        var folderView = (IFolderView)view;

        Guid iidFolder = typeof(IShellFolder).GUID;
        hr = folderView.GetFolder(ref iidFolder, out var folderObj);
        if (hr != 0) return DesktopIconMapper.Failed($"0x{hr:X8}");
        var folder = (IShellFolder)folderObj;

        folderView.ItemCount(SVGIO_ALLVIEW, out int count);
        folderView.GetSpacing(out var spacing);

        var raw = new List<RawIcon>(count);
        for (int i = 0; i < count; i++)
        {
            if (folderView.Item(i, out var pidl) != 0 || pidl == 0) continue;
            try
            {
                if (folderView.GetItemPosition(pidl, out var pos) != 0) continue;
                uint attrs = SFGAO_FOLDER;
                var arr = new[] { pidl };
                bool isFolder = folder.GetAttributesOf(1, arr, ref attrs) == 0 && (attrs & SFGAO_FOLDER) != 0;
                raw.Add(new RawIcon(new PointI(pos.X, pos.Y), isFolder));
            }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }

        var origin = ListViewOrigin();
        return DesktopIconMapper.Map(raw, origin, spacing.X, spacing.Y);
    }

    /// <summary>Top-left of the desktop list view (under Progman, or a WorkerW when a slideshow wallpaper is on).</summary>
    static PointI ListViewOrigin()
    {
        nint lv = FindListView(Win32.FindWindow("Progman", null));
        if (lv == 0)
        {
            nint w = 0;
            while ((w = Win32.FindWindowEx(0, w, "WorkerW", null)) != 0)
                if ((lv = FindListView(w)) != 0) break;
        }
        return lv != 0 && Win32.GetWindowRect(lv, out var r) ? new PointI(r.Left, r.Top) : new PointI(0, 0);
    }

    static nint FindListView(nint parent)
    {
        if (parent == 0) return 0;
        nint defView = Win32.FindWindowEx(parent, 0, "SHELLDLL_DefView", null);
        return defView == 0 ? 0 : Win32.FindWindowEx(defView, 0, "SysListView32", null);
    }

    // ---- COM declarations -------------------------------------------------------------------

    static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    const int CSIDL_DESKTOP = 0, SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1;
    const uint SVGIO_ALLVIEW = 2, SFGAO_FOLDER = 0x20000000;

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface IShellWindows
    {
        void _get_Count(); void _Item(); void _NewEnum(); void _Register(); void _RegisterPending();
        void _Revoke(); void _OnNavigate(); void _OnActivated();
        [PreserveSig]
        int FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object pvarloc, [MarshalAs(UnmanagedType.Struct)] ref object pvarlocRoot,
            int swClass, out int phwnd, int swfwOptions, [MarshalAs(UnmanagedType.IDispatch)] out object? ppdispOut);
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
    }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellBrowser
    {
        void _GetWindow(); void _ContextSensitiveHelp();
        void _InsertMenusSB(); void _SetMenuSB(); void _RemoveMenusSB(); void _SetStatusTextSB(); void _EnableModelessSB();
        void _TranslateAcceleratorSB(); void _BrowseObject(); void _GetViewStateStream(); void _GetControlWindow(); void _SendControlMsg();
        [PreserveSig]
        int QueryActiveShellView([MarshalAs(UnmanagedType.IUnknown)] out object? ppshv);
    }

    [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFolderView
    {
        void _GetCurrentViewMode(); void _SetCurrentViewMode();
        [PreserveSig] int GetFolder(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int Item(int iItemIndex, out nint ppidl);
        [PreserveSig] int ItemCount(uint uFlags, out int pcItems);
        void _Items(); void _GetSelectionMarkedItem(); void _GetFocusedItem();
        [PreserveSig] int GetItemPosition(nint pidl, out Win32.POINT ppt);
        [PreserveSig] int GetSpacing(out Win32.POINT ppt);
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellFolder
    {
        void _ParseDisplayName(); void _EnumObjects(); void _BindToObject(); void _BindToStorage();
        void _CompareIDs(); void _CreateViewObject();
        [PreserveSig] int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] apidl, ref uint rgfInOut);
    }
}
