using System.Data;
using System.Text.Json;
using Dapper;
using Markdig.Syntax;
using Microsoft.Data.Sqlite;

public class OverprivilegedWithRAGTest
{

     private IMapper<SkillData> mapper;
     private IParser<SkillData> yamlParser;
     private IParser<MarkdownDocument> markDownParser;
     //Input Processsing
     private IInput input;

     private ILLMClient llmClient;

     private IEmbeddingClient embeddingClient;
     private IMarkdownChunker markdownChunker;

     private IVectorRepository vectorRepository;
     
     private string dbPath;
     private IDbConnection connection;

     private IRule overprivilegedRule;

     private IEnumerable<EvalData> evalData;
    private IList<EvalResult> evalResults;
    private const string EvalSetDirectory = "EvalSet";

    private double truePositives = 0.0;
    private double falsePositives = 0.0;

    private double trueNegatives = 0.0;
    private double falseNegatives = 0.0;
    
    [OneTimeSetUp]
     public async Task Setup()
    {
        //Intialize all the dependency
        #region Input & Parser
        IMapper<SkillData> mapper = new ReflectionMapper<SkillData>();
        yamlParser = new YamlParser(mapper);
        markDownParser = new MarkdownParser();
        input=new Input(yamlParser, markDownParser);
        #endregion
        #region LLM & Embedding
         var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        llmClient=new OllamaClient(httpClient);
         embeddingClient=new OllamaEmbeddingClient(httpClient);
         #endregion
        #region Chunking
        markdownChunker=new MarkdownChunker();
       #endregion
        #region VectorRepository
        dbPath = Path.Combine(Path.GetTempPath(), $"vectest_{Guid.NewGuid()}.db");


        var sqliteConnection = new SqliteConnection($"Data Source={dbPath}");
        sqliteConnection.Open();
        sqliteConnection.EnableExtensions(true);
        sqliteConnection.LoadExtension("vec0");

        sqliteConnection.Execute("PRAGMA journal_mode=WAL;");
        sqliteConnection.Execute("PRAGMA busy_timeout=5000;");
        connection = sqliteConnection;
        vectorRepository=new VectorRepository(connection);
        #endregion
        #region Overprivileged
        overprivilegedRule=new Overprivileged(llmClient, vectorRepository,embeddingClient, markdownChunker);
        await Initialize();
        await EvaluateOverprivilegedRuleWithRAGAsync();
        #endregion
    
    }

    private async Task Initialize()
    {
        await VectorCorpusSeeder.SeedAsync(vectorRepository, embeddingClient);
        var fileData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"EvalSet", "ground_truth.json"));
        evalData = JsonSerializer.Deserialize<IEnumerable<EvalData>>(fileData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<EvalData>();
        
    }
      private async Task EvaluateOverprivilegedRuleWithRAGAsync()
    {
         evalResults=new List<EvalResult>();
          foreach (var eval in evalData)
        {
            var skillData =  await input.ProcessInputAsync(Path.Combine(EvalSetDirectory, eval.Filename));
            if (skillData == null)
            {
                throw new InvalidOperationException($"Failed to parse the skill content from file: {eval.Filename}");
            }
            var ruleResults =  await overprivilegedRule.EvaluateAsyncWithRAG(skillData);

            // Assuming that the ground truth data contains a boolean indicating whether the skill is overprivileged or not.
            var result = new EvalResult
            {
                Filename = eval.Filename,
                Predicted = ruleResults.FirstOrDefault()?.IsFlagged ?? false,
                GroundTruth = eval.IsFlagged,
            };
            evalResults.Add(result);

        }

        CalculateMetrics();
    }
     private void CalculateMetrics()
    {
        foreach (var result in evalResults)
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
    [Test]
    public async Task OverpriviledgedRuleWithRAG_RecallRate_OnEvalSet()
    {


        // Compare the results with the ground truth data to calculate recall.
         TestContext.WriteLine($"(TN={trueNegatives}) (FN={falseNegatives})(TP={truePositives}, FP={falsePositives})");
      
        double recall = (double)(truePositives / (truePositives + falseNegatives));
        TestContext.WriteLine($"Recall: {recall:P2} (TP={truePositives}, FN={falseNegatives})");
         var result =RecallExtensions.MeetsThreshold(recall, 0.8);
         Assert.That(result, Is.True);
    
    }
    
   [Test]
    public async Task OverpriviledgedRuleWithRAG_PrecisionRate_OnEvalSet()
    {
        // Compare the results with the ground truth data to calculate precision.
        double precision = (double)(truePositives / (truePositives + falsePositives));
        TestContext.WriteLine($"Precision: {precision:P2} (TP={truePositives}, FP={falsePositives})");
        bool isPrecisonMeetsThreshold=precision>=0.5;
        Assert.That(isPrecisonMeetsThreshold, Is.True);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        connection?.Dispose();
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }


}