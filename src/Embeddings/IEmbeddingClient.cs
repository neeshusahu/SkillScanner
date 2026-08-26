public interface IEmbeddingClient
{
    Task<float[]> GetEmbeddingAsync(string text);
}