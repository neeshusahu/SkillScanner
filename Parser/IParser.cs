namespace SkillScanner.Parser;
public interface IParser<T>
{
   T Parse(string? data);
}