
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;
public class MarkdownChunker : IMarkdownChunker
{
    private const string InlineSeparator = " ";

    public List<DocumentChunk> Chunk(MarkdownDocument document)
    {
        var sectionStack = new Stack<Section>();
        var currentSections = new List<Section>();
        var documentChunks = new List<DocumentChunk>();

        foreach (var block in document)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    FlushOpenHeading(sectionStack, currentSections, documentChunks);
                    PushHeading(sectionStack, heading);
                    break;

                case ParagraphBlock paragraph:
                    PushParagraph(sectionStack, paragraph);
                    break;

                case ListBlock list:
                    PushList(sectionStack, list);
                    break;

                case Table table:
                    PushTable(sectionStack, table);
                    break;
            }
        }

        FlushOpenHeading(sectionStack, currentSections, documentChunks);

        return documentChunks;
    }

    // Drains everything currently on the stack (the previous heading and
    // whatever content was pushed under it) into one finished chunk.
    private void FlushOpenHeading(Stack<Section> sectionStack, List<Section> currentSections, List<DocumentChunk> documentChunks)
    {
        while (sectionStack.Count > 0)
        {
            currentSections.Add(sectionStack.Pop());
        }

        if (currentSections.Count == 0)
        {
            return;
        }

        documentChunks.Add(BuildChunk(currentSections));
        currentSections.Clear();
    }

    private void PushHeading(Stack<Section> sectionStack, HeadingBlock heading)
    {
        var headingText = GetInlineContent(heading.Inline);

        sectionStack.Push(new Section(
            leafBlock: heading,
            containerBlock: null,
            level: heading.Level,
            content: headingText,
            spanStart: heading.Span.Start,
            spanEnd: heading.Span.End,
            isHeading: true
          ));
    }

    private void PushParagraph(Stack<Section> sectionStack, ParagraphBlock paragraph)
    {
        var parentHeading = GetCurrentHeadingTitle(sectionStack);

        sectionStack.Push(new Section(
            leafBlock: paragraph,
            containerBlock: null,
            level: GetCurrentLevel(sectionStack),
            content: GetInlineContent(paragraph.Inline),
            spanStart: paragraph.Span.Start,
            spanEnd: paragraph.Span.End,
            isHeading: false
            ));
    }

    private void PushList(Stack<Section> sectionStack, ListBlock list)
    {
      
        var listContent = GetListContent(list);

        sectionStack.Push(new Section(
            leafBlock: null,
            containerBlock: list,
            level: GetCurrentLevel(sectionStack),
            content: JoinLines(listContent),
            spanStart: list.Span.Start,
            spanEnd: list.Span.End,
            isHeading: false
            ));
    }

    private void PushTable(Stack<Section> sectionStack, Table table)
    {
        var parentHeading = GetCurrentHeadingTitle(sectionStack);
        var tableContent = GetTableContent(table);

        sectionStack.Push(new Section(
            leafBlock: null,
            containerBlock: table,
            level: GetCurrentLevel(sectionStack),
            content: JoinLines(tableContent),
            spanStart: table.Span.Start,
            spanEnd: table.Span.End,
            isHeading: false
            ));
    }

    private static int GetCurrentLevel(Stack<Section> sectionStack)
        => sectionStack.TryPeek(out var current) ? current.Level : 0;

    private static string? GetCurrentHeadingTitle(Stack<Section> sectionStack)
        => sectionStack.TryPeek(out var current) ? current.Content : null;

    private DocumentChunk BuildChunk(List<Section> sections)
    {
        sections.Reverse();

        var contentBuilder = new StringBuilder();
        foreach (var section in sections)
        {
            contentBuilder.AppendLine(section.Content);
        }

        var ownHeading = sections[0];
      

        return new DocumentChunk
        {
            Content = contentBuilder.ToString(),
            MainHeading = ownHeading.Content,
            OriginalData = new List<Section>(sections),
            ChunkStartOffset = sections[0].SpanStart,
            ChunkEndOffset = sections[^1].SpanEnd
        };
    }

    private List<string> GetListContent(ListBlock listBlock)
    {
        var content = new List<string> { "[List]" };
        var index = 1;

        foreach (var item in listBlock)
        {
            if (item is not ListItemBlock listItemBlock)
            {
                continue;
            }

            foreach (var para in listItemBlock)
            {
                if (para is ParagraphBlock paragraphBlock)
                {
                    var itemText = GetInlineContent(paragraphBlock.Inline);
                    content.Add($"{index}. {itemText}");
                }
            }

            index++;
        }

        return content;
    }

    private List<string> GetTableContent(Table table)
    {
        var content = new List<string> { "[Table]" };
        var rowIndex = 1;

        foreach (var row in table)
        {
            if (row is not TableRow tableRow)
            {
                continue;
            }

            var rowText = GetTableRowText(tableRow);

            if (rowIndex == 1)
            {
                rowText = "Columns: " + rowText;
            }

            content.Add(rowText);
            rowIndex++;
        }

        return content;
    }

    private string GetTableRowText(TableRow tableRow)
    {
        var cellTexts = new List<string>();

        foreach (var cell in tableRow)
        {
            if (cell is not TableCell tableCell)
            {
                continue;
            }

            foreach (var cellBlock in tableCell)
            {
                if (cellBlock is ParagraphBlock paragraphBlock && paragraphBlock.Inline is ContainerInline containerInline)
                {
                    cellTexts.Add(GetInlineContent(containerInline));
                }
            }
        }

        return string.Join(" | ", cellTexts);
    }

    private string GetInlineContent(ContainerInline? container)
    {
        if (container is null)
        {
            return "";
        }

        var stringBuilder = new StringBuilder();

        foreach (var inline in container)
        {
            switch (inline)
            {
                case CodeInline codeInline:
                    stringBuilder.Append(codeInline.Content);
                    stringBuilder.Append(InlineSeparator);
                    break;

                case LiteralInline literalInline:
                    stringBuilder.Append(literalInline.Content);
                    stringBuilder.Append(InlineSeparator);
                    break;
            }
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string JoinLines(List<string> lines)
        => string.Join(Environment.NewLine, lines);
}