using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DropMyShelf;

public partial class App : Application
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DropMyShelf";
    // Passed by the Windows startup entry, so the welcome balloon only shows on a manual launch.
    private const string StartupArgument = "--startup";
    // Passed by the installer when it restarts DropMyShelf after an in-app update.
    private const string UpdatedArgument = "--updated";
    // "DropMyShelf.exe --update" checks for a newer release and installs it right away.
    private const string UpdateArgument = "--update";

    private Mutex? _singleInstance;
    private MouseShakeHook? _hook;
    private Forms.NotifyIcon? _tray;
    // What clicking the current notification does, if anything.
    private Action? _notificationClick;
    private DispatcherTimer? _updateTimer;
    private bool _installingUpdate;

    /// <summary>A newer release found on GitHub, or null.</summary>
    internal UpdateInfo? AvailableUpdate { get; private set; }
    private readonly List<ShelfWindow> _shelves = new();
    private LibraryWindow? _library;
    private DateTime _libraryClosedAt;

    internal Settings Settings { get; private set; } = new();
    internal ShelfHistory History { get; set; } = new();
    internal IReadOnlyList<ShelfWindow> OpenShelves => _shelves;
    // A shelf created ahead of time, so shaking shows one instantly instead of building a window.
    private ShelfWindow? _spare;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "DropMyShelf.SingleInstance", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }

        // A tray app has no window to show an error in: log it and keep running instead of vanishing.
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };

        MoveDataFromOldName();
        Settings = Settings.Load();
        History = ShelfHistory.Load(Settings.HistorySize);
        PrepareSpareShelf();

        _hook = new MouseShakeHook();
        // Hook events arrive on the hook's own thread: hand them over to the UI thread.
        _hook.Shaken += () => Dispatcher.BeginInvoke(() => OpenShelfAtCursor(autoClose: true));
        _hook.ButtonReleased += () => Dispatcher.BeginInvoke(ScheduleEmptyShelfCleanup);
        _hook.Start();

        CreateTrayIcon();
        if (e.Args.Contains(UpdatedArgument))
        {
            UpdateChecker.DeleteDownloadedInstallers();
            Notify($"DropMyShelf updated to {UpdateChecker.CurrentVersion}", "Your shelves and settings were kept.");
        }
        else if (!Settings.WelcomeShown && !e.Args.Contains(StartupArgument))
        {
            // Once only: the Store build can't pass --startup, so a start with Windows looks like any other.
            Settings.WelcomeShown = true;
            Settings.Save();
            Notify("DropMyShelf is running", "Drag a file and shake the mouse to bring up a shelf.");
        }

        // The Store updates its apps itself and doesn't allow them to install their own updates.
        if (PackageInfo.IsPackaged)
            return;
        if (e.Args.Contains(UpdateArgument))
            UpdateNow();
        else if (Settings.AutoCheckUpdates)
            StartUpdateTimer();
    }

    private async void UpdateNow()
    {
        await CheckForUpdatesAsync(manual: true);
        if (AvailableUpdate != null)
            InstallUpdate();
    }

    private void Notify(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info, Action? onClick = null)
    {
        _notificationClick = onClick;
        _tray?.ShowBalloonTip(5000, title, text, icon);
    }

    // ---- Updates ----

    private void StartUpdateTimer()
    {
        // First check shortly after startup so it doesn't compete with launching, then once a day.
        _updateTimer ??= new DispatcherTimer();
        _updateTimer.Interval = TimeSpan.FromSeconds(15);
        _updateTimer.Tick -= OnUpdateTimer;
        _updateTimer.Tick += OnUpdateTimer;
        _updateTimer.Start();
    }

    private async void OnUpdateTimer(object? sender, EventArgs e)
    {
        _updateTimer!.Interval = TimeSpan.FromHours(24);
        await CheckForUpdatesAsync(manual: false);
    }

    internal void SetAutoCheckUpdates(bool enabled)
    {
        Settings.AutoCheckUpdates = enabled;
        Settings.Save();
        if (enabled)
            StartUpdateTimer();
        else
            _updateTimer?.Stop();
    }

    internal async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            AvailableUpdate = await UpdateChecker.CheckAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            // Being offline is normal; only say something when the user asked.
            if (manual)
                Notify("Couldn't check for updates", "Check your internet connection and try again.", Forms.ToolTipIcon.Warning);
            return;
        }

        if (AvailableUpdate is not { } update)
        {
            if (manual)
                Notify("DropMyShelf is up to date", $"You have the latest version ({UpdateChecker.CurrentVersion}).");
            return;
        }

        // Announce each release once, unless the user is asking right now.
        string version = update.Version.ToString();
        if (manual || Settings.NotifiedVersion != version)
        {
            Settings.NotifiedVersion = version;
            Settings.Save();
            Notify($"DropMyShelf {version} is available", "Click here to update. Your shelves and settings are kept.",
                onClick: InstallUpdate);
        }
    }

    /// <summary>Downloads the new installer, runs it silently and exits; the installer restarts DropMyShelf.</summary>
    internal async void InstallUpdate()
    {
        if (AvailableUpdate is not { } update || _installingUpdate)
            return;
        _installingUpdate = true;
        Notify("Updating DropMyShelf", $"Downloading version {update.Version}…");
        try
        {
            string installer = await UpdateChecker.DownloadAsync(update);
            Process.Start(new ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /update=1")
            {
                UseShellExecute = true,
            });
            // Exit cleanly before the installer stops the process, so open shelves go to history.
            Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is ErrorSystemIntegrityPolicyViolation or ErrorAccessDisabledByPolicy)
        {
            // Smart App Control or another app-control policy refused to run the unsigned installer.
            LogError(ex);
            _installingUpdate = false;
            UpdateChecker.DeleteDownloadedInstallers();
            Notify("Windows blocked the update",
                $"Smart App Control or your security settings stopped the installer. Click here to get DropMyShelf {update.Version} from GitHub.",
                Forms.ToolTipIcon.Warning, () => OpenUrl(update.PageUrl));
        }
        catch (Exception ex)
        {
            LogError(ex);
            _installingUpdate = false;
            Notify("Update failed", "Click here to download it from GitHub instead.", Forms.ToolTipIcon.Warning,
                () => OpenUrl(update.PageUrl));
        }
    }

    // Win32 errors raised when an app-control policy (Smart App Control, WDAC, AppLocker) blocks a program.
    private const int ErrorSystemIntegrityPolicyViolation = 4551;
    private const int ErrorAccessDisabledByPolicy = 1260;

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    /// <summary>
    /// The app was called DropMe up to 1.3.2: on the first run under the new name, bring its history
    /// and settings along. Files saved in %TEMP%\DropMe stay put, since the history points at them.
    /// </summary>
    private static void MoveDataFromOldName()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string oldFolder = Path.Combine(appData, "DropMe");
        string newFolder = Path.Combine(appData, "DropMyShelf");
        if (!Directory.Exists(oldFolder) || Directory.Exists(newFolder))
            return;
        try
        {
            Directory.Move(oldFolder, newFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Old files are locked or unreadable: start fresh rather than fail.
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DropMyShelf");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "errors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Nowhere left to report it.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void PrepareSpareShelf()
    {
        // Two shelves taken in a row queue two calls; the second must not orphan the first spare.
        if (_spare != null)
            return;
        _spare = new ShelfWindow();
        _spare.Prewarm();
    }

    private ShelfWindow TakeShelf()
    {
        var shelf = _spare ?? new ShelfWindow();
        _spare = null;
        Dispatcher.BeginInvoke(PrepareSpareShelf, DispatcherPriority.ApplicationIdle);

        shelf.Closed += (_, _) =>
        {
            _shelves.Remove(shelf);
            History.Add(shelf.Items);
        };
        _shelves.Add(shelf);
        return shelf;
    }

    private void OpenShelfAtCursor(bool autoClose)
    {
        if (ShelfWindow.IsDraggingOut)
            return;

        var shelf = _shelves.FirstOrDefault(s => s.IsEmpty) ?? TakeShelf();
        shelf.AutoCloseIfEmpty = autoClose;
        shelf.ShowNearCursor();
    }

    internal void OpenNewShelf() => OpenShelfAtCursor(autoClose: false);

    internal void SetHistorySize(int size)
    {
        Settings.HistorySize = size;
        Settings.Save();
        History.Capacity = size;
    }

    /// <summary>Brings an open shelf next to the cursor.</summary>
    internal void BringShelf(ShelfWindow shelf) => shelf.ShowNearCursor();

    internal void ReopenFromHistory(ShelfHistory.Entry entry)
    {
        History.Remove(entry);
        if (!entry.Items.Any(p => File.Exists(p) || Directory.Exists(p)))
        {
            Notify("DropMyShelf", "These files no longer exist.", Forms.ToolTipIcon.Warning);
            return;
        }

        var shelf = TakeShelf();
        shelf.LoadItems(entry.Items);
        shelf.ShowNearCursor();
    }

    // When the user lets go of the mouse without dropping anything on a freshly
    // shaken-open shelf, it disappears. The delay lets the Drop event land first.
    private void ScheduleEmptyShelfCleanup()
    {
        // This runs on every click anywhere in Windows: bail out unless a shelf is waiting for a drop.
        if (!_shelves.Any(s => s.IsEmpty && s.AutoCloseIfEmpty))
            return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var shelf in _shelves.Where(s => s.IsEmpty && s.AutoCloseIfEmpty && !s.IsMouseOver).ToList())
                shelf.Close();
        };
        timer.Start();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) => BuildTrayMenu(menu);

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "DropMyShelf — shake the mouse while dragging",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
                ToggleLibrary();
        };
        _tray.BalloonTipClicked += (_, _) => _notificationClick?.Invoke();
        _tray.BalloonTipClosed += (_, _) => _notificationClick = null;
    }

    private void ToggleLibrary()
    {
        if (_library != null)
        {
            _library.Close();
            return;
        }
        // Clicking the icon while the window is open first deactivates (and closes) it;
        // that same click must not open it again.
        if ((DateTime.Now - _libraryClosedAt).TotalMilliseconds < 400)
            return;

        _library = new LibraryWindow(this);
        _library.Closed += (_, _) =>
        {
            _library = null;
            _libraryClosedAt = DateTime.Now;
        };
        _library.ShowFlyout();
    }

    private void BuildTrayMenu(Forms.ContextMenuStrip menu)
    {
        menu.Items.Clear();
        if (AvailableUpdate is { } update)
        {
            var install = new Forms.ToolStripMenuItem($"Update to {update.Version}", null, (_, _) => InstallUpdate());
            install.Font = new Drawing.Font(install.Font, Drawing.FontStyle.Bold);
            menu.Items.Add(install);
            menu.Items.Add(new Forms.ToolStripSeparator());
        }
        menu.Items.Add("Open DropMyShelf", null, (_, _) => ToggleLibrary());
        menu.Items.Add("New shelf", null, (_, _) => OpenNewShelf());
        if (_shelves.Count > 0)
        {
            menu.Items.Add("Close all shelves", null, (_, _) =>
            {
                foreach (var shelf in _shelves.ToList())
                    shelf.Close();
            });
        }
        menu.Items.Add(new Forms.ToolStripSeparator());

        var historySize = new Forms.ToolStripMenuItem("History size");
        foreach (int size in Settings.HistorySizeChoices)
        {
            int choice = size;
            historySize.DropDownItems.Add(new Forms.ToolStripMenuItem($"{choice} shelves", null, (_, _) => SetHistorySize(choice))
            {
                Checked = Settings.HistorySize == choice,
            });
        }
        menu.Items.Add(historySize);

        if (PackageInfo.IsPackaged)
        {
            menu.Items.Add(StartupSettingsLabel, null, (_, _) => OpenStartupSettings());
            menu.Items.Add(new Forms.ToolStripSeparator());
        }
        else
        {
            var startup = new Forms.ToolStripMenuItem("Start with Windows") { Checked = IsStartupEnabled() };
            startup.Click += (_, _) => SetStartupEnabled(!startup.Checked);
            menu.Items.Add(startup);

            var autoUpdate = new Forms.ToolStripMenuItem("Check for updates automatically") { Checked = Settings.AutoCheckUpdates };
            autoUpdate.Click += (_, _) => SetAutoCheckUpdates(!Settings.AutoCheckUpdates);
            menu.Items.Add(autoUpdate);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(manual: true));
        }
        menu.Items.Add("About DropMyShelf", null, (_, _) => ShowAbout());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
    }

    private static void ShowAbout()
    {
        var answer = MessageBox.Show(
            $"DropMyShelf {UpdateChecker.CurrentVersion}\nCopyright (c) 2026 JohanRouyer\n\n"
            + "Licensed under the PolyForm Noncommercial License 1.0.0: free for noncommercial use, "
            + "and this copyright notice must be kept when sharing it.\n\n"
            + "Open the project page on GitHub?",
            "About DropMyShelf", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
            OpenUrl(UpdateChecker.RepositoryUrl);
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/DropMyShelf.ico")).Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    internal const string StartupSettingsLabel = "Start with Windows…";

    /// <summary>
    /// The Store build starts with Windows through a package startup task, which Windows lets the
    /// user switch on or off in Settings > Apps > Startup.
    /// </summary>
    internal static void OpenStartupSettings() => OpenUrl("ms-settings:startupapps");

    internal static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    internal static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled && Environment.ProcessPath is string exe)
            key.SetValue(RunValueName, $"\"{exe}\" {StartupArgument}");
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }
}
