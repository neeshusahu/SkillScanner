
public class RuleResult
{
    public RuleType RuleType { get; set; }
    public  string? Message { get; set; }

    public bool IsFlagged { get; init;}= false;

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
