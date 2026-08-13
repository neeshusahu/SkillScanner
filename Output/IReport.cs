using System.Data.Common;
using SkillScanner.Models;

namespace SkillScanner.Output;
public interface IReport
{
    void GenerateReport(Dictionary<string, List<RuleResult>> ruleResults);
    void GenerateReportFile(IEnumerable<RuleResult> ruleResults, string filePath, string outputPath);
}