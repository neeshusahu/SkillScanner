using Markdig;
using Markdig.Syntax;


public class MarkdownParser : IParser<MarkdownDocument>
{
    private readonly MarkdownPipeline pipeline;
    public MarkdownParser()
    {
     pipeline=new MarkdownPipelineBuilder()
    .UsePipeTables()
    .Build();
    }
    public async Task<MarkdownDocument> ParseAsync(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Input data cannot be null or empty.", nameof(data));
        }
        var document=Markdown.Parse(data, pipeline);
        
        return document; // Return the Markdown content as a string      
    }
}