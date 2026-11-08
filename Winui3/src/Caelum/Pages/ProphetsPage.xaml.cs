using System.Collections.ObjectModel;
using Caelum.Core.Localization;
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
///     replies appear as bubbles; the assistant is a fixed placeholder until the model backing is
///     wired up.
/// </summary>
public sealed partial class ProphetsPage
{
    private const int ReplyDelayMilliseconds = 1200;

    private readonly StringLocalizer _strings = new();
    private readonly ObservableCollection<ChatMessage> _messages = [];

    /// <summary>
    ///     Cancels the pending placeholder reply when the page goes away, so a reply cannot land on
    ///     a conversation the student has already left. One source per page lifetime.
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

        UpdateSendState();
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

    /// <summary>
    ///     Shows the student's message at once and lets the placeholder reply follow a moment later,
    ///     so the two never appear together and the conversation reads the way a live one would.
    /// </summary>
    private async void SendMessage()
    {
        PromptBox.Document.GetText(TextGetOptions.None, out var text);

        var trimmed = text.Trim();

        if (string.IsNullOrEmpty(trimmed)) return;

        AddMessage(new ChatMessage(NormalizeLineEndings(trimmed), IsUser: true));

        PromptBox.Document.SetText(TextSetOptions.None, string.Empty);

        var reply = _strings.Get("prophets_placeholder_reply");

        _replyCancellation ??= new CancellationTokenSource();

        try
        {
            await Task.Delay(ReplyDelayMilliseconds, _replyCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The page went away before the reply was due
            return;
        }

        AddMessage(new ChatMessage(reply, IsUser: false));
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

        SendButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>True while Shift is held, which turns Enter into a newline rather than a sending.</summary>
    private static bool IsShiftDown()
    {
        // The state arrives as a flags enum over the key asked for, so zero means up
        return (int)InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) != 0;
    }

    /// <summary>One turn of the conversation.</summary>
    private sealed record ChatMessage(string Text, bool IsUser);

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