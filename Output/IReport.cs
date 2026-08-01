using System.Data.Common;
using SkillScanner.Models;

namespace SkillScanner.Output;
public interface IReport
{
    void GenerateReport(Dictionary<int, List<RuleResult>> ruleResults, string filePath);
    void GenerateReportFile(IEnumerable<RuleResult> ruleResults, string filePath, string outputPath);
}