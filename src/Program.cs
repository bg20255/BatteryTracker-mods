using System.Threading;
using BatteryTracker.Contracts.Services;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using BatteryTracker.Helpers;

namespace BatteryTracker
{
    public static class Program
    {
        private static DispatcherQueue? _uiDispatcherQueue;

        // Replaces the standard App.g.i.cs.
        // Note: We can't declare Main to be async because in a WinUI app
        // this prevents Narrator from reading XAML elements.
        [STAThread]
        static void Main()
        {
            // Ensure Windows App SDK is initialized before any WinAppSDK types are used (unpackaged mode).
            if (!RuntimeHelper.IsMSIX)
            {
                InitializeWindowsAppSdk();
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();

            bool isRedirect = DecideRedirection();
            if (!isRedirect)
            {
                Application.Start((p) =>
                {
                    _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
                    var context = new DispatcherQueueSynchronizationContext(_uiDispatcherQueue);
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }
        }

        private static void InitializeWindowsAppSdk()
        {
            // WinAppSDK 1.5 minimum runtime version (5001.159.55.0)
            var minVersion = new Microsoft.Windows.ApplicationModel.DynamicDependency.PackageVersion
            {
                Major = 5001,
                Minor = 159,
                Build = 55,
                Revision = 0
            };

            const uint majorMinorVersion = 0x00010005; // 1.5
            const string versionTag = "";

            if (!Bootstrap.TryInitialize(majorMinorVersion, versionTag, minVersion, Bootstrap.InitializeOptions.OnNoMatch_ShowUI, out int hr))
            {
                Environment.Exit(hr);
            }
        }

        private static void OnActivated(object? sender, AppActivationArguments args)
        {
            _uiDispatcherQueue!.EnqueueAsync(() =>
            {
                App.GetService<IActivationService>().ActivateAsync(args).Wait();
            }).Wait();
        }


        #region Redirection

        // Decide if we want to redirect the incoming activation to another instance.
        private static bool DecideRedirection()
        {
            bool isRedirect = false;

            AppInstance mainInstance = AppInstance.FindOrRegisterForKey("main");

            // If we successfully registered the file name, we must be the
            // only instance running that was activated for this file.
            if (mainInstance.IsCurrent)
            {
                // Hook up the Activated event, to allow for this instance of the app
                // getting reactivated as a result of multi-instance redirection.
                mainInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = true;

                // According to https://github.com/microsoft/WindowsAppSDK/issues/2959#issue-1368660765,
                // this method must be called first to use AppInstance.GetCurrent().GetActivatedEventArgs().
                AppNotificationManager.Default.Register();
                // Find out what kind of activation this is.
                AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
                AppNotificationManager.Default.Unregister();

                RedirectActivationTo(args, mainInstance);
            }

            return isRedirect;
        }

        // private static IntPtr redirectEventHandle = IntPtr.Zero;

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        private static void RedirectActivationTo(AppActivationArguments args, AppInstance mainInstance)
        {
            var redirectSemaphore = new Semaphore(0, 1);
            Task.Run(() =>
            {
                mainInstance.RedirectActivationToAsync(args).AsTask().Wait();
                redirectSemaphore.Release();
            });
            redirectSemaphore.WaitOne();
        }

        #endregion
    }
}
