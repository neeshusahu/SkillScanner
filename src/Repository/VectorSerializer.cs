using System.Runtime.InteropServices;



/// <summary>
/// Converts between float[] embeddings and the packed byte layout sqlite-vec
/// expects for its vec0 virtual table columns. Pure memory reinterpretation —
/// no semantic conversion, no normalization.
/// </summary>
public static class VectorSerializer
{
    public static byte[] ToBytes(float[] vector)
    {
        if (vector is null || vector.Length == 0)
            throw new ArgumentException("Vector must be non-null and non-empty.", nameof(vector));

        return MemoryMarshal.Cast<float, byte>(vector).ToArray();
    }

    public static float[] FromBytes(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("Bytes must be non-null and non-empty.", nameof(bytes));

        if (bytes.Length % sizeof(float) != 0)
            throw new ArgumentException(
                $"Byte length ({bytes.Length}) is not a multiple of sizeof(float) — malformed vector blob.",
                nameof(bytes));

        return MemoryMarshal.Cast<byte, float>(bytes).ToArray();
    }
}