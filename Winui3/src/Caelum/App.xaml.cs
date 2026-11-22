using Caelum.Core.Localization;
using Caelum.Services;
using Microsoft.UI.Xaml;

namespace Caelum;

/// <summary>
///     Process wide entry point. Owns the startup sequence, because a WinUI 3 window cannot be shown
///     until the asynchronous database work has finished.
/// </summary>
public partial class App
{
    private Window? _window;
    private Windows.GuideWindow? _guide;

    /// <summary>Creates the application.</summary>
    public App()
    {
        CrashLog.Install(this);
        InitializeComponent();
    }

    /// <summary>Opens whichever window the stored state calls for.</summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await AppServices.InitialiseAsync();

        // A first run is asked its three questions by a window of its own, and the shell is not built
        // until that window closes. Showing both at once would put an empty dashboard behind the
        // wizard, which reads as a second question rather than as the app being ready
        if (AppServices.Settings.Current.IsFirstLogin)
        {
            await RunOnboardingAsync();
            return;
        }

        OpenShell();
    }

    /// <summary>
    ///     Shows the onboarding wizard and waits for it to be dismissed, whether the student finished
    ///     it or skipped it. The wizard only hides itself when dismissed, so the shell opens while it
    ///     is still alive and the window is closed once the shell has settled.
    /// </summary>
    private async Task RunOnboardingAsync()
    {
        // The field is what keeps the wizard alive for as long as it is on screen
        _guide = new Windows.GuideWindow();

        _guide.Activate();

        // Dismissed rather than Closed: the wizard writes the first login flag as it goes, and the
        // shell must not start until that write has landed
        await _guide.Dismissed;

        OpenShell();

        // The wizard is hidden rather than destroyed when the shell opens. Tearing down its window
        // while the shell is activating fails fast in InputNonClientPointerSourceWinRT.PostDestroy
        // on Windows App SDK 2.6.5, so the close is delayed until the shell has settled.
        await Task.Delay(2000);

        _guide?.Close();

        _guide = null;
    }

    /// <summary>Creates and shows the main window.</summary>
    private void OpenShell()
    {
        // Restore the stored UI language before the shell is built, so every page resolves its
        // strings in the chosen language from the first frame instead of only after a restart.
        StringLocalizer.UseLanguage(AppServices.Settings.Current.SelectedLanguage);

        // Make sure the theme service has re-read the stored preference after the onboarding
        // wizard wrote it, so the shell renders the correct theme on the very first frame
        // instead of flashing the wrong one and correcting itself later.
        AppServices.Theme.Refresh();

        _window = new Windows.MainWindow();
        _window.Activate();
    }
}