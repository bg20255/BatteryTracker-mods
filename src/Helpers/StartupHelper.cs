using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.UI.Popups;

namespace BatteryTracker.Helpers;

internal static class StartupHelper
{
    private const string StartupTaskName = "BatteryTrackerStartupTask";
    private static StartupTask? _startupTask;

    internal static async Task<bool> IsRunAtStartup()
    {
        if (!RuntimeHelper.IsMSIX)
        {
            return IsRunAtStartupUnpackaged();
        }

        _startupTask ??= await StartupTask.GetAsync(StartupTaskName);
        StartupTaskState startupTaskState = _startupTask.State;
        return startupTaskState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    internal static async Task<bool> DisableStartup()
    {
        if (!RuntimeHelper.IsMSIX)
        {
            return DisableStartupUnpackaged();
        }

        _startupTask ??= await StartupTask.GetAsync(StartupTaskName);
        switch (_startupTask.State)
        {
            case StartupTaskState.Enabled:
                _startupTask.Disable();
                break;
            case StartupTaskState.EnabledByPolicy:
                var dialog = new MessageDialog("Startup enabled by group policy, or not supported on this device");
                await dialog.ShowAsync();
                break;
        }
        return true;
    }

    internal static async Task<bool> EnableStartup()
    {
        if (!RuntimeHelper.IsMSIX)
        {
            return EnableStartupUnpackaged();
        }

        _startupTask ??= await StartupTask.GetAsync(StartupTaskName);
        switch (_startupTask.State)
        {
            case StartupTaskState.Disabled:
                StartupTaskState newState = await _startupTask.RequestEnableAsync();
                return newState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            case StartupTaskState.DisabledByPolicy:
                var dialog = new MessageDialog("Startup disabled by group policy, or not supported on this device");
                await dialog.ShowAsync();
                return false;
            default:
                return false;
        }
    }

    #region Unpackaged helpers (Registry HKCU\Software\Microsoft\Windows\CurrentVersion\Run)

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "BatteryTracker";

    private static string CurrentExePath =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    private static bool IsRunAtStartupUnpackaged()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        if (runKey == null) return false;
        string? value = runKey.GetValue(RunValueName) as string;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim('"');
        return string.Equals(Path.GetFullPath(normalized), Path.GetFullPath(CurrentExePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EnableStartupUnpackaged()
    {
        string exe = CurrentExePath;
        if (string.IsNullOrWhiteSpace(exe)) return false;
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (runKey == null) return false;
        runKey.SetValue(RunValueName, $"\"{exe}\"");
        return true;
    }

    private static bool DisableStartupUnpackaged()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (runKey == null) return false;
        if (runKey.GetValue(RunValueName) != null)
        {
            runKey.DeleteValue(RunValueName, false);
        }
        return true;
    }

    #endregion
}
