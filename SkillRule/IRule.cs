
using SkillScanner.LLMClient;
using SkillScanner.Models;

namespace SkillScanner.SkillRule;
public interface IRule
{
   Task<IEnumerable<RuleResult>> EvaluateAsync(SkillData skillData, ILLMClient llmClient, CancellationToken cancellationToken = default);
}