using System.Text;
using SkillScanner.Models;

namespace SkillScanner.Output;
public class Report : IReport
{
    

    public void GenerateReport(Dictionary<int, List<RuleResult>> ruleResults, string filePath)
    {
         StringBuilder reportBuilder = new StringBuilder();
        reportBuilder.AppendLine($"Skill Scanner Report");
        reportBuilder.AppendLine("File Path: " + filePath);
        reportBuilder.AppendLine("====================");
        reportBuilder.AppendLine($"Generated on: {DateTime.Now}");
        reportBuilder.AppendLine();

        // Implement logic to generate a report based on the rule results
        foreach (var key in ruleResults.Keys)
        {
            reportBuilder.AppendLine($"Rule Type: {key}");
            foreach(var result in ruleResults[key])
            {
                reportBuilder.AppendLine($"Rule: {result.RuleType.Name}");
              reportBuilder.AppendLine($"Message: {result.Message}");
              reportBuilder.AppendLine($"Severity: {result.Severity}");
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