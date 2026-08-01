using SkillScanner.Mapping;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkillScanner.Parser;
public class YamlParser : IParser<SkillData>
{
    private readonly IDeserializer _deserializer;
    private readonly IMapper<SkillData> _mapper;
   

    public YamlParser(IMapper<SkillData> mapper)
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        _mapper = mapper;
       
    }

    public SkillData Parse(string ?skillMetadata)
    {
        if (string.IsNullOrEmpty(skillMetadata))
        {
            throw new ArgumentException("Skill metadata cannot be null or empty.", nameof(skillMetadata));
        }
        var skillMetaData= _deserializer.Deserialize<Dictionary<string, object>>(skillMetadata);
        SkillData skillData = _mapper.Map(skillMetaData, SkillMetadataMapping.Properties);
       return skillData;
    }

    
}