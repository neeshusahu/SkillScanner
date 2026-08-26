using System.Data;
using Dapper;
using Markdig.Syntax;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;



public static class DependencyInjection
{
    public static IServiceCollection AddSkillScanner(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IMapper<>), typeof(ReflectionMapper<>));
        services.AddTransient<IReport, Report>();
        services.AddTransient<IParser<SkillData>, YamlParser>();
        services.AddTransient<IParser<MarkdownDocument>, MarkDownParser>();
        
        services.AddTransient<IInput, Input>();
        services.AddTransient<IExceptionHandler, ConsoleExceptionHandler>();

        var ruleType = typeof(IRule);

        foreach (var type in ruleType.Assembly.GetTypes()
                     .Where(t => t.IsClass &&
                                 !t.IsAbstract &&
                                 ruleType.IsAssignableFrom(t)))
        {
            services.AddTransient(ruleType, type);
        }
        services.AddTransient<Scanner>();

        services.AddHttpClient<ILLMClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
        });
        
        services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(client=>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
        });
        services.AddSingleton<IVectorRepository, VectorRepository>();

        services.AddSingleton<IDbConnection>(sp =>
         {
             var dbPath = Path.Combine(AppContext.BaseDirectory, "skillscanner.db");
             var connection = new SqliteConnection($"Data Source={dbPath}");
             connection.Open();
             connection.EnableExtensions(true);
             connection.LoadExtension("vec0");

             // Enable WAL so reads (QueryNearestAsync) aren't blocked by an in-progress write,
             // and set a busy timeout so writers wait/retry instead of throwing immediately
             // if the parallel scan path is ever re-enabled.
             connection.Execute("PRAGMA journal_mode=WAL;");
             connection.Execute("PRAGMA busy_timeout=5000;");

             return connection;
         });

         services.AddTransient<IMarkdownChunker, MarkdownChunker>();
     
        return services;
    }
}