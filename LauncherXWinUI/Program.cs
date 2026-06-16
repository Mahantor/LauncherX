using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace LauncherXWinUI
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            bool isRedirect = DecideRedirection();

            if (!isRedirect)
            {
                // Continue launching application as normal if a second instance is not running
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }

            return 0;
        }

        /// <summary>
        /// Determines if an instance of LauncherX is already running, by registering a unique key.
        /// If the app is already running, we can then redirect the program launch to activate 
        /// the already running instance in Main()
        /// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
        /// </summary>
        /// <returns></returns>
        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("ClickPhaseLauncherX");

            if (keyInstance.IsCurrent)
            {
                // This is the main (and only) instance. The AppInstance.Activated event is
                // subscribed to inside App.OnLaunched (as MainInstance_Activated), where it has
                // access to the UI thread's DispatcherQueue and the MainWindow. Keeping the
                // handler there is what lets a second launch (e.g. the desktop shortcut) bring
                // the trayed-out window back to the foreground.
            }
            else
            {
                RedirectActivationTo(args, keyInstance);
                isRedirect = true;
            }

            return isRedirect;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes, bool bManualReset,
        bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(
            uint dwFlags, uint dwMilliseconds, ulong nHandles,
            IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IntPtr redirectEventHandle = IntPtr.Zero;

        /// <summary>
        /// Bring an existing instance of LauncherX into the foreground, and do not launch a second instance of LauncherX
        /// </summary>
        /// <param name="args">Activation args</param>
        /// <param name="keyInstance">The key used to check if LauncherX is already running, in DecideRedirection()</param>
        public static void RedirectActivationTo(AppActivationArguments args,
                                                AppInstance keyInstance)
        {
            redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
            Task.Run(() =>
            {
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
                SetEvent(redirectEventHandle);
            });

            uint CWMO_DEFAULT = 0;
            uint INFINITE = 0xFFFFFFFF;
            _ = CoWaitForMultipleObjects(
               CWMO_DEFAULT, INFINITE, 1,
               [redirectEventHandle], out uint handleIndex);

            // Bring the window to the foreground. The reliable path is the AppInstance.Activated
            // handler in App.xaml.cs (which can restore a hidden/trayed-out window); this
            // SetForegroundWindow is a best-effort fallback for when the window is already visible.
            // Guard against IntPtr.Zero so we don't fail when the window is hidden to the tray
            // (Process.MainWindowHandle is zero for a hidden window).
            try
            {
                Process process = Process.GetProcessById((int)keyInstance.ProcessId);
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(process.MainWindowHandle);
                }
            }
            catch
            {
                // Best-effort; the Activated handler is the authoritative path.
            }
        }

    }
}
