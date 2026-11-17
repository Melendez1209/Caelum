using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
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
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .Build();

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
                AddInlines(textParagraph.Inlines, paragraph.Inline);
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
                            AddInlines(itemParagraph.Inlines, itemBlock.Inline);
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

            case Table table:
                RenderTable(table, blocks);
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

    /// <summary>
    ///     Renders a Markdown table as a bordered grid. A RichTextBlock has no table element, so the
    ///     grid is hosted in an inline container and participates in the surrounding text flow.
    /// </summary>
    private static void RenderTable(Table table, BlockCollection blocks)
    {
        var rows = new List<TableRow>();

        foreach (var row in table)
        {
            if (row is TableRow tableRow)
                rows.Add(tableRow);
        }

        if (rows.Count == 0 || table.ColumnDefinitions.Count == 0)
            return;

        var grid = new Grid { Margin = new Thickness(1) };

        for (var column = 0; column < table.ColumnDefinitions.Count; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var isHeader = rows[rowIndex].IsHeader;
            var columnIndex = 0;

            foreach (var cell in rows[rowIndex])
            {
                if (cell is not TableCell tableCell) continue;

                var cellText = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 5, 10, 5)
                };

                if (isHeader)
                    cellText.FontWeight = new FontWeight { Weight = 700 };

                AddInlines(cellText.Inlines, CellContents(tableCell));

                Grid.SetRow(cellText, rowIndex);
                Grid.SetColumn(cellText, columnIndex);

                grid.Children.Add(cellText);

                columnIndex++;
            }
        }

        var tableParagraph = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
        tableParagraph.Inlines.Add(new InlineUIContainer
        {
            Child = new Border
            {
                BorderBrush = (Brush?)Application.Current.Resources["CardStrokeColorDefaultBrush"]
                    ?? new SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = grid
            }
        });

        blocks.Add(tableParagraph);
    }

    /// <summary>The inline content of a cell, taken from its first paragraph.</summary>
    private static ContainerInline? CellContents(TableCell cell)
    {
        foreach (var child in cell)
        {
            if (child is ParagraphBlock paragraph)
                return paragraph.Inline;
        }

        return null;
    }

    private static void AddInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null) return;

        foreach (var inline in container)
            AddInline(target, inline);
    }

    private static void AddInline(InlineCollection target, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run { Text = literal.Content.ToString() });
                break;

            case CodeInline code:
                target.Add(new Run
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

                target.Add(run);
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
                target.Add(hyperlink);
                break;

            case LineBreakInline:
                target.Add(new LineBreak());
                break;

            case ContainerInline container:
                foreach (var child in container)
                    AddInline(target, child);
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
