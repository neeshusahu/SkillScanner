using Markdig;
using Markdig.Syntax;

namespace SkillScanner.Parser;
public class MarkDownParser : IParser<String>
{
    public MarkDownParser()
    {
    }
    public async Task<string> ParseAsync(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Input data cannot be null or empty.", nameof(data));
        }
        MarkdownDocument document = Markdown.Parse(data);
       if (document == null)
        {
            throw new InvalidOperationException("Failed to parse the Markdown content.");
        }
        return data; // Return the Markdown content as a string      
    }
}