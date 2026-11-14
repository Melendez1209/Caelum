using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Caelum.Core.Localization;
using Caelum.Core.Services;
using Caelum.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Caelum.Pages;

/// <summary>
///     The conversation page for the AI assistant. The student's messages and the assistant's
///     replies appear as bubbles; the assistant streams its answer through the configured model,
///     and every turn is saved to the session the navigation pane is showing.
/// </summary>
public sealed partial class ProphetsPage
{
    private readonly StringLocalizer _strings = new();
    private readonly ObservableCollection<ChatBubble> _messages = [];

    /// <summary>
    ///     Cancels the pending reply when the page goes away, so an answer cannot land on a
    ///     conversation the student has already left. One source per request.
    /// </summary>
    private CancellationTokenSource? _replyCancellation;

    /// <summary>Sessions a title has already been requested for, so a quick second message cannot ask twice.</summary>
    private readonly HashSet<long> _titleRequested = [];

    /// <summary>Creates the page, binds the message list and translates the captions.</summary>
    public ProphetsPage()
    {
        InitializeComponent();

        TitleText.Text = _strings.Get("prophets");
        PromptBox.PlaceholderText = _strings.Get("ask_something");

        // The templates live in XAML so the bubbles can use theme resources; the selector picks
        // between them from the speaker flag carried by each message
        MessagesList.ItemTemplateSelector = new ChatTemplateSelector
        {
            UserTemplate = (DataTemplate)Resources["UserMessageTemplate"],
            AssistantTemplate = (DataTemplate)Resources["AssistantMessageTemplate"]
        };

        MessagesList.ItemsSource = _messages;

        // Attached with handled events so the control cannot swallow Enter before the handler runs:
        // the preview handler sends and stops the control inserting a paragraph break. Shift+Enter
        // is left alone, so the control drops a new line.
        PromptBox.AddHandler(
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnPromptPreviewKeyDown),
            handledEventsToo: true);

        // A keyboard accelerator is the reliable way to take Enter away from the text box: it is
        // invoked before the focused control sees the key, so the box never inserts the newline.
        var enter = new KeyboardAccelerator { Key = VirtualKey.Enter };
        enter.Invoked += OnEnterAcceleratorInvoked;
        PromptBox.KeyboardAccelerators.Add(enter);

        ApplyConfigurationState();
    }

    /// <inheritdoc />
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        await ReloadAsync().ConfigureAwait(true);

        // The settings page may have changed the provider while this page was away
        ApplyConfigurationState();
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _replyCancellation?.Cancel();
        _replyCancellation?.Dispose();
        _replyCancellation = null;

        base.OnNavigatedFrom(e);
    }

    /// <summary>
    ///     Reloads the current session's saved turns. The navigation shell calls this after it has
    ///     switched or deleted a session, so the page never has to listen for chat changes itself.
    /// </summary>
    public async Task ReloadAsync()
    {
        try
        {
            await AppServices.Chats.LoadAsync().ConfigureAwait(true);
            await LoadMessagesAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // A session that cannot be loaded leaves an empty page rather than a broken one
        }
    }

    private void OnPromptPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        // Shift+Enter drops a new line; Enter on its own sends
        if (IsShiftDown()) return;

        // Stop the control from inserting a paragraph break: the message is sent instead
        e.Handled = true;

        SendMessage();
    }

    private void OnEnterAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        // Shift+Enter still drops a new line; the accelerator is left unhandled so the text box
        // performs its normal insertion
        if (IsShiftDown()) return;

        args.Handled = true;

        SendMessage();
    }

    private void OnPromptTextChanged(object sender, RoutedEventArgs e) => UpdateSendState();

    private void OnSend(object sender, RoutedEventArgs e)
    {
        // While a reply is streaming the button is the stop control
        if (_replyCancellation is not null)
        {
            _replyCancellation.Cancel();
            return;
        }

        SendMessage();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SettingsPage));
    }

    /// <summary>Replaces the message list with the current session's saved turns.</summary>
    private async Task LoadMessagesAsync()
    {
        _messages.Clear();

        var session = AppServices.Chats.CurrentSession;

        if (session is null) return;

        var messages = await AppServices.Chats.GetMessagesAsync(session.Id).ConfigureAwait(true);

        foreach (var message in messages)
        {
            _messages.Add(new ChatBubble(
                message.Content,
                string.Equals(message.Role, "user", StringComparison.Ordinal)));
        }

        if (_messages.Count > 0)
            DispatcherQueue.TryEnqueue(() => MessagesList.ScrollIntoView(_messages[^1]));
    }

    /// <summary>
    ///     Shows the student's message at once, saves it, then replaces the pending bubble with the
    ///     model's answer as it arrives.
    /// </summary>
    private async void SendMessage()
    {
        if (!AppServices.Llm.IsConfigured) return;

        var session = AppServices.Chats.CurrentSession;

        if (session is null) return;

        // A reply is already on its way; the send button is disabled, so this is only reachable
        // through the Enter accelerator
        if (_replyCancellation is not null) return;

        var sessionId = session.Id;

        var text = PromptBox.Text;

        var trimmed = text.Trim();

        if (string.IsNullOrEmpty(trimmed)) return;

        var userText = NormalizeLineEndings(trimmed);

        AddMessage(new ChatBubble(userText, isUser: true));

        PromptBox.Text = string.Empty;

        try
        {
            await AppServices.Chats.AddMessageAsync(sessionId, "user", userText).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // The bubble stays on screen; the next send will try to persist again
        }

        var pending = new ChatBubble(string.Empty, isUser: false, isPending: true);

        AddMessage(pending);

        var history = _messages
            .Where(message => !message.IsPending)
            .Select(message => new LlmMessage(message.IsUser ? "user" : "assistant", message.Text))
            .ToList();

        var progress = new Progress<string>(delta =>
        {
            pending.Text += delta;
            pending.RaisePropertyChanged(nameof(pending.Text));
        });

        var cancellation = new CancellationTokenSource();
        _replyCancellation = cancellation;

        UpdateSendState();

        try
        {
            var result = await AppServices.Llm.CompleteAsync(
                history,
                progress,
                cancellation.Token).ConfigureAwait(true);

            pending.IsPending = false;
            pending.Text = result.Content;
            pending.RaisePropertyChanged(nameof(pending.IsPending));
            pending.RaisePropertyChanged(nameof(pending.Text));

            if (result.IsSuccess)
            {
                await SaveAssistantMessageAsync(sessionId, result.Content).ConfigureAwait(true);
            }
            else
            {
                // A partial answer is worth keeping, and the error is saved as its own turn
                if (!string.IsNullOrEmpty(result.Content))
                    await SaveAssistantMessageAsync(sessionId, result.Content).ConfigureAwait(true);

                var errorText = LocalizeLlmError(result.ErrorKind);

                AddMessage(new ChatBubble(errorText, isUser: false));

                await SaveAssistantMessageAsync(sessionId, errorText).ConfigureAwait(true);
            }

            await MaybeGenerateTitleAsync(sessionId, userText).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page went away before the answer was complete
            _messages.Remove(pending);
        }
        catch (Exception)
        {
            _messages.Remove(pending);

            var errorText = _strings.Get("llm_error_generic");

            AddMessage(new ChatBubble(errorText, isUser: false));

            await SaveAssistantMessageAsync(sessionId, errorText).ConfigureAwait(true);
        }
        finally
        {
            cancellation.Dispose();

            // A newer send may have replaced the field; only clear it when it still points here
            if (ReferenceEquals(_replyCancellation, cancellation))
                _replyCancellation = null;

            UpdateSendState();
        }
    }

    private async Task SaveAssistantMessageAsync(long sessionId, string content)
    {
        try
        {
            await AppServices.Chats.AddMessageAsync(sessionId, "assistant", content).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Persistence is the best effort; the bubble is already on screen
        }
    }

    /// <summary>
    ///     Names a session from its first message once the reply is in. The model's summary is used
    ///     when it answers, otherwise the message itself is truncated.
    /// </summary>
    private async Task MaybeGenerateTitleAsync(long sessionId, string firstMessage)
    {
        var session = AppServices.Chats.CurrentSession;

        if (session is null || session.Id != sessionId) return;

        if (!string.IsNullOrWhiteSpace(session.Title)) return;

        if (!_titleRequested.Add(sessionId)) return;

        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var title = await AppServices.Llm
                .GenerateTitleAsync(firstMessage, cancellation.Token)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(title))
                title = firstMessage.Length > 40 ? firstMessage[..40] : firstMessage;

            await AppServices.Chats.RenameAsync(sessionId, title, cancellation.Token).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Title generation is the best effort; the default title stays
        }
    }

    private void ApplyConfigurationState()
    {
        var configured = AppServices.Llm.IsConfigured;

        SetupBanner.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
        SetupBannerText.Text = _strings.Get("llm_not_configured");
        OpenSettingsButton.Content = _strings.Get("llm_open_settings");

        UpdateSendState();
    }

    /// <summary>Puts line endings on a common footing so the bubbles render them consistently.</summary>
    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    private void AddMessage(ChatBubble message)
    {
        _messages.Add(message);

        // The new row may not be realised yet, so the scroll is queued until after the layout pass
        DispatcherQueue.TryEnqueue(() => MessagesList.ScrollIntoView(message));
    }

    private void UpdateSendState()
    {
        var text = PromptBox.Text;
        var replying = _replyCancellation is not null;

        SendButton.IsEnabled =
            replying ||
            (!string.IsNullOrWhiteSpace(text) &&
             AppServices.Llm.IsConfigured &&
             AppServices.Chats.CurrentSession is not null);

        // The same button cancels a streaming reply, so its glyph and tip follow the state
        SendButtonIcon.Glyph = replying ? "\uE71A" : "\uE724";
        ToolTipService.SetToolTip(SendButton, _strings.Get(replying ? "stop" : "send"));
    }

    private string LocalizeLlmError(LlmErrorKind kind) => kind switch
    {
        LlmErrorKind.NotConfigured => _strings.Get("llm_not_configured"),
        LlmErrorKind.Unauthorized => _strings.Get("llm_error_unauthorized"),
        LlmErrorKind.NotFound => _strings.Get("llm_error_not_found"),
        LlmErrorKind.RateLimited => _strings.Get("llm_error_rate_limited"),
        LlmErrorKind.Network => _strings.Get("llm_error_network"),
        LlmErrorKind.Cancelled => _strings.Get("llm_error_cancelled"),
        _ => _strings.Get("llm_error_generic"),
    };

    /// <summary>True while Shift is held, which turns Enter into a newline rather than a sending.</summary>
    private static bool IsShiftDown()
    {
        // The state arrives as a flags enum over the key asked for, so zero means up
        return (int)InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) != 0;
    }

    /// <summary>One turn of the conversation. Streaming replies raise change notifications.</summary>
    private sealed class ChatBubble(string text, bool isUser, bool isPending = false) : INotifyPropertyChanged
    {
        public string Text { get; set; } = text;
        public bool IsUser { get; } = isUser;
        public bool IsPending { get; set; } = isPending;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Picks the bubble template that matches the speaker.</summary>
    private sealed class ChatTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? UserTemplate { get; init; }

        public DataTemplate? AssistantTemplate { get; init; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            return item is ChatBubble { IsUser: true } ? UserTemplate : AssistantTemplate;
        }
    }
}
