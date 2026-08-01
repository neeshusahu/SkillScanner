

using System.Data;
using SkillScanner.Models;

namespace SkillScanner.SkillRule;

public class Overprivileged : IRule
{
    public RuleType RuleType { get; } = new RuleType
    {
        Name = "Overprivileged",
        Id = 1,
        Description = "The skill has overprivileged permissions."
    };
    public IEnumerable<RuleResult> Evaluate(SkillData skillData)
    {
        var results = new List<RuleResult>();
        if(skillData == null)
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
                Severity = RuleSeverity.High
            });
        }
    }
    private void EvaluateWriteAccess(SkillData skillData, List<RuleResult> results)
    {
        string[] identityFiles=["memory.md", "soul.md"];
        skillData.SkillMarkDown = skillData.SkillMarkDown?.ToLower();
        if (!string.IsNullOrEmpty(skillData.SkillMarkDown) && identityFiles.Any(file => skillData.SkillMarkDown.Contains(file)))
        {
            results.Add(new RuleResult
            {
                RuleType = this.RuleType,
                Message = $"The skill has the write permission to identity files.",
                Severity = RuleSeverity.High
            });
        }
    }
    
          
}

