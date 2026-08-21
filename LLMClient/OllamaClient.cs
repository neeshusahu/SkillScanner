using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Markdig.Syntax;

namespace SkillScanner.LLMClient;
public class OllamaClient : ILLMClient
{
    private readonly HttpClient _httpClient;
     private const string Model = "phi4-mini";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
       PropertyNameCaseInsensitive = true
    };
    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
       //_httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<LLMVerdict> GetResponseAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {  var request = new
        {
            model = Model,
            system = systemPrompt,
            prompt = userPrompt,
            format = "json",
            stream = false,
            options=new {temperature=0}
        };

          HttpResponseMessage response;
          try
            {
                response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw new OperationCanceledException("The request was canceled.", ex, cancellationToken);
            }
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error from Ollama API: {response.ReasonPhrase}");
        }
       var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);
       if (ollamaResponse == null)
        {
            throw new Exception("Failed to read the response from Ollama API.");
        }
        var result = JsonSerializer.Deserialize<LLMVerdict>(ollamaResponse.Response, JsonSerializerOptions);

   
        if (result == null)
        {
            throw new Exception("Failed to deserialize the response from Ollama API.");
        }
        //Console.WriteLine($"Ollama API Response: {ollamaResponse.Response}");
        return new LLMVerdict
        {
            IsFlagged = result.IsFlagged,
            Confidence = result.Confidence,
            Reasoning = result.Reasoning,
            Source= VerdictSource.LLMGrounded
        };



    }
}