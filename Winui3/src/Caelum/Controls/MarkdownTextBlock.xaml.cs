using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace Caelum.Controls;

/// <summary>
///     Renders Markdown into a rich text block, so the assistant's formatted answers read as
///     formatting rather than raw symbols. The text is expected to change while a reply streams,
///     so updates are coalesced into one render per UI turn.
/// </summary>
public sealed partial class MarkdownTextBlock
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    private bool _renderQueued;
    private string _textToRender = string.Empty;

    public MarkdownTextBlock()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MarkdownTextBlock),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>The Markdown source to render.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkdownTextBlock)d;

        control._textToRender = e.NewValue as string ?? string.Empty;

        if (control._renderQueued) return;

        control._renderQueued = true;

        // A streamed answer raises this for every delta; coalesce them so the block re-renders
        // once per UI turn instead of once per token
        control.DispatcherQueue.TryEnqueue(() =>
        {
            control._renderQueued = false;
            control.Render(control._textToRender);
        });
    }

    private void Render(string markdown)
    {
        RichText.Blocks.Clear();

        if (string.IsNullOrWhiteSpace(markdown)) return;

        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var block in document)
            RenderBlock(block, RichText.Blocks);
    }

    private static void RenderBlock(MarkdigBlock block, BlockCollection blocks)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var headingParagraph = new Paragraph();
                headingParagraph.Inlines.Add(new Run
                {
                    Text = GetInnerText(heading.Inline),
                    FontWeight = new FontWeight { Weight = 700 },
                    FontSize = 18
                });
                blocks.Add(headingParagraph);
                break;

            case ParagraphBlock paragraph:
                var textParagraph = new Paragraph();
                AddInlines(textParagraph, paragraph.Inline);
                blocks.Add(textParagraph);
                break;

            case ListBlock list:
                var number = 0;

                foreach (var item in list)
                {
                    if (item is not ListItemBlock listItem) continue;

                    var itemParagraph = new Paragraph();
                    itemParagraph.Inlines.Add(new Run { Text = list.IsOrdered ? $"{++number}. " : "• " });

                    foreach (var child in listItem)
                    {
                        if (child is ParagraphBlock itemBlock)
                            AddInlines(itemParagraph, itemBlock.Inline);
                    }

                    blocks.Add(itemParagraph);
                }
                break;

            case CodeBlock code:
                var codeParagraph = new Paragraph();
                var codeText = string.Join("\n", code.CodeBlockLines.Select(line => line.ToString()));

                codeParagraph.Inlines.Add(new Run
                {
                    Text = codeText,
                    FontFamily = new FontFamily("Consolas")
                });
                blocks.Add(codeParagraph);
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderBlock(child, blocks);
                break;

            case ThematicBreakBlock:
                var lineParagraph = new Paragraph();
                var line = new InlineUIContainer
                {
                    Child = new Border
                    {
                        Height = 1,
                        Background = (Brush?)Application.Current.Resources["CardStrokeColorDefaultBrush"]
                            ?? new SolidColorBrush(Microsoft.UI.Colors.Gray)
                    }
                };
                lineParagraph.Inlines.Add(line);
                blocks.Add(lineParagraph);
                break;
        }
    }

    private static void AddInlines(Paragraph paragraph, ContainerInline? container)
    {
        if (container is null) return;

        foreach (var inline in container)
            AddInline(paragraph, inline);
    }

    private static void AddInline(Paragraph paragraph, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                paragraph.Inlines.Add(new Run { Text = literal.Content.ToString() });
                break;

            case CodeInline code:
                paragraph.Inlines.Add(new Run
                {
                    Text = code.Content,
                    FontFamily = new FontFamily("Consolas")
                });
                break;

            case EmphasisInline emphasis:
                var run = new Run { Text = GetInnerText(emphasis) };

                if (emphasis.DelimiterCount >= 2)
                    run.FontWeight = new FontWeight { Weight = 700 };
                if (emphasis.DelimiterCount == 1)
                    run.FontStyle = FontStyle.Italic;

                paragraph.Inlines.Add(run);
                break;

            case LinkInline link:
                var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.Single };

                if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    try
                    {
                        hyperlink.NavigateUri = new Uri(link.Url, UriKind.RelativeOrAbsolute);
                    }
                    catch (UriFormatException)
                    {
                        // A malformed link is shown as plain text
                    }
                }

                hyperlink.Inlines.Add(new Run { Text = GetInnerText(link) });
                paragraph.Inlines.Add(hyperlink);
                break;

            case LineBreakInline:
                paragraph.Inlines.Add(new LineBreak());
                break;

            case ContainerInline container:
                foreach (var child in container)
                    AddInline(paragraph, child);
                break;
        }
    }

    /// <summary>Collects the plain text of a container, for the nodes that render as a single run.</summary>
    private static string GetInnerText(ContainerInline? container)
    {
        if (container is null) return string.Empty;

        var builder = new StringBuilder();

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content);
                    break;

                case CodeInline code:
                    builder.Append(code.Content);
                    break;

                case ContainerInline child:
                    builder.Append(GetInnerText(child));
                    break;
            }
        }

        return builder.ToString();
    }
}
