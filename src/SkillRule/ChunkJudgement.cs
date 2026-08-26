

public record ChunkJudgment
{
    public DocumentChunk Chunk { get; init; } = null!;
    public List<TextCorpusMatch> Matches { get; init; } = new();
    public LLMVerdict Verdict { get; init; } = null!;
}