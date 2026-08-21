public interface IVectorRepository
{
    Task InitializeSchemaAsync();
    
    Task<long> InsertRuleAsync(string ruleCode, string ruleName, string description);

    Task<long> InsertTextCorpusEntryAsync(string content, int? ruleId, float[] embedding);
    Task<IEnumerable<TextCorpusMatch>> SearchSimilarTextAsync(
    float[] queryEmbedding, long? ruleId, int topK = 3);

}