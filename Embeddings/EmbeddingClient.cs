
public class EmbeddingClient : IEmbeddingClient
{

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        // deterministic fake vector, just to exercise the seeding/storage path
        var rng = new Random(text.GetHashCode());
        var vector = new float[768];
        for (int i = 0; i < 768; i++) vector[i] = (float)rng.NextDouble();
        return Task.FromResult(vector);
    }

}