using System.Text;
using SkillScanner.Models;

namespace SkillScanner.Output;
public class Report : IReport
{
    

    public void GenerateReport(Dictionary<string, List<RuleResult>> ruleResults)
    {
         StringBuilder reportBuilder = new StringBuilder();
        reportBuilder.AppendLine($"Skill Scanner Report");
        foreach (var filePath in ruleResults.Keys)
        {
            reportBuilder.AppendLine($"File Path: {filePath}");
            reportBuilder.AppendLine("====================");
            reportBuilder.AppendLine($"Generated on: {DateTime.Now}");
            reportBuilder.AppendLine();
            if (ruleResults[filePath] == null || ruleResults[filePath].Count == 0)
            {
                reportBuilder.AppendLine("No rule results to report.");
                continue;
            }

            // Implement logic to generate a report based on the rule results
            foreach (var result in ruleResults[filePath])
            {
                reportBuilder.AppendLine($"Rule: {result.RuleType.Name}");
                reportBuilder.AppendLine($"Message: {result.Message}");
                reportBuilder.AppendLine($"Severity: {result.RuleType.Severity}");
                reportBuilder.AppendLine();
            }
        }
     
        
        Console.WriteLine(reportBuilder.ToString());
    
    }

    public void GenerateReportFile(IEnumerable<RuleResult> ruleResults, string filePath, string outputPath)
    {
        //yet to implement;
        return;
    }
}