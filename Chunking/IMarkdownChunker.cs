using Markdig.Syntax;

public interface IMarkdownChunker
{
    List<DocumentChunk> Chunk(MarkdownDocument document);
}