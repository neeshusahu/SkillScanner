public record TextCorpusMatch
{
    public long TextId { get; init; }
    public long? RuleId { get; init; }
    public string Content { get; init; } = "";
    public double Distance { get; init; }
}