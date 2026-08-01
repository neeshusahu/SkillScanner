namespace SkillScanner.Inputs;
public interface IInput
{
    SkillData? ProcessInput(string path);
}