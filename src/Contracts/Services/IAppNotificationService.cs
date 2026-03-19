using System.Collections.Specialized;

namespace BatteryTracker.Contracts.Services;

public interface IAppNotificationService
{
    void Initialize();

    bool Show(string payload);

    bool ShowDischargeReminder(string payload);

    NameValueCollection ParseArguments(string arguments);

    void Unregister();
}
