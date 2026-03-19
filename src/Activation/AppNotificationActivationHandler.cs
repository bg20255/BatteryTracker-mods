using System.Diagnostics;
using BatteryTracker.Contracts.Services;
using BatteryTracker.Helpers;
using BatteryTracker.Views;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;

namespace BatteryTracker.Activation;

public sealed class AppNotificationActivationHandler : ActivationHandler<AppActivationArguments>
{
    // private readonly IAppNotificationService _notificationService;

    public AppNotificationActivationHandler()
    {
        // _notificationService = notificationService;
    }

    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        return args.Kind == ExtendedActivationKind.AppNotification;
    }

    protected override async Task HandleInternalAsync(AppActivationArguments args)
    {
        // Access the AppNotificationActivatedEventArgs.
        var activatedEventArgs = (AppNotificationActivatedEventArgs)args.Data;

        if (activatedEventArgs.Arguments.TryGetValue("action", out string? action) && action == "snooze")
        {
            int minutes = App.GetService<ISettingsService>().DischargeReminderSnoozeMinutes;
            if (activatedEventArgs.UserInput.TryGetValue("snoozeMinutes", out string? inputValue))
            {
                if (int.TryParse(inputValue, out int parsedInput) && parsedInput > 0)
                {
                    minutes = parsedInput;
                }
            }
            if (activatedEventArgs.Arguments.TryGetValue("minutes", out string? minutesText)
                && int.TryParse(minutesText, out int parsed)
                && parsed > 0)
            {
                minutes = parsed;
            }
            App.GetService<BatteryIcon>().SnoozeDischargeReminder(minutes);
            return;
        }
        if (activatedEventArgs.Arguments.TryGetValue("action", out string? ackAction) && ackAction == "ack")
        {
            App.GetService<BatteryIcon>().AcknowledgeDischargeReminder();
            return;
        }

        // Handle the case when users click `Submit feedback` button on notifications
        if (activatedEventArgs.Arguments.TryGetValue("action", out string? actionValue) && actionValue == "SubmitFeedback")
        {
            await LaunchHelper.LaunchUriAsync(LaunchHelper.GitHubNewIssueUri);
            // Quit
            Process.GetCurrentProcess().Kill();
        }
    }
}


