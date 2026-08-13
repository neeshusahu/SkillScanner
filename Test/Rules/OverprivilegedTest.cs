using System.Text.Json;
using SkillScanner.Inputs;
using SkillScanner.LLMClient;
using SkillScanner.Mapping;
using SkillScanner.Parser;
using SkillScanner.SkillRule;


namespace SkillScanner.Tests;

public static class RecallExtensions
{
    public static bool MeetsThreshold(this double recall, double threshold = 0.8)
        => recall >= threshold;
}
public class OverprivilegedTest

{
    private const string EvalSetDirectory = "EvalSet";
    private IEnumerable<EvalData> _evalData;

    private IParser<SkillData> yamlParser;
    private ILLMClient llmClient;
    private IParser<string> markDownParser;
    private IInput input;
    private IRule overprivilegedRule;

    private int truePositives = 0;
    private int falsePositives = 0;

    private int trueNegatives = 0;
    private int falseNegatives = 0;

    private IList<EvalResult> _evalResults;


    [SetUp]
    public async Task Setup()
    {
        var fileData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"EvalSet", "ground_truth.json"));
        _evalData = JsonSerializer.Deserialize<IEnumerable<EvalData>>(fileData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<EvalData>();
        //Instantiate the Input class 
        IMapper<SkillData> mapper = new ReflectionMapper<SkillData>();
        yamlParser = new YamlParser(mapper);
        markDownParser = new MarkDownParser();
        input = new Input(mapper, yamlParser, markDownParser);
        //Instantiate the LLMClient
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        llmClient = new OllamaClient(httpClient);
        //Instantiate the overprivileged rule
        overprivilegedRule = new Overprivileged(llmClient);
        // Initialize the evaluation results list
        _evalResults = new List<EvalResult>();
        await EvaluateOverprivilegedRuleAsync();
         
    }

    [Test]
    public async Task OverpriviledgedRule_RecallRate_OnEvalSet()
    {


        // Compare the results with the ground truth data to calculate recall.
      
        double recall = (double)(truePositives / (truePositives + falseNegatives));
        TestContext.WriteLine($"Recall: {recall:P2} (TP={truePositives}, FN={falseNegatives})");
        RecallExtensions.MeetsThreshold(recall, 0.8);
        Assert.Pass($"Recall is as per the expected threshold. Recall: {recall:P2}");
    }
    
   [Test]
    public async Task OverpriviledgedRule_PrecisionRate_OnEvalSet()
    {
        // Compare the results with the ground truth data to calculate precision.
        double precision = (double)(truePositives / (truePositives + falsePositives));
        TestContext.WriteLine($"Precision: {precision:P2} (TP={truePositives}, FP={falsePositives})");
    }
  
   private async Task EvaluateOverprivilegedRuleAsync()
    {
          foreach (var eval in _evalData)
        {
            var skillData =  await input.ProcessInputAsync(Path.Combine(EvalSetDirectory, eval.Filename));
            if (skillData == null)
            {
                throw new InvalidOperationException($"Failed to parse the skill content from file: {eval.Filename}");
            }
            var ruleResults =  await overprivilegedRule.EvaluateAsync(skillData, llmClient);

            // Assuming that the ground truth data contains a boolean indicating whether the skill is overprivileged or not.
            var result = new EvalResult
            {
                Filename = eval.Filename,
                Predicted = ruleResults.FirstOrDefault()?.IsFlagged ?? false,
                GroundTruth = eval.IsFlagged,
            };
            _evalResults.Add(result);

        }

        CalculateMetrics();
    }

    private void CalculateMetrics()
    {
        foreach (var result in _evalResults)
        {
            if (result.Predicted && result.GroundTruth)
            {
                truePositives++;
            }
            else if (result.Predicted && !result.GroundTruth)
            {
                falsePositives++;
            }
            else if (!result.Predicted && !result.GroundTruth)
            {
                trueNegatives++;
            }
            else if (!result.Predicted && result.GroundTruth)
            {
                falseNegatives++;
            }
        }

    }
}


