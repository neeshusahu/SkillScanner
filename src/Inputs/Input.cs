
using Markdig.Syntax;

public class Input : IInput
{

    private IParser<SkillData> _parser;
    private IParser<MarkdownDocument> _markdownParser;

    public Input( IParser<SkillData> parser, IParser<MarkdownDocument> markdownParser)
    {
        
        _parser = parser;
        _markdownParser = markdownParser;
    }

    // Implement the GetSkillDocument method to read the skill document from the specified path
    private SkillDocument GetSkillDocument(string path)
    {
        // Implement logic to read the skill document from the specified path
        var yamlContent = File.ReadAllText(path);
        //Split the content into metadata and skill content
        var lastYamlIndex = yamlContent.IndexOf("---", 3);
        var skillMetadata = yamlContent.Substring(0, lastYamlIndex);
        var skillContent = yamlContent.Substring(lastYamlIndex + 3);
        return new SkillDocument
        {
            SkillMetadata = skillMetadata,
            SkillContent = skillContent
        };
    }
    // Implement the ProcessInput method to parse the skill document and return SkillData
    public async Task<SkillData?> ProcessInputAsync(string path)
    {
        var skillDocument = GetSkillDocument(path);


        SkillData? skillData = await _parser.ParseAsync(skillDocument.SkillMetadata) as SkillData;
        
        if (skillData != null)
        {
            skillData.SkillMarkdownContent= skillDocument.SkillMetadata;
            skillData.SkillMarkdown = await _markdownParser.ParseAsync(skillDocument.SkillContent);
        }
        return skillData;


    }

}
