using Microsoft.Extensions.DependencyInjection;


public static class SkillMetadataMapping
{
    public static readonly Dictionary<string, string> Properties = new()
    {
        ["name"] = nameof(SkillData.Name),
        ["description"] = nameof(SkillData.Description),
        ["compatibility"] = nameof(SkillData.Compatibility)
    };
}