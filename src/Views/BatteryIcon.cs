using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using BatteryTracker.Contracts.Models;
using BatteryTracker.Contracts.Services;
using BatteryTracker.Helpers;
using CommunityToolkit.WinUI;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.System.Power;
using Windows.UI.ViewManagement;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Color = Windows.UI.Color;

namespace BatteryTracker.Views;

public partial class BatteryIcon : IDisposable
{
    private sealed class BatteryIconSettings(BatteryIcon batteryIcon) : IBatterySettings
    {
        public bool LowPowerNotificationEnabled { get; set; }

        public bool HighPowerNotificationEnabled { get; set; }

        public bool FullyChargedNotificationEnabled { get; set; }

        private bool _dischargeReminderEnabled;
        public bool DischargeReminderEnabled
        {
            get => _dischargeReminderEnabled;
            set
            {
                _dischargeReminderEnabled = value;
                batteryIcon.ResetDischargeReminderTimer();
                batteryIcon.UpdateDischargeReminderTimer();
            }
        }

        private int _dischargeReminderIntervalMinutes;
        public int DischargeReminderIntervalMinutes
        {
            get => _dischargeReminderIntervalMinutes;
            set
            {
                _dischargeReminderIntervalMinutes = value;
                batteryIcon.ResetDischargeReminderTimer();
                batteryIcon.UpdateDischargeReminderTimer();
            }
        }

        private int _lowPowerNotificationThreshold;
        public int LowPowerNotificationThreshold
        {
            get => _lowPowerNotificationThreshold;
            set
            {
                _lowPowerNotificationThreshold = value;
                batteryIcon._isLowPower = false;
            }
        }

        private int _highPowerNotificationThreshold;
        public int HighPowerNotificationThreshold
        {
            get => _highPowerNotificationThreshold;
            set
            {
                _highPowerNotificationThreshold = value;
                batteryIcon._isHighPower = false;
            }
        }
    }

    #region Static

    private static readonly Dictionary<double, int> DpiFontSizeMap = new()
    {
        { 1.0, 82 },
        { 1.25, 66 },
        { 1.5, 55 },
        { 1.75, 45 },
        { 2.0, 40 },
        { 2.25, 36 },
        { 2.5, 33 },
        { 3, 27 },
        { 3.5, 23 },
    };

    #endregion

    #region Properties

    public IBatterySettings Settings { get; }

    private int _chargedPercent;
    public int ChargedPercent
    {
        get => _chargedPercent;
        private set
        {
            if (value == _chargedPercent) return;

            _chargedPercent = value;
            string newPercentText = value switch
            {
                100 => " F",
                < 10 => $" {_chargedPercent}",
                _ => $"{_chargedPercent}"
            };
            // Use a DispatcherQueue to execute UI related code on the main UI thread.
            // Otherwise you may get an exception.
            Task.Run(async () =>
            {
                await _dispatcherQueue!.EnqueueAsync(() =>
                {
                    if (_iconSource != null)
                    {
                        _iconSource.Text = newPercentText;
                    }
                });
            });
        }
    }

    #endregion

    #region Private fields

    private static readonly Brush White = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
    private static readonly Brush Black = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
    private static readonly TimeSpan DischargeReminderPollInterval = TimeSpan.FromSeconds(10);

    private TaskbarIcon? _trayIcon;
    private GeneratedIconSource? _iconSource;
    private readonly UISettings _settings = new();
    private readonly IAppNotificationService _notificationService;
    private readonly ILogger<BatteryIcon> _logger;

    private bool _isLowPower;
    private bool _isHighPower;
    private DateTime _lastDischargeReminderUtc = DateTime.MinValue;
    private DateTime? _nextDischargeReminderUtc;
    private bool _dischargeReminderAwaitingAck;

    private bool _trayIconEventsRegistered;

    private DispatcherQueue? _dispatcherQueue;
    private Timer? _dischargeReminderTimer;

    #endregion

    public BatteryIcon(IAppNotificationService notificationService, ILogger<BatteryIcon> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
        Settings = new BatteryIconSettings(this);
    }

    public async Task InitAsync(TaskbarIcon icon)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _trayIcon = icon;
        _trayIcon.ForceCreate(true);
        _iconSource = _trayIcon.IconSource as GeneratedIconSource;

        // init percentage and color
        UpdateTrayIconPercent();
        await UpdateTrayIconThemeAsync();

        RegisterTrayIconEvents();
        // register display events
        PowerManager.DisplayStatusChanged += PowerManager_DisplayStatusChanged;

        _dischargeReminderTimer = new Timer(_ => CheckDischargeReminder(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        UpdateDischargeReminderTimer();
    }

    ~BatteryIcon()
    {
        Dispose();
    }

    public void Dispose()
    {
        UnregisterTrayIconEvents();
        PowerManager.DisplayStatusChanged -= PowerManager_DisplayStatusChanged;

        _dischargeReminderTimer?.Dispose();
        _trayIcon?.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ResetDischargeReminderTimer()
    {
        _lastDischargeReminderUtc = DateTime.UtcNow;
        _nextDischargeReminderUtc = null;
        _dischargeReminderAwaitingAck = false;
    }

    private void UpdateDischargeReminderTimer()
    {
        if (_dischargeReminderTimer == null)
        {
            return;
        }

        if (!Settings.DischargeReminderEnabled)
        {
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = false;
            _dischargeReminderTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        int minutes = Settings.DischargeReminderIntervalMinutes;
        if (minutes <= 0)
        {
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = false;
            _dischargeReminderTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        if (PowerManager.BatteryStatus != BatteryStatus.Discharging)
        {
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = false;
            _dischargeReminderTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        _dischargeReminderTimer.Change(DischargeReminderPollInterval, DischargeReminderPollInterval);

        if (PowerManager.BatteryStatus == BatteryStatus.Discharging && _nextDischargeReminderUtc == null)
        {
            _nextDischargeReminderUtc = DateTime.UtcNow.AddMinutes(minutes);
        }
    }

    private void CheckDischargeReminder()
    {
        if (!Settings.DischargeReminderEnabled)
        {
            _nextDischargeReminderUtc = null;
            return;
        }

        int minutes = Settings.DischargeReminderIntervalMinutes;
        if (minutes <= 0)
        {
            return;
        }

        if (PowerManager.BatteryStatus != BatteryStatus.Discharging)
        {
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = false;
            _dischargeReminderTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        DateTime now = DateTime.UtcNow;
        TimeSpan interval = TimeSpan.FromMinutes(minutes);
        if (_dischargeReminderAwaitingAck)
        {
            return;
        }

        _nextDischargeReminderUtc ??= now + interval;
        if (now >= _nextDischargeReminderUtc.Value)
        {
            _lastDischargeReminderUtc = now;
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = true;
            _notificationService.ShowDischargeReminder("电池正在放电，请考虑接通电源。");
        }
    }

    public void SnoozeDischargeReminder(int minutes)
    {
        if (!Settings.DischargeReminderEnabled || minutes <= 0)
        {
            return;
        }

        if (PowerManager.BatteryStatus != BatteryStatus.Discharging)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        _lastDischargeReminderUtc = now;
        _nextDischargeReminderUtc = now.AddMinutes(minutes);
        _dischargeReminderAwaitingAck = false;
        UpdateDischargeReminderTimer();
    }

    public void AcknowledgeDischargeReminder()
    {
        if (!Settings.DischargeReminderEnabled)
        {
            return;
        }

        if (PowerManager.BatteryStatus != BatteryStatus.Discharging)
        {
            _nextDischargeReminderUtc = null;
            _dischargeReminderAwaitingAck = false;
            return;
        }

        int minutes = Settings.DischargeReminderIntervalMinutes;
        if (minutes <= 0)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        _lastDischargeReminderUtc = now;
        _nextDischargeReminderUtc = now.AddMinutes(minutes);
        _dischargeReminderAwaitingAck = false;
        UpdateDischargeReminderTimer();
    }

    public async Task AdaptToDpiChange(double rastScale)
    {
        double scale = DpiFontSizeMap.Keys.MinBy(d => Math.Abs(d - rastScale));
        await _dispatcherQueue!.EnqueueAsync(() =>
        {
            if (_iconSource != null)
            {
                _iconSource.FontSize = DpiFontSizeMap[scale];
            }
        });
    }

    private async void PowerManager_DisplayStatusChanged(object? sender, object _)
    {
        DisplayStatus displayStatus = PowerManager.DisplayStatus;
        switch (displayStatus)
        {
            case DisplayStatus.Off:
                if (_trayIconEventsRegistered)
                {
                    UnregisterTrayIconEvents();
                    _logger.LogInformation("Display off, unregister power events");
                }
                break;
            case DisplayStatus.On:
                if (!_trayIconEventsRegistered)
                {
                    RegisterTrayIconEvents();
                    _logger.LogInformation("Display on, register power events");
                }
                UpdateTrayIconPercent();
                await UpdateTrayIconThemeAsync();
                break;
            case DisplayStatus.Dimmed:
                break;
            default:
                _logger.LogWarning("Invalid display status: {displayStatus}", displayStatus);
                break;
        }
    }

    private void RegisterTrayIconEvents()
    {
        // register power events
        PowerManager.RemainingChargePercentChanged += OnRemainingChargePercentChanged;
        // register theme events
        _settings.ColorValuesChanged += OnThemeChanged;
        _trayIconEventsRegistered = true;
    }

    private void UnregisterTrayIconEvents()
    {
        // unregister power events
        PowerManager.RemainingChargePercentChanged -= OnRemainingChargePercentChanged;
        // unregister theme events
        _settings.ColorValuesChanged -= OnThemeChanged;
        _trayIconEventsRegistered = false;
        // ! note: cannot add display status events here
    }

    private void OnRemainingChargePercentChanged(object? _, object? __)
    {
        UpdateTrayIconPercent();
        BatteryStatus batteryStatus = PowerManager.BatteryStatus;

        // push low power notification
        if (Settings.LowPowerNotificationEnabled
            && !_isLowPower
            && _chargedPercent <= Settings.LowPowerNotificationThreshold)
        {
            _isLowPower = true;
            _notificationService.Show($"{"LowPowerMessage".Localized()}: {_chargedPercent}%");
        }
        else if (_chargedPercent > Settings.LowPowerNotificationThreshold)
        {
            _isLowPower = false;
        }

        // push high power notification
        if (Settings.HighPowerNotificationEnabled
            && batteryStatus != BatteryStatus.Discharging // only when the battery is not discharging
            && !_isHighPower
            && _chargedPercent >= Settings.HighPowerNotificationThreshold)
        {
            _isHighPower = true;
            _notificationService.Show($"{"HighPowerMessage".Localized()}: {_chargedPercent}%");
        }
        else if (_chargedPercent < Settings.HighPowerNotificationThreshold)
        {
            _isHighPower = false;
        }

        // push fully charged notification
        if (Settings.FullyChargedNotificationEnabled && _chargedPercent == 100)
        {
            _notificationService.Show($"{"FullyChargedMessage".Localized()}⚡");
        }

        // refresh reminder schedule when power status changes
        UpdateDischargeReminderTimer();

    }

    private void UpdateTrayIconPercent() => ChargedPercent = PowerManager.RemainingChargePercent;

    private async void OnThemeChanged(UISettings sender, object? _)
    {
        await UpdateTrayIconThemeAsync();
    }

    private async Task UpdateTrayIconThemeAsync()
    {
        if (_trayIcon is null) return;

        await _dispatcherQueue!.EnqueueAsync(() =>
        {
            Brush newForeground = ShouldSystemUseDarkMode() ? White : Black;

            if (_iconSource != null)
            {
                _iconSource.Foreground = newForeground;
                // _trayIcon!.UpdateIcon(_iconSource.ToIcon());
            }
        });
    }

    [LibraryImport("UXTheme.dll", EntryPoint = "#138", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool ShouldSystemUseDarkMode();

#if DEBUG
    public void UpdateTrayIconPercent(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent));
        }
        ChargedPercent = percent;
    }
#endif
}
