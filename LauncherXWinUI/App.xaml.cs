using LauncherXWinUI.Classes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.Windows.AppLifecycle;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LauncherXWinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Tray icon for LauncherX to run in background
        /// </summary>
        private static TrayIcon AppTrayIcon;

        /// <summary>
        /// MainWindow instance
        /// </summary>
        public static MainWindow MainWindow;

        /// <summary>
        /// To register system hot keys to activate LauncherX
        /// </summary>
        public static HotKeyHook ActivationHotKeyHook;

        /// <summary>
        /// Set to true when the user actually wants to quit LauncherX (via Tray -> Quit LauncherX).
        /// While false, closing the MainWindow only hides it to the tray.
        /// </summary>
        public static bool IsExiting = false;

        /// <summary>
        /// DispatcherQueue of the UI thread, used to marshal activation callbacks back to the UI thread.
        /// </summary>
        private static DispatcherQueue _uiDispatcher;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Configure FilePersistence for WinUIEx to save Window position in Unpackaged apps https://github.com/dotMorten/WinUIEx/issues/61
            WinUIEx.WindowManager.PersistenceStorage = new FilePersistence(Path.Combine(UserSettingsClass.SettingsDir, "windowPlace.json"));

            // Create an instance of HotKeyHook to watch out for activation shortcuts,
            // and create a new event handler for when the activation shortcut (hotkey) is triggered
            ActivationHotKeyHook = new HotKeyHook(0);
            ActivationHotKeyHook.KeyPressed += ActivationHotKeyHook_KeyPressed;

            // Capture the UI thread's DispatcherQueue so activation callbacks (which arrive on a
            // thread-pool thread) can be marshalled back onto the UI thread before touching windows.
            _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        }
       
        /// <summary>
        /// Creates a new MainWindow (if applicable) and activates the MainWindow
        /// </summary>
        public void GetMainWindow()
        {
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
                MainWindow.Activate();
                return;
            }

            // Try to just activate the window, if it fails, create a new instance
            try
            {
                MainWindow.Activate();
            }
            catch
            {
                MainWindow = new MainWindow();
                MainWindow.Activate();
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Register a tray icon
            AppTrayIcon = new TrayIcon(1, "Resources\\icon.ico", "LauncherX");
            AppTrayIcon.IsVisible = true;
            AppTrayIcon.Selected += (s, e) =>
            {
                GetMainWindow();
                BringMainWindowToFront();
            };
            // A bit messy, but its just UI stuff, so who cares?
            AppTrayIcon.ContextMenu += (w, e) =>
            {
                var flyout = new MenuFlyout();
                flyout.Items.Add(new MenuFlyoutItem() 
                { 
                    Text = "Open LauncherX",
                    Height = 36,
                    Icon = new FontIcon() 
                    { 
                        Glyph="\uE8A7"
                    } 
                });
                ((MenuFlyoutItem)flyout.Items[0]).Click += (s, e) =>
                {
                    GetMainWindow();
                    BringMainWindowToFront();
                };

                flyout.Items.Add(new MenuFlyoutItem() 
                { 
                    Text = "Quit LauncherX", 
                    Height = 36,
                    Icon = new FontIcon()
                    {
                        Glyph = "\uE711"
                    }
                });
                ((MenuFlyoutItem)flyout.Items[1]).Click += (s, e) =>
                {
                    ExitApplication();
                };
                e.Flyout = flyout;
            };

            // Launch MainWindow
            GetMainWindow();

            // Subscribe to the AppInstance.Activated event. When a second instance is launched
            // (e.g. the user double-clicks the desktop shortcut while LauncherX is already running
            // in the tray), it redirects its activation to this main instance, which raises this
            // event. We use it to bring the hidden window back to the foreground.
            AppInstance.GetCurrent().Activated += MainInstance_Activated;
        }

        /// <summary>
        /// Raised when a second instance redirects its activation to this (main) instance.
        /// This is how the desktop shortcut can bring the trayed-out window back up.
        /// </summary>
        private void MainInstance_Activated(object sender, AppActivationArguments args)
        {
            if (_uiDispatcher == null)
                return;

            // The event fires on a non-UI thread; marshal onto the UI thread.
            _uiDispatcher.TryEnqueue(() =>
            {
                GetMainWindow();
                BringMainWindowToFront();
            });
        }

        /// <summary>
        /// Restores the MainWindow from the tray and brings it to the foreground.
        /// Handles the case where the window was previously hidden via Close -> Hide().
        /// </summary>
        public static void BringMainWindowToFront()
        {
            if (MainWindow == null)
                return;

            // Ensure the window is visible (it may have been hidden to the tray).
            MainWindow.Show();
            MainWindow.Activate();

            // Bring the underlying HWND to the foreground. WinUI's Activate() alone is not
            // reliable when the window was hidden, so we additionally use SetForegroundWindow.
            try
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                if (hwnd != IntPtr.Zero)
                {
                    // Restore if minimised, then bring to front.
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
            }
            catch
            {
                // Bringing to foreground is best-effort; never let it crash activation.
            }
        }

        // Win32 helpers used to reliably bring the window to the foreground.
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        // Activate LauncherX when hot key is triggered
        private void ActivationHotKeyHook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            GetMainWindow();
            BringMainWindowToFront();
        }

        /// <summary>
        /// Exits the application and cleans up the necessary objects
        /// </summary>
        public static void ExitApplication()
        {
            // Signal that this is a genuine exit so the MainWindow.Closed handler lets the
            // window actually close (instead of hiding to the tray).
            IsExiting = true;

            AppTrayIcon.Dispose();
            ActivationHotKeyHook.Dispose();

            // Close the MainWindow so its Closed handler runs the final clean-up
            // (saving items and disposing the MultiFileSystemWatcher).
            MainWindow?.Close();

            Application.Current.Exit();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder packageFullName);

        private class FilePersistence : IDictionary<string, object>
        {
            private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
            private readonly string _file;

            public FilePersistence(string filename)
            {
                _file = filename;
                try
                {
                    if (File.Exists(filename))
                    {
                        var jo = System.Text.Json.Nodes.JsonObject.Parse(File.ReadAllText(filename)) as JsonObject;
                        foreach (var node in jo)
                        {
                            if (node.Value is JsonValue jvalue && jvalue.TryGetValue<string>(out string value))
                                _data[node.Key] = value;
                        }
                    }
                }
                catch { }
            }
            private void Save()
            {
                JsonObject jo = new JsonObject();
                foreach (var item in _data)
                {
                    if (item.Value is string s) // In this case we only need string support. TODO: Support other types
                        jo.Add(item.Key, s);
                }
                File.WriteAllText(_file, jo.ToJsonString());
            }
            public object this[string key] { get => _data[key]; set { _data[key] = value; Save(); } }

            public ICollection<string> Keys => _data.Keys;

            public ICollection<object> Values => _data.Values;

            public int Count => _data.Count;

            public bool IsReadOnly => false;

            public void Add(string key, object value)
            {
                _data.Add(key, value); Save();
            }

            public void Add(KeyValuePair<string, object> item)
            {
                _data.Add(item.Key, item.Value); Save();
            }

            public void Clear()
            {
                _data.Clear(); Save();
            }

            public bool Contains(KeyValuePair<string, object> item) => _data.Contains(item);

            public bool ContainsKey(string key) => _data.ContainsKey(key);

            public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new NotImplementedException(); // TODO

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw new NotImplementedException(); // TODO

            public bool Remove(string key) => throw new NotImplementedException(); // TODO

            public bool Remove(KeyValuePair<string, object> item) => throw new NotImplementedException(); // TODO

            public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) => throw new NotImplementedException(); // TODO

            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException(); // TODO
        }
    }
}
