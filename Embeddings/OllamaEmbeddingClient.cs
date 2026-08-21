
using System.Net.Http.Json;
public record OllamaEmbedResponse
{
    public List<float[]>? Embeddings { get; init; }
}

public class OllamaEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _client;

    public OllamaEmbeddingClient(HttpClient client)
    {
        _client=client;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var requestBody = new 
        {
            model="nomic-embed-text",
            input=text
        };

        var response=  await _client.PostAsJsonAsync("/api/embed", requestBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>();
        if (result?.Embeddings is null || result.Embeddings.Count == 0)
            throw new InvalidOperationException("Ollama returned no embedding for the given text.");
        //          result.Embeddings         → List<float[]> with 1 element
       // result.Embeddings.Count   → 1
       // result.Embeddings[0]      → float[768]  { -0.0347, 0.0085, 0.0235, ... }  (768 numbers)
       return result.Embeddings[0];
       
    }
}


