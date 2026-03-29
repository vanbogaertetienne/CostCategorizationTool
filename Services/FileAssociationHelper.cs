using Microsoft.Win32;

namespace CostCategorizationTool.Services;

/// <summary>
/// Registers / unregisters .ccp file association under HKCU (no admin required).
/// Double-clicking a .ccp file will launch the application with the file path as arg[0].
/// </summary>
internal static class FileAssociationHelper
{
    private const string ProgId   = "CostCategorizationTool.Project";
    private const string ExtKey   = @"Software\Classes\.ccp";
    private const string ProgKey  = @"Software\Classes\" + ProgId;

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ExtKey);
        return key?.GetValue(null) as string == ProgId;
    }

    public static void Register()
    {
        string exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];

        // .ccp → ProgId
        using (var ext = Registry.CurrentUser.CreateSubKey(ExtKey))
            ext.SetValue(null, ProgId);

        // ProgId description
        using (var prog = Registry.CurrentUser.CreateSubKey(ProgKey))
            prog.SetValue(null, "Cost Categorization Project");

        // Default icon (the exe itself)
        using (var icon = Registry.CurrentUser.CreateSubKey($@"{ProgKey}\DefaultIcon"))
            icon.SetValue(null, $"\"{exePath}\",0");

        // Open command
        using (var cmd = Registry.CurrentUser.CreateSubKey($@"{ProgKey}\shell\open\command"))
            cmd.SetValue(null, $"\"{exePath}\" \"%1\"");

        // Notify the shell so Explorer updates immediately without a reboot
        SHChangeNotify();
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(ExtKey,  throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ProgKey, throwOnMissingSubKey: false);
        SHChangeNotify();
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId = 0x08000000 /* SHCNE_ASSOCCHANGED */,
        uint uFlags = 0, nint dwItem1 = 0, nint dwItem2 = 0);
}
