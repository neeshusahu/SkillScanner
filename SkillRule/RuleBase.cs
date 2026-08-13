
using System.Data;
using System.Diagnostics;
using System.Net.Mail;
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
    protected static int SuccessCount = 0;
    protected static int FailureCount = 0;
    
    protected static int TotalCount=0;

    
    protected const string JsonContract = """
        Respond with ONLY a JSON object, no other text, no markdown fences, in this exact shape:
        {"isFlagged": bool, "confidence": number between 0 and 1, "reasoning": "one sentence"}
        """;

    protected abstract IEnumerable<RuleResult> EvaluateDeterministic(SkillData skillData);

    protected async Task<RuleResult> EvaluateWithLLMAsync(SkillData skillData, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (skillData == null || string.IsNullOrWhiteSpace(SystemPrompt))
        {
            throw new InvalidOperationException("Invalid skill data or system prompt for LLM evaluation.");
        }
//Make this thread safe by using Interlocked.Increment to increment the TotalCount variable atomically  
       Interlocked.Increment(ref TotalCount);

        LLMVerdict llmVerdict;
        try
        {
            llmVerdict = await LLMClient.GetResponseAsync(
                SystemPrompt,
                skillData.SkillMarkDown ?? "",
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation timed out after {stopwatch.Elapsed} for rule: {RuleType.Name}");
            return new RuleResult();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation failed after {stopwatch.Elapsed} for rule: {RuleType.Name}. {ex.Message}");
            return new RuleResult();
        }

        stopwatch.Stop();
        Console.WriteLine($"LLM evaluation completed in {stopwatch.Elapsed} seconds for rule: {RuleType.Name}");
        if (llmVerdict == null)     
        {
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation returned no verdict for rule: {RuleType.Name}");
            return new RuleResult();
        }
        if (llmVerdict.IsFlagged)
        {
             Interlocked.Increment(ref SuccessCount);
           
            return new RuleResult
            {
                RuleType = this.RuleType,
                Message = llmVerdict.Reasoning ?? "The skill is non-compliant based on LLM evaluation.",
                IsFlagged = true
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
    public void CountCalls()
    {
        Console.WriteLine($"Total Success Count: {SuccessCount}, Total Failure Count: {FailureCount}, UnFlagged Count: {TotalCount - SuccessCount - FailureCount}");
        Console.WriteLine($"Total Evaluations: {TotalCount}");
    }
}
