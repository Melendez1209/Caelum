using System.Globalization;
using Caelum.Core.Data;
using Caelum.Core.Localization;
using Caelum.Pages;
using Caelum.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Caelum.Windows;

/// <summary>
///     The application shell.
/// </summary>
/// <remarks>
///     A <see cref="TitleBar" /> sits above a <see cref="NavigationView" />, which is the arrangement the
///     Windows App SDK recommends: the TitleBar owns the drag region, the caption buttons and the
///     back button, while the NavigationView drives a single <see cref="Frame" /> for all pages.
///     This matches the WinUI Notes sample and the Fluent Design navigation guidance.
/// </remarks>
public sealed partial class MainWindow
{
    private readonly StringLocalizer _strings = new();
    private readonly ThemeService _themeService;

    /// <summary>The last session list rendered into the pane, so unchanged writes do not rebuild it.</summary>
    private string? _builtSignature;

    /// <summary>Set while the selection is changed from code, to keep the handler from re-entering.</summary>
    private bool _suppressSelection;

    /// <summary>Creates the window, wires the shell up and opens the landing page.</summary>
    public MainWindow()
    {
        InitializeComponent();

        // Defensive: the shell is only opened after AppServices.InitialiseAsync() has completed,
        // but guard against a null theme service in case initialisation order ever changes.
        _themeService = AppServices.Theme ?? throw new InvalidOperationException(
            $"{nameof(AppServices)}.{nameof(AppServices.Theme)} has not been initialised yet.");

        _themeService.ThemeChanged += OnThemeChanged;

        AppTitleBar.Title = _strings.Get("app_name");
        TitleSearchBox.PlaceholderText = _strings.Get("search_hint");

        // Both of these only take effect from code. Setting ExtendsContentIntoTitleBar from XAML is
        // an error, and doing it late would flash the system title bar before replacing it
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // The bar carries interactive content (the search box), for which the Fluent guidance is a
        // taller title bar. The system caption buttons are rendered at the standard height otherwise,
        // so they would fill only the top of the bar and their touch target would not match it.
        // PreferredHeightOption requires ExtendsContentIntoTitleBar to be set first
        if (AppWindowTitleBar.IsCustomizationSupported())
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        DesktopShare.Initialise(this);

        ApplyCaptions();

        // The session list under the assistant tab follows the stored conversations
        AppServices.Chats.Reloaded += OnChatsReloaded;
        BuildSessions();

        // Start on the dashboard. The navigation item below is selected to match.
        MainFrame.Navigate(typeof(HomePage));

        if (Navigation.MenuItems.FirstOrDefault() is NavigationViewItem firstItem) Navigation.SelectedItem = firstItem;

        // Keep the NavigationView selection in step with the frame, including when the student
        // taps the back button and the frame unwinds to a previous page.
        MainFrame.Navigated += OnMainFrameNavigated;

        // The first navigation happened before the handler above was attached, so the title bar's
        // back button needs its initial state set by hand.
        ApplyBackButton();

        Closed += OnClosed;
        Activated += OnWindowActivated;
        ApplyTheme();
    }

