using Microsoft.Extensions.DependencyInjection;
using SkillScanner.ExceptionHandler;
using SkillScanner.Inputs;
using SkillScanner.LLMClient;
using SkillScanner.Mapping;
using SkillScanner.Output;
using SkillScanner.Parser;
using SkillScanner.SkillRule;

namespace SkillScanner.Registrations;
public static class DependencyInjection
{
    public static IServiceCollection AddSkillScanner(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IMapper<>), typeof(ReflectionMapper<>));
        services.AddTransient<IReport, Report>();
        services.AddTransient<IParser<SkillData>, YamlParser>();
        services.AddTransient<IParser<string>, MarkDownParser>();
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
            
        return services;
    }
}