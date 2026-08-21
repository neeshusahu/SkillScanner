

public static class Schema
{
    public const string CreateRulesTable = """
        CREATE TABLE IF NOT EXISTS Rules (
            RuleId       INTEGER PRIMARY KEY,
            RuleCode     TEXT NOT NULL UNIQUE,
            RuleName     TEXT NOT NULL,
            Description  TEXT
        );
        """;

    public const string CreateTextCorpusTable = """
        CREATE TABLE IF NOT EXISTS TextCorpus (
            TextId       INTEGER PRIMARY KEY,
            Content      TEXT NOT NULL,
            RuleId       INTEGER REFERENCES Rules(RuleId)
        );
        """;

    public const string CreateTextCorpusEmbeddingsTable = """
        CREATE VIRTUAL TABLE IF NOT EXISTS TextCorpusEmbeddings USING vec0(
            embedding float[768] distance_metric=cosine
        );
        """;
}