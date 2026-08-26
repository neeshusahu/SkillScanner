
using Markdig;



public class MarkDownParserWithChunks : IParser<IEnumerable<DocumentChunk>>
{
    private readonly MarkdownPipeline pipeline;
    private readonly IMarkdownChunker _markdownChunker;
    public MarkDownParserWithChunks(IMarkdownChunker markdownChunker)
    {
       pipeline=new MarkdownPipelineBuilder()
    .UsePipeTables()
    .Build();
    _markdownChunker=markdownChunker;
    }
    public Task<IEnumerable<DocumentChunk>> ParseAsync(string? data)
    {
        if(string.IsNullOrEmpty(data))
         throw new ArgumentException("Input data cannot be null or empty.", nameof(data));
         var document=Markdown.Parse(data, pipeline);
        var documentChunks= _markdownChunker.Chunk(document);
        return Task.FromResult<IEnumerable<DocumentChunk>>(documentChunks);


    }
}