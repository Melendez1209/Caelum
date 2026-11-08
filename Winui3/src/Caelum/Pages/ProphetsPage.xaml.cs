using Caelum.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Caelum.Pages;

/// <summary>
///     The conversation page for the AI assistant. The shell is built out now so it can be reached
///     from the shell and the exam detail page; the model backing is wired up separately.
/// </summary>
public sealed partial class ProphetsPage
{
    private readonly StringLocalizer _strings = new();

    /// <summary>Creates the page and translates its captions.</summary>
    public ProphetsPage()
    {
        InitializeComponent();

        TitleText.Text = _strings.Get("prophets");
        PromptBox.PlaceholderText = _strings.Get("ask_something");
    }

    // The send button is left live but inert on purpose, so the page can be exercised before the
    // conversation backing exists. The handler is the seam the AI service will be wired into
    private void OnSend(object sender, RoutedEventArgs e)
    {
    }
}
