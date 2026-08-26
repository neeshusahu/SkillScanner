using System.CommandLine.Completions;
using System.Data;
using System.Text.Json;
using Dapper;
using Markdig.Syntax;
using Microsoft.Data.Sqlite;


public class OverprivilegedWithLLMFallbackTest

{
    private const string EvalSetDirectory = "EvalSet";
    private IEnumerable<EvalData> _evalData;

    private IParser<SkillData> yamlParser;
    private ILLMClient llmClient;
    private IParser<MarkdownDocument> markDownParser;
    private IInput input;
    private IRule overprivilegedRule;

    private IEmbeddingClient embeddingClient;

    private IVectorRepository vectorRepository;

    private IMarkdownChunker markdownChunker;

    private string _dbPath;
    private IDbConnection connection;

    private double truePositives = 0.0;
    private double falsePositives = 0.0;

    private double trueNegatives = 0.0;
    private double falseNegatives = 0.0;

    private IList<EvalResult> _evalResults;


    [OneTimeSetUp]
    public async Task Setup()
    {
        var fileData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"EvalSet", "ground_truth.json"));
        _evalData = JsonSerializer.Deserialize<IEnumerable<EvalData>>(fileData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<EvalData>();
        //Instantiate the Input class 
        IMapper<SkillData> mapper = new ReflectionMapper<SkillData>();
        yamlParser = new YamlParser(mapper);
        markDownParser = new MarkDownParser();
        input = new Input( yamlParser, markDownParser);
        //Instantiate the LLMClient
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        llmClient = new OllamaClient(httpClient);

         _dbPath = Path.Combine(Path.GetTempPath(), $"vectest_{Guid.NewGuid()}.db");


        var sqliteConnection = new SqliteConnection($"Data Source={_dbPath}");
        sqliteConnection.Open();
        sqliteConnection.EnableExtensions(true);
        sqliteConnection.LoadExtension("vec0");

        sqliteConnection.Execute("PRAGMA journal_mode=WAL;");
        sqliteConnection.Execute("PRAGMA busy_timeout=5000;");
        connection = sqliteConnection;
        vectorRepository=new VectorRepository(connection);
        embeddingClient=new OllamaEmbeddingClient(httpClient);
        markdownChunker=new MarkdownChunker();
        //Instantiate the overprivileged rule
        overprivilegedRule = new Overprivileged(llmClient, vectorRepository, embeddingClient, markdownChunker);
        // Initialize the evaluation results list
        _evalResults = new List<EvalResult>();
        await EvaluateOverprivilegedRuleAsync();
         
    }

    [Test]
    public async Task OverpriviledgedRule_RecallRate_OnEvalSet()
    {

         TestContext.WriteLine($"(TN={trueNegatives}) (FN={falseNegatives})(TP={truePositives}, FP={falsePositives})");
        // Compare the results with the ground truth data to calculate recall.
      
        double recall = (double)(truePositives / (truePositives + falseNegatives));
        TestContext.WriteLine($"Recall: {recall:P2} (TP={truePositives}, FN={falseNegatives})");
        var result=RecallExtensions.MeetsThreshold(recall, 0.8);
        Assert.That(result,Is.True);
    }
    
   [Test]
    public async Task OverpriviledgedRule_PrecisionRate_OnEvalSet()
    {
        // Compare the results with the ground truth data to calculate precision.
        double precision = (double)(truePositives / (truePositives + falsePositives));
        TestContext.WriteLine($"Precision: {precision:P2} (TP={truePositives}, FP={falsePositives})");
         bool isPrecisonMeetsThreshold=precision>=0.5;
        Assert.That(isPrecisonMeetsThreshold, Is.True);
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
            var ruleResults =  await overprivilegedRule.EvaluateAsync(skillData);

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

    [OneTimeTearDown]
    public void TearDown()
    {
        connection?.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}


