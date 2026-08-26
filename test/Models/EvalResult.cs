public record EvalResult
{
    public string Filename { get; init; } = "";
    public bool GroundTruth { get; init; }
    public bool Predicted { get; init; }
    public bool IsMatch => GroundTruth == Predicted;
}