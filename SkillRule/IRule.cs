
using System.Runtime.InteropServices.Marshalling;
using SkillScanner.LLMClient;
using SkillScanner.Models;

namespace SkillScanner.SkillRule;
public interface IRule
{
    Task<IEnumerable<RuleResult>> EvaluateAsync(SkillData skillData, CancellationToken cancellationToken = default);
  
   Task<IEnumerable<RuleResult>> EvaluateAsyncWithRAG(SkillData skillData, CancellationToken cancellationToken=default);

   void CountCalls();
}