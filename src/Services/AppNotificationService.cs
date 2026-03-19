using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using BatteryTracker.Contracts.Services;
using BatteryTracker.Views;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace BatteryTracker.Services;

public sealed class AppNotificationService : IAppNotificationService
{
    private readonly ISettingsService _settingsService;

    public AppNotificationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    ~AppNotificationService()
    {
        Unregister();
    }

    public void Initialize()
    {
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        AppNotificationManager.Default.Register();
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("action", out string? action) && action == "snooze")
        {
            int minutes = _settingsService.DischargeReminderSnoozeMinutes;
            if (args.UserInput.TryGetValue("snoozeMinutes", out string? inputValue))
            {
                if (int.TryParse(inputValue, out int parsed) && parsed > 0)
                {
                    minutes = parsed;
                }
            }
            if (args.Arguments.TryGetValue("minutes", out string? minutesText)
                && int.TryParse(minutesText, out int parsedFromArgs)
                && parsedFromArgs > 0)
            {
                minutes = parsedFromArgs;
            }
            App.GetService<BatteryIcon>().SnoozeDischargeReminder(minutes);
        }
        else if (args.Arguments.TryGetValue("action", out string? ackAction) && ackAction == "ack")
        {
            App.GetService<BatteryIcon>().AcknowledgeDischargeReminder();
        }
    }

    public bool Show(string payload)
    {
        AppNotificationBuilder builder = new AppNotificationBuilder()
            .AddText(payload);

        AppNotification appNotification = builder.BuildNotification();
        AppNotificationManager.Default.Show(appNotification);

        return appNotification.Id != 0;
    }

    public bool ShowDischargeReminder(string payload)
    {
        int minutes = Math.Max(1, _settingsService.DischargeReminderSnoozeMinutes);
        List<int> options = new() { 5, 10, 30, 60, 120 };
        if (!options.Contains(minutes))
        {
            options[^1] = minutes;
        }
        options = options.Distinct().OrderBy(m => m).Take(5).ToList();

        ToastSelectionBox snoozeInput = new("snoozeMinutes")
        {
            DefaultSelectionBoxItemId = minutes.ToString()
        };
        foreach (int option in options)
        {
            snoozeInput.Items.Add(new ToastSelectionBoxItem(option.ToString(), $"{option} 分钟"));
        }

        ToastContentBuilder builder = new ToastContentBuilder()
            .AddText(payload)
            .AddToastInput(snoozeInput)
            .AddButton(new ToastButton("我知道了", "action=ack"))
            .AddButton(new ToastButton("延后提醒", "action=snooze"));

        string xml = builder.GetToastContent().GetContent();
        AppNotification appNotification = new AppNotification(xml);
        AppNotificationManager.Default.Show(appNotification);

        return appNotification.Id != 0;
    }

    public NameValueCollection ParseArguments(string arguments) => HttpUtility.ParseQueryString(arguments);

    public void Unregister()
    {
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
    }
}
