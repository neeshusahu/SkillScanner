
using System.Data;
using SkillScanner.LLMClient;
using SkillScanner.Models;
using SkillScanner.SkillRule;

public abstract class RuleBase : IRule
{
    protected abstract RuleType RuleType { get; }

    protected abstract string SystemPrompt { get; }

    protected ILLMClient LLMClient { get; set; }

    public RuleBase(ILLMClient llmClient)
    {
        LLMClient = llmClient;
    }

    protected const string JsonContract = """
        Respond with ONLY a JSON object, no other text, no markdown fences, in this exact shape:
        {"isFlagged": bool, "confidence": number between 0 and 1, "reasoning": "one sentence"}
        """;

    protected abstract IEnumerable<RuleResult> EvaluateDeterministic(SkillData skillData);

    protected async Task<RuleResult> EvaluateWithLLMAsync(SkillData skillData, CancellationToken cancellationToken = default)
    {
        if (skillData == null || string.IsNullOrWhiteSpace(SystemPrompt))
        {
            throw new InvalidOperationException("Invalid skill data or system prompt for LLM evaluation.");
        }
        LLMVerdict llmVerdict = LLMClient.GetResponseAsync(SystemPrompt, skillData.SkillMarkDown ?? "", cancellationToken).GetAwaiter().GetResult();
        if (llmVerdict == null)
        {
            throw new InvalidOperationException("LLM returned null verdict.");
        }
        if (llmVerdict.IsFlagged)
        {
            return new RuleResult
            {
                RuleType = this.RuleType,
                Message = llmVerdict.Reasoning ?? "The skill is non-compliant based on LLM evaluation.",
            };
        }
        return new RuleResult();
    }

    public virtual async Task<IEnumerable<RuleResult>> EvaluateAsync(SkillData skillData, ILLMClient llmClient, CancellationToken cancellationToken = default)
    {

        var ruleResults = new List<RuleResult>();
        ruleResults = EvaluateDeterministic(skillData).ToList();


        if (ruleResults.Count.Equals(0))
        {
            // If no deterministic results, use LLM for evaluation
            var fallbackResult = await EvaluateWithLLMAsync(skillData, cancellationToken);
            if (fallbackResult != null && !string.IsNullOrEmpty(fallbackResult.Message))
            {
                ruleResults.Add(fallbackResult);
            }
        }


        return ruleResults;
    }
}