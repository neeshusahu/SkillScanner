public class DocumentChunk
{
    public string Content { get; init; } = "";
    public string MainHeading { get; init; } = "";
    public int ChunkStartOffset { get; init; }
    public int ChunkEndOffset { get; init; }
    public List<Section> OriginalData { get; init; } = new();
}
