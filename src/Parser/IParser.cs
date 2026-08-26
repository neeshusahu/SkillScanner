
public interface IParser<T>
{
   Task<T> ParseAsync(string? data);
}