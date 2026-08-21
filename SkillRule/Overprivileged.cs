

using System.Data;
using SkillScanner.LLMClient;
using SkillScanner.Models;

namespace SkillScanner.SkillRule;

public class Overprivileged : RuleBase

{
    public Overprivileged(ILLMClient llmClient, IVectorRepository vectorRepository, IEmbeddingClient embeddingClient, IMarkdownChunker markdownChunker) : base(llmClient, vectorRepository, embeddingClient,markdownChunker)
    {
    }
    protected override RuleType RuleType { get; } = new RuleType
    {
        Name = "Overprivileged",
        Id = 1,
        Description = "The skill has overprivileged permissions.",
        Severity = RuleSeverity.High

    };


   protected override string SystemPrompt => $"""
        You are a security analyst reviewing an AI agent "skill" definition. Determine if the
        skill requests broader permissions or capabilities than its stated purpose requires.

        {JsonContract}
        """;



    protected override IEnumerable<RuleResult> EvaluateDeterministic(SkillData skillData)
    {
        var results = new List<RuleResult>();
        if (skillData == null)
        {
            return results;
        }
        EvaluatePermission(skillData, results);
        EvaluateWriteAccess(skillData, results);
        return results;
    }

    private void EvaluatePermission(SkillData skillData, List<RuleResult> results)
    {
        if (!string.IsNullOrEmpty(skillData.Compatibility) && skillData.Compatibility.Contains("Requires network access"))
        {
            results.Add(new RuleResult
            {
                RuleType = this.RuleType,
                Message = $"The skill has the following permission: {skillData.Compatibility}.",

            });
        }
    }
    private void EvaluateWriteAccess(SkillData skillData, List<RuleResult> results)
    {
        string[] notAllowedInputs = ["memory.md", "soul.md", "curl", "bash"];
        skillData.SkillMarkdownContent = skillData.SkillMarkdownContent?.ToLower();
        if (!string.IsNullOrEmpty(skillData.SkillMarkdownContent) && notAllowedInputs.Any(file => skillData.SkillMarkdownContent.Contains(file)))
        {
            results.Add(new RuleResult
            {
                RuleType = this.RuleType,
                Message = $"The skill has the write permission to identity files.",

            });
        }
    }


}