    /// <summary>
    ///     Coming back to the window is the moment the system theme can have changed underneath a
    ///     "follow system" preference, since nothing raised a notification while the window was idle.
    /// </summary>
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) return;

        _themeService.Refresh();
    }

    private void ApplyCaptions()
    {
        HomeItem.Content = _strings.Get("home");
        HistoryItem.Content = _strings.Get("history");
        ProphetsItem.Content = _strings.Get("prophets");
        NewSessionItem.Content = _strings.Get("chat_new_session");
        // The pane's settings item is built by NavigationView itself and comes with the framework's
        // own caption, which would keep answering in the shell language after the student switches.
        // SettingsItem is typed object here, as opposed to the NavigationViewItem it really is
        if (Navigation.SettingsItem is NavigationViewItem settingsItem)
            settingsItem.Content = _strings.Get("settings");
    }

    // ------------------------------------------------------------------
    // Assistant sessions
    // ------------------------------------------------------------------

    private void OnChatsReloaded(object? sender, EventArgs e)
    {
        BuildSessions();
    }

    /// <summary>
    ///     Rebuilds the session entries under the assistant tab. The new-chat entry stays at the top,
    ///     and the sessions are inserted below it in the order the service reports (most recent first).
    /// </summary>
    private void BuildSessions()
    {
        var sessions = AppServices.Chats.Sessions;

        // The service raises Reloaded for every write, but the pane only shows ids and titles, so a
        // rebuild is skipped until one of those actually changes
        var signature = string.Join(
            "|",
            sessions.Select(session => $"{session.Id}:{session.Title}"));

        if (signature == _builtSignature) return;

        _builtSignature = signature;

        // Drop the previous session entries, keeping the new-chat entry that sits at the top
        while (ProphetsItem.MenuItems.Count > 1)
            ProphetsItem.MenuItems.RemoveAt(1);

        for (var index = sessions.Count - 1; index >= 0; index--)
        {
            var session = sessions[index];

            var item = new NavigationViewItem
            {
                Content = string.IsNullOrWhiteSpace(session.Title)
                    ? _strings.Get("chat_title_default")
                    : session.Title,
                Tag = $"Session:{session.Id}",
                Icon = new SymbolIcon(Symbol.Message)
            };

            var delete = new MenuFlyoutItem
            {
                Text = _strings.Get("chat_delete_session"),
                Tag = session.Id
            };

            delete.Click += OnDeleteSessionClick;

            var flyout = new MenuFlyout();
            flyout.Items.Add(delete);

            item.ContextFlyout = flyout;

            ProphetsItem.MenuItems.Insert(1, item);
        }

        RestoreSessionSelection();
    }

    /// <summary>
    ///     Puts the pane selection back on the entry for the current session after the entries have
    ///     been rebuilt, because removing the selected item clears the highlight.
    /// </summary>
    private void RestoreSessionSelection()
    {
        var current = AppServices.Chats.CurrentSession;

        if (current is null) return;

        foreach (var item in ProphetsItem.MenuItems)
        {
            if (item is NavigationViewItem child &&
                string.Equals(child.Tag as string, $"Session:{current.Id}", StringComparison.Ordinal))
            {
                SetSelectedItem(child);
                return;
            }
        }
    }

    private void SetSelectedItem(NavigationViewItem item)
    {
        if (ReferenceEquals(Navigation.SelectedItem, item)) return;

        _suppressSelection = true;

        Navigation.SelectedItem = item;

        _suppressSelection = false;
    }

    private async Task NewSessionAsync()
    {
        await AppServices.Chats.CreateAsync().ConfigureAwait(true);

        MainFrame.Navigate(typeof(ProphetsPage));
    }

    private async Task SwitchSessionAsync(long sessionId)
    {
        await AppServices.Chats.SwitchAsync(sessionId).ConfigureAwait(true);

        if (MainFrame.Content is ProphetsPage page)
            await page.ReloadAsync().ConfigureAwait(true);
        else
            MainFrame.Navigate(typeof(ProphetsPage));
    }

    private async void OnDeleteSessionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not long sessionId) return;

        var dialog = new ContentDialog
        {
            Title = _strings.Get("attention"),
            Content = _strings.Get("chat_delete_confirm"),
            PrimaryButtonText = _strings.Get("delete"),
            CloseButtonText = _strings.Get("cancel"),
            XamlRoot = MainFrame.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await AppServices.Chats.DeleteAsync(sessionId).ConfigureAwait(true);

        BuildSessions();

        if (MainFrame.Content is ProphetsPage page)
            await page.ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     Picks the navigation entry that represents the conversation the chat page is showing, so
    ///     the pane highlights the current session rather than only the assistant tab.
    /// </summary>
    private NavigationViewItem ResolveProphetsItem()
    {
        var currentId = AppServices.Chats.CurrentSession?.Id;

        if (currentId is not null)
        {
            foreach (var item in ProphetsItem.MenuItems)
            {
                if (item is NavigationViewItem child &&
                    string.Equals(child.Tag as string, $"Session:{currentId.Value}", StringComparison.Ordinal))
                    return child;
            }
        }

        return ProphetsItem;
    }

    /// <summary>
    ///     Stores the exam search query typed into the title bar and refreshes the suggestion
    ///     list. The history page listens for the change, so the box can live in the shell and
    ///     still drive the list.
    /// </summary>
    private void OnTitleSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        AppServices.Search.SetQuery(sender.Text);
        UpdateSearchSuggestions(sender);
    }

    /// <summary>
    ///     Fills the suggestion list with the exams whose name matches the typed text, the ones
    ///     starting with it first. A blank query offers nothing, so the list stays shut until the
    ///     student types.
    /// </summary>
    private void UpdateSearchSuggestions(AutoSuggestBox sender)
    {
        var query = sender.Text.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            sender.ItemsSource = null;
            return;
        }

        sender.ItemsSource = AppServices.Exams.Exams
            .Where(exam => exam.Exam.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(exam => exam.Exam.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(exam => exam.Exam.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .ToList();
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not ExamWithTotal exam) return;

        OpenExam(exam);
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Enter opens an exam the student actually highlighted in the list; a plain Enter on
        // typed text leaves the history filter alone
        if (args.ChosenSuggestion is not ExamWithTotal exam) return;

        OpenExam(exam);
    }

    /// <summary>
    ///     Opens the chosen exam and leaves the box showing its name, so the query and the list
    ///     behind the detail page agree.
    /// </summary>
    private void OpenExam(ExamWithTotal exam)
    {
        AppServices.Search.SetQuery(exam.Exam.Name);
        TitleSearchBox.Text = exam.Exam.Name;

        MainFrame.Navigate(typeof(DetailPage), exam.Exam.Id);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Content is not FrameworkElement root) return;

        root.RequestedTheme = _themeService.ShouldUseAppTheme
            ? ElementTheme.Dark
            : ElementTheme.Light;
    }

    /// <summary>The pane toggle lives in the title bar, so the handler lives here too.</summary>
    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        Navigation.IsPaneOpen = !Navigation.IsPaneOpen;
    }

    /// <summary>
    ///     Handles the back button in the TitleBar. Unwinds the main frame; when there is nowhere
    ///     left to go the window closes, which is what the system back gesture does on an app that
    ///     has nowhere left to go.
    /// </summary>
    private void OnBackRequested(TitleBar sender, object args)
    {
        if (MainFrame.CanGoBack)
            MainFrame.GoBack();
        else
            Close();
    }

    /// <summary>
    ///     The pane toggle has no meaning once the NavigationView flattens its items into a horizontal
    ///     strip at the top, which it does on a narrow window.
    /// </summary>
    private void OnDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.IsPaneToggleButtonVisible =
            Navigation.PaneDisplayMode != NavigationViewPaneDisplayMode.Top;
    }

    /// <summary>
    ///     Routes a pane selection. Every tag is matched explicitly: a catch-all would quietly send
    ///     a misspelt or newly added item to the dashboard instead of showing that it is unhandled.
    /// </summary>
    private async void OnTabSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelection) return;

        // NavigationView contributes a settings item of its own, below the footer items and shown
        // without being asked for, because IsSettingsVisible is true by default. The control builds
        // it, so it belongs to neither collection and carries no Tag, which leaves the switch below
        // unable to see it. IsSettingsSelected is the flag raised for exactly that item, and the one
        // the WinUI Gallery sample tests; it is asked before the cast because the settings item is
        // not one of ours. Without it the pane offers a settings button that does nothing at all.
        if (args.IsSettingsSelected)
        {
            MainFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;

        switch (item.Tag as string)
        {
            case "History":
                MainFrame.Navigate(typeof(HistoryPage));
                break;

            case "Home":
                MainFrame.Navigate(typeof(HomePage));
                break;

            case "Prophets":
                MainFrame.Navigate(typeof(ProphetsPage));
                break;

            case "NewSession":
                await NewSessionAsync().ConfigureAwait(true);
                break;

            default:
                // A session entry carries its id in the tag; anything else is left alone so a
                // misspelt tag cannot quietly navigate to the dashboard
                if (item.Tag is string tag &&
                    tag.StartsWith("Session:", StringComparison.Ordinal) &&
                    long.TryParse(
                        tag["Session:".Length..],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var sessionId))
                {
                    // The pane sets the selection back to the current session after a rebuild, which
                    // raises this event again; that is not a request to switch
                    if (AppServices.Chats.CurrentSession?.Id == sessionId) return;

                    await SwitchSessionAsync(sessionId).ConfigureAwait(true);
                }

                break;
        }
    }

    /// <summary>
    ///     Keeps the NavigationView selection in step with the frame, including when the student
    ///     taps the back button and the frame unwinds to a previous page. Child pages such as
    ///     DetailPage or InputtingPage keep the current selection because they belong to the tab
    ///     that opened them.
    /// </summary>
    private void OnMainFrameNavigated(object sender, NavigationEventArgs e)
    {
        var page = MainFrame.Content;

        var selected = page switch
        {
            HomePage => HomeItem,
            HistoryPage => HistoryItem,
            ProphetsPage => ResolveProphetsItem(),
            SettingsPage when Navigation.SettingsItem is NavigationViewItem settingsItem => settingsItem,
            _ => Navigation.SelectedItem as NavigationViewItem
        };

        if (selected is not null)
            SetSelectedItem(selected);

        ApplyBackButton();
    }

    /// <summary>
    ///     Keeps the title bar's back button in step with the frame. Called on every navigation,
    ///     because <see cref="Frame.CanGoBack" /> raises no change notification a binding could use.
    /// </summary>
    private void ApplyBackButton()
    {
        AppTitleBar.IsBackButtonVisible = MainFrame.CanGoBack;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _themeService.ThemeChanged -= OnThemeChanged;
        AppServices.Chats.Reloaded -= OnChatsReloaded;

        AppServices.Shutdown();
    }
}
