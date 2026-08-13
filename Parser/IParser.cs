namespace SkillScanner.Parser;
public interface IParser<T>
{
   Task<T> ParseAsync(string? data);
}