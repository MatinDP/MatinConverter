using System.Runtime.InteropServices;
namespace MatinConverter.ShellTests;

[ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    void GetTitle(IShellItemArray items, out IntPtr title); void GetIcon(IShellItemArray items, out IntPtr icon); void GetToolTip(IShellItemArray items, out IntPtr tip); void GetCanonicalName(out Guid name);
    void GetState(IShellItemArray items, [MarshalAs(UnmanagedType.Bool)] bool slow, out uint state);
    [PreserveSig] int Invoke(IShellItemArray items, IntPtr context); void GetFlags(out uint flags); void EnumSubCommands(out IntPtr commands);
}
[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemArray
{
    void BindToHandler(IntPtr binding, ref Guid handler, ref Guid iid, out IntPtr result); void GetPropertyStore(int flags, ref Guid iid, out IntPtr store); void GetPropertyDescriptionList(IntPtr key, ref Guid iid, out IntPtr list); void GetAttributes(uint flags, uint mask, out uint attributes); void GetCount(out uint count); void GetItemAt(uint index, out IntPtr item); void EnumItems(out IntPtr items);
}
