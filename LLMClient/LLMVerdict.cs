namespace SkillScanner.LLMClient;
public class LLMVerdict
{
   
    public bool IsFlagged { get; set; }

    public double Confidence { get; set; }

    public string Reasoning { get; set; } = string.Empty;
}

public class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
}