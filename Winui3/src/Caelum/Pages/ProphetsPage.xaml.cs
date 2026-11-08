using System.Collections.ObjectModel;
using Caelum.Core.Localization;
using Microsoft.UI.Input;
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

    /// <summary>
    ///     Enter sends the message; Shift+Enter drops a new line, which is what the multi line box is
    ///     for.
    /// </summary>
    private void OnPromptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        // A shift held down belongs to the text, so the newline is left to the box
        if (IsShiftDown()) return;

        e.Handled = true;
        SendMessage();
    }

    private void OnPromptTextChanged(object sender, TextChangedEventArgs e) => UpdateSendState();

    private void OnSend(object sender, RoutedEventArgs e) => SendMessage();

    /// <summary>
    ///     Shows the student's message at once and lets the placeholder reply follow a moment later,
    ///     so the two never appear together and the conversation reads the way a live one would.
    /// </summary>
    private async void SendMessage()
    {
        var text = PromptBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(text)) return;

        AddMessage(new ChatMessage(text, IsUser: true));

        PromptBox.Text = string.Empty;

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

    private void AddMessage(ChatMessage message)
    {
        _messages.Add(message);

        // The new row may not be realised yet, so the scroll is queued until after the layout pass
        DispatcherQueue.TryEnqueue(() => MessagesList.ScrollIntoView(message));
    }

    private void UpdateSendState() =>
        SendButton.IsEnabled = !string.IsNullOrWhiteSpace(PromptBox.Text);

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