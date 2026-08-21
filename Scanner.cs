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
using System.Linq;
using System.Diagnostics;
using System.Collections.Concurrent;

public class Scanner
{
       
        private readonly IReport _report;
        private readonly IInput  _input;
        private readonly IEnumerable<IRule> _rules;

        private readonly IVectorRepository _vectorRepository;

        private readonly IEmbeddingClient _embeddingClient;

       
    

        public Scanner( IReport report, IInput input, IEnumerable<IRule> rules, ILLMClient llmClient, IVectorRepository vectorRepository, IEmbeddingClient embeddingClient)
        {
            //_mapper = mapper;
            //_yamlParser = yamlParser;
            _report = report;
            _input = input;
            _rules = rules;
            _vectorRepository=vectorRepository;
            _embeddingClient=embeddingClient;
          
            // _markdownParser = markdownParser;
        }
    //Sequentially scan the skill files in the given path and generate a report#region Name


    #region   Sequential Scan
    public async Task Scan(string path, string outputPath = "")

    {
       await VectorCorpusSeeder.SeedAsync(_vectorRepository, _embeddingClient);
       Dictionary<string, List<RuleResult>> result = new Dictionary<string, List<RuleResult>>();
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        //Parse the skill document and get SkillData
        foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories))
        {
            var stopwatch = Stopwatch.StartNew();
            Console.WriteLine($"Processing file at Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {file}");
            var skillData = await _input.ProcessInputAsync(file);

            if (skillData == null)
            {
                throw new InvalidOperationException($"Failed to parse the skill content from file: {file}");
            }
              foreach (var rule in _rules)
                {
                    //var ruleResults = await rule.EvaluateAsync(skillData);
                    //Try with RAG
                    var ruleResults=await rule.EvaluateAsyncWithRAG(skillData);
                    if (!result.ContainsKey(file))
                    {
                        result[file] = new List<RuleResult>();
                    }
                    result[file].AddRange(ruleResults);
                }

            
            //Evaluate the rules on the SkillData

            Console.WriteLine($"Completed processing file at Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {file}");
            stopwatch.Stop();

            Console.WriteLine($"Total Time Taken For File {file}: {stopwatch.Elapsed}");

            _rules.First().CountCalls();
       

        //Generate the report based on the results
      
        }
  
       
         _report.GenerateReport(result);


    }

    
    #endregion

    #region Parallel Scan
//     // public async Task ScanParallel(string path, string outputPath = "")
//     {
//           ConcurrentBag<RuleResult> result = new ConcurrentBag<RuleResult>();
//           //BenchMarking the time taken for the entire scan process
//           var fileTimings = new ConcurrentBag<(string File, TimeSpan Elapsed, int CallCount)>();
// var overallStopwatch = Stopwatch.StartNew();
//         if (string.IsNullOrEmpty(path))
//         {
//             throw new ArgumentException("Path cannot be null or empty.", nameof(path));
//         }
//         //Parse the skill document and get SkillData
//         var parallelOptions = new ParallelOptions
//         {
//             MaxDegreeOfParallelism = 4 // Adjust this based on your system's capabilities
//         };
//        await Parallel.ForEachAsync(Directory.EnumerateFiles(path, "*.md", 
//        SearchOption.AllDirectories), parallelOptions, async (file, cancellationToken) =>
//         {
//             var stopwatch = Stopwatch.StartNew();
//             Console.WriteLine($"Processing file at Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {file}");
//             var skillData = await _input.ProcessInputAsync(file);

//             if (skillData == null)
//             {
//                 throw new InvalidOperationException($"Failed to parse the skill content from file: {file}");
//             }
//             foreach (var num in Enumerable.Range(1, 10))
//             {
//                 foreach (var rule in _rules)
//                 {
//                     var ruleResults = await rule.EvaluateAsync(skillData, _llmClient, cancellationToken);
//                     foreach (var ruleResult in ruleResults)
//                     {
//                         result.Add(ruleResult);
//                     }
//                 }
//             }

//             stopwatch.Stop();
//             fileTimings.Add((File: file, Elapsed: stopwatch.Elapsed, CallCount: _rules.Count()));
//             Console.WriteLine($"Completed processing file at Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {file}");
//             Console.WriteLine($"Total Time Taken For File {file}: {stopwatch.Elapsed}");
//         });
//         overallStopwatch.Stop();
//         Console.WriteLine($"Total Time Taken For All Files: {overallStopwatch.Elapsed}");
//         _rules.First().CountCalls();
//         Dictionary<int, List<RuleResult>> resultsByRuleType = result
//                                         .GroupBy(r => r.RuleType.Id)
//                                        .ToDictionary(
//                                         g => g.Key,
//                                         g => g.ToList());

//         //Generate the report based on the results
//         _report.GenerateReport(resultsByRuleType, path);
//     }
    #endregion
}

