public static class SearchQueries
{
    //This query will not work with vec0 as KNN planner gets confused if 
    //distance  is not directly linked with where
    // public const string SearchSimilarChunks = """
    // SELECT
    //     tc.TextId,
    //     tc.RuleId,
    //     tc.Content,
    //     tce.distance AS Distance
    // FROM TextCorpusEmbeddings tce
    // JOIN TextCorpus tc
    //     ON tce.rowid = tc.TextId
    // WHERE tce.embedding MATCH @Embedding
    //   AND (tc.RuleId = @RuleId OR tc.RuleId IS NULL)
    // ORDER BY tce.distance
    // LIMIT @TopK;
    // """;

  public const string SearchSimilarChunks = """
    SELECT
    tc.TextId,
    tc.RuleId,
    tc.Content,
    knn.distance AS Distance
 FROM (
    SELECT rowid, distance
    FROM TextCorpusEmbeddings
    WHERE embedding MATCH @Embedding
    ORDER BY distance
    LIMIT @TopK
 ) AS knn
 JOIN TextCorpus tc ON tc.TextId = knn.rowid
 WHERE (tc.RuleId = @RuleId OR tc.RuleId IS NULL)
 ORDER BY knn.distance;
 """;

 public const string SeachRuleIdQuery= """
  Select RuleId 
  from Rules
  where RuleCode=@RuleCode
  """;
} 

