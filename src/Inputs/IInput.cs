
public interface IInput
{
    Task<SkillData?> ProcessInputAsync(string path);
}