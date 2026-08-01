
using SkillScanner.Models;

namespace SkillScanner.SkillRule;
public interface IRule
{
   IEnumerable<RuleResult> Evaluate(SkillData skillData);
}