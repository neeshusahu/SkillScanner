using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

[TestFixture]
public class VectorDbSetupTests
{
    private IDbConnection connection;

    private IEmbeddingClient embeddingClient;

    private IVectorRepository vectorRepository;
    private string _dbPath;
     
     //OneTimeSetUp vs SetUp
     //Instead of amking the DB test isolated I'm using one time set up which would make test test depend on each other
    [OneTimeSetUp]
    public async Task Setup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vectest_{Guid.NewGuid()}.db");


        var sqliteConnection = new SqliteConnection($"Data Source={_dbPath}");
        sqliteConnection.Open();
        sqliteConnection.EnableExtensions(true);
        sqliteConnection.LoadExtension("vec0");

        sqliteConnection.Execute("PRAGMA journal_mode=WAL;");
        sqliteConnection.Execute("PRAGMA busy_timeout=5000;");
        connection = sqliteConnection;
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        embeddingClient = new OllamaEmbeddingClient(httpClient);

       vectorRepository=new VectorRepository(connection);
        
        await VectorCorpusSeeder.SeedAsync(vectorRepository, embeddingClient);


    }

    [Test]
    public async Task InitializeSchema_CreatesExpectedTables()
    {
        
        var tables = (await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type IN ('table','virtual table')"
        )).ToList();

        TestContext.WriteLine("Tables found: " + string.Join(", ", tables));

        Assert.That(tables, Does.Contain("Rules"));
        Assert.That(tables, Does.Contain("TextCorpus"));
        Assert.That(tables, Does.Contain("TextCorpusEmbeddings"));
    }

    [Test]
    public async Task SeedData_AddsExpectedEmbeddings()
    {
       

        var ruleCodes = (await connection.QueryAsync<string>(
            "SELECT RuleCode FROM Rules"
        )).ToList();

        Assert.That(ruleCodes, Does.Contain("AST03"));

        var textCorpusCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM TextCorpus"
        );
        Assert.That(textCorpusCount, Is.EqualTo(11)); // matches SeedData.OverprivilegedCorpus.Length

        var embeddingCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM TextCorpusEmbeddings"
        );
        Assert.That(embeddingCount, Is.EqualTo(11));
    }

   [Test]
    public async Task SearchSimilar_ReturnsExpectedResult()
    {
        string text= "I'm Neeshu";
        var embeddings=await embeddingClient.GetEmbeddingAsync(text);
        var result= await vectorRepository.SearchSimilarTextAsync(embeddings, 1);
        TestContext.Write("Result : {0}", result.First());
        Assert.That(result.Count, Is.EqualTo(3));

    }


    [OneTimeTearDown]
    public void TearDown()
    {
        connection?.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}