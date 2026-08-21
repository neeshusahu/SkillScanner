public static class RecallExtensions
{
    public static bool MeetsThreshold(this double recall, double threshold = 0.8)
        => recall >= threshold;
}