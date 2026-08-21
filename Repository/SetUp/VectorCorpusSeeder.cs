public static class VectorCorpusSeeder
{
    public static async Task SeedAsync(IVectorRepository repository, IEmbeddingClient embeddingClient)
    {
        await repository.InitializeSchemaAsync();

        // var existingRuleId = await repository.GetRuleIdByCodeAsync("AST03");
        // if (existingRuleId.HasValue)
        //     return; // already seeded — don't duplicate on every app/test start

        var ruleId = await repository.InsertRuleAsync(
            "AST03",
            "Excessive Agency / Over-Privilege",
            "Skill requests broader capability or access than its stated purpose requires.");

        foreach (var (content, ruleCode) in SeedData.OverprivilegedCorpus)
        {
            var embedding = await embeddingClient.GetEmbeddingAsync(content);
            int? appliedRuleId = ruleCode is null ? null : (int)ruleId;
            await repository.InsertTextCorpusEntryAsync(content, appliedRuleId, embedding);
        }
    }
}