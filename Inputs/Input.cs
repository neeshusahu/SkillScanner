using Markdig.Parsers;
using SkillScanner.Mapping;
using SkillScanner.Parser;
using YamlDotNet.Core;

namespace SkillScanner.Inputs;
public class Input : IInput
{
    private readonly IMapper<SkillData> _mapper;
    private IParser<SkillData> _parser;
    private IParser<string> _markdownParser;

    public Input(IMapper<SkillData> mapper, IParser<SkillData> parser, IParser<string> markdownParser)
    {
        _mapper = mapper;
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
        ;
        if (skillData != null)
        {
            skillData.SkillMarkDown = await _markdownParser.ParseAsync(skillDocument.SkillContent) as string;
        }
        return skillData;


    }

}
