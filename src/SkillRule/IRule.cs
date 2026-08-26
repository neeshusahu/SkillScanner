
using System.Runtime.InteropServices.Marshalling;



public interface IRule
{
    Task<IEnumerable<RuleResult>> EvaluateAsync(SkillData skillData, CancellationToken cancellationToken = default);
  
   Task<IEnumerable<RuleResult>> EvaluateAsyncWithRAG(SkillData skillData, CancellationToken cancellationToken=default);

   void CountCalls();
}