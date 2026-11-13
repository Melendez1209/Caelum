using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Caelum.Core.Localization;
using Caelum.Core.Services;
using Caelum.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Caelum.Pages;

/// <summary>
///     The conversation page for the AI assistant. The student's messages and the assistant's
///     replies appear as bubbles; the assistant streams its answer through the configured model.
/// </summary>
public sealed partial class ProphetsPage
{
    private readonly StringLocalizer _strings = new();
    private readonly ObservableCollection<ChatMessage> _messages = [];

    /// <summary>
    ///     Cancels the pending reply when the page goes away, so an answer cannot land on a
    ///     conversation the student has already left. One source per request.
    /// </summary>
    private CancellationTokenSource? _replyCancellation;

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

        ApplyConfigurationState();
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

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

    private void OnPromptPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        // Shift+Enter drops a new line; Enter on its own sends
        if (IsShiftDown()) return;

        // Stop the control from inserting a paragraph break: the message is sent instead
        e.Handled = true;

        SendMessage();
    }

    private void OnPromptTextChanged(object sender, RoutedEventArgs e) => UpdateSendState();

    private void OnSend(object sender, RoutedEventArgs e) => SendMessage();

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SettingsPage));
    }

    /// <summary>
    ///     Shows the student's message at once, then replaces the pending bubble with the model's
    ///     answer as it arrives.
    /// </summary>
    private async void SendMessage()
    {
        if (!AppServices.Llm.IsConfigured) return;

        PromptBox.Document.GetText(TextGetOptions.None, out var text);

        var trimmed = text.Trim();

        if (string.IsNullOrEmpty(trimmed)) return;

        AddMessage(new ChatMessage(NormalizeLineEndings(trimmed), isUser: true));

        PromptBox.Document.SetText(TextSetOptions.None, string.Empty);

        var pending = new ChatMessage(string.Empty, isUser: false, isPending: true);

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

        _replyCancellation?.Cancel();
        _replyCancellation?.Dispose();
        _replyCancellation = new CancellationTokenSource();

        UpdateSendState();

        try
        {
            var result = await AppServices.Llm.CompleteAsync(
                history,
                progress,
                _replyCancellation.Token);

            pending.IsPending = false;
            pending.Text = result.Content;
            pending.RaisePropertyChanged(nameof(pending.IsPending));
            pending.RaisePropertyChanged(nameof(pending.Text));

            if (!result.IsSuccess)
            {
                // A partial answer is worth keeping; without one the pending bubble is simply dropped
                if (string.IsNullOrEmpty(result.Content))
                    _messages.Remove(pending);

                AddMessage(new ChatMessage(LocalizeLlmError(result.ErrorKind), isUser: false));
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away before the answer was complete
            _messages.Remove(pending);
        }
        catch (Exception)
        {
            _messages.Remove(pending);

            AddMessage(new ChatMessage(_strings.Get("llm_error_generic"), isUser: false));
        }
        finally
        {
            // OnNavigatedFrom may have already disposed the source when the page went away
            _replyCancellation?.Dispose();
            _replyCancellation = null;

            UpdateSendState();
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

    private void AddMessage(ChatMessage message)
    {
        _messages.Add(message);

        // The new row may not be realised yet, so the scroll is queued until after the layout pass
        DispatcherQueue.TryEnqueue(() => MessagesList.ScrollIntoView(message));
    }

    private void UpdateSendState()
    {
        PromptBox.Document.GetText(TextGetOptions.None, out var text);

        SendButton.IsEnabled =
            !string.IsNullOrWhiteSpace(text) &&
            AppServices.Llm.IsConfigured &&
            _replyCancellation is null;
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
    private sealed class ChatMessage : INotifyPropertyChanged
    {
        public ChatMessage(string text, bool isUser, bool isPending = false)
        {
            Text = text;
            IsUser = isUser;
            IsPending = isPending;
        }

        public string Text { get; set; }
        public bool IsUser { get; }
        public bool IsPending { get; set; }

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
            return item is ChatMessage { IsUser: true } ? UserTemplate : AssistantTemplate;
        }
    }
}
