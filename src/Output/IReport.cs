using System.Data.Common;



public interface IReport
{
    void GenerateReport(Dictionary<string, List<RuleResult>> ruleResults);
    void GenerateReportFile(IEnumerable<RuleResult> ruleResults, string filePath, string outputPath);
}