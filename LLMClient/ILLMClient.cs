
namespace SkillScanner.LLMClient;
public interface ILLMClient
{
    Task<LLMVerdict> GetResponseAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}