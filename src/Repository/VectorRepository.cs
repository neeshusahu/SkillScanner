using System.Data;
using Dapper;

public class VectorRepository: IVectorRepository
{
    private readonly IDbConnection connection;

    public VectorRepository(IDbConnection dbConnection)
    {
        connection=dbConnection;
    }
    public async Task InitializeSchemaAsync()
    {
        await connection.ExecuteAsync(Schema.CreateRulesTable);
        await connection.ExecuteAsync(Schema.CreateTextCorpusTable);
        await connection.ExecuteAsync(Schema.CreateTextCorpusEmbeddingsTable);
    }
public async Task<long> InsertRuleAsync(
    string ruleCode,
    string ruleName,
    string description)
{
    var existingId = await connection.ExecuteScalarAsync<long?>(SearchQueries.SeachRuleIdQuery,
        new { RuleCode = ruleCode });

    if (existingId.HasValue)
        return existingId.Value;

    return await connection.ExecuteScalarAsync<long>(
        InsertQueries.InsertRule,
        new
        {
            RuleCode = ruleCode,
            RuleName = ruleName,
            Description = description
        });
}

public async Task<long> InsertTextCorpusEntryAsync(string content, int? ruleId, float[] embedding)
{
    using var transaction = connection.BeginTransaction();

    try
    {
        var textId = await connection.ExecuteScalarAsync<long>(InsertQueries.InsertText, new
        {
            Content = content,
            RuleId = ruleId
        }, transaction);

        // TextCorpusEmbeddings.rowid must equal this TextId — the join key
        // QueryNearestTextAsync uses to go from a vector match back to its labeled source.
        await connection.ExecuteAsync(InsertQueries.InsertEmbedding, new
        {
            TextId = textId,
            Embedding = VectorSerializer.ToBytes(embedding)
        }, transaction);

        transaction.Commit();
        return textId;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

public async Task<IEnumerable<TextCorpusMatch>> SearchSimilarTextAsync(
    float[] queryEmbedding, long? ruleId, int topK = 3)
{
    return await connection.QueryAsync<TextCorpusMatch>(SearchQueries.SearchSimilarChunks, new
    {
        Embedding = VectorSerializer.ToBytes(queryEmbedding),
        RuleId = ruleId,
        TopK = topK
    });
}
}