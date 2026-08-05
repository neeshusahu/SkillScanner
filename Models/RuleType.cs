namespace SkillScanner.Models;
public class RuleType
{
    public string? Name { get; set; }
    public int Id { get; set; }

    public string? Description { get; set; }
   
   public RuleSeverity Severity { get; set; }

}