using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using SkillScanner.Inputs;
using SkillScanner.Mapping;
using SkillScanner.Models;
using SkillScanner.Parser;
using SkillScanner.Output;
using SkillScanner.SkillRule;
using YamlDotNet.Core.Tokens;
using SkillScanner.LLMClient;

public class Scanner
{
       
        private readonly IReport _report;
        private readonly IInput  _input;
        private readonly IEnumerable<IRule> _rules;

        private readonly ILLMClient _llmClient;
    

        public Scanner( IReport report, IInput input, IEnumerable<IRule> rules, ILLMClient llmClient)
        {
            //_mapper = mapper;
            //_yamlParser = yamlParser;
            _report = report;
            _input = input;
            _rules = rules;
            _llmClient = llmClient;
            // _markdownParser = markdownParser;
        }

        public async Task  Scan( string path, string outputPath="")
        {
            
            List<RuleResult> result=new List<RuleResult>();
            if(string.IsNullOrEmpty(path))
            {
               throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }
            //Parse the skill document and get SkillData
            var skillData = _input.ProcessInput(path);

            if (skillData == null)
            {
                throw new InvalidOperationException("Failed to parse the skill content.");
            }
            //Evaluate the rules on the SkillData
           foreach (var rule in _rules)
            {
                var ruleResults = await rule.EvaluateAsync(skillData, _llmClient);
                result.AddRange(ruleResults);
            }

        Dictionary<int, List<RuleResult>> resultsByRuleType = result
                                        .GroupBy(r => r.RuleType.Id)
                                       .ToDictionary(
                                        g => g.Key,
                                        g => g.ToList());

        //Generate the report based on the results
        _report.GenerateReport(resultsByRuleType, path);
            
                       
        }
    }

