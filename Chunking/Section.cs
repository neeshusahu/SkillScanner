using Markdig.Syntax;

public class Section(LeafBlock? leafBlock, ContainerBlock? containerBlock, int level, string content, int spanStart, int spanEnd, bool isHeading)
{
    public LeafBlock? Block { get; init; } = leafBlock;
    public ContainerBlock? ContainerBlock { get; init; } = containerBlock;
    public int Level { get; init; } = level;
    public string Content { get; init; } = content;
    public int SpanStart { get; init; } = spanStart;
    public int SpanEnd { get; init; } = spanEnd;
    public bool IsHeading { get; init; } = isHeading;
 
}