public static class InsertQueries
{
    public const string InsertRule = """
        INSERT INTO Rules (RuleCode, RuleName, Description)
        VALUES (@RuleCode, @RuleName, @Description);
        SELECT last_insert_rowid();
        """;

    public const string InsertText = """
        INSERT INTO TextCorpus (Content, RuleId)
        VALUES (@Content, @RuleId);
        SELECT last_insert_rowid();
        """;

    public const string InsertEmbedding = """
        INSERT INTO TextCorpusEmbeddings (rowid, embedding)
        VALUES (@TextId, @Embedding);
        """;
}