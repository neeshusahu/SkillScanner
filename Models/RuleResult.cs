namespace SkillScanner.Models;
public class RuleResult
{
    public RuleType RuleType { get; set; }
    public  string? Message { get; set; }

}


public enum RuleResultType
{
    Success,
    Failure,
    Warning
}

public enum RuleSeverity
{
    Low,
    Medium,
    High
}
