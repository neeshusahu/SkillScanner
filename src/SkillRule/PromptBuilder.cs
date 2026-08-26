public static class PromptBuilder
{
    public static string BuildAmbiguousPrompt(string chunkContent, TextCorpusMatch? violationExample, TextCorpusMatch? benignExample)
    {
        var violationLine = violationExample is null
            ? "- Known VIOLATION example: none available"
            : $"""- Known VIOLATION example — Content: "{violationExample.Content}" | Cosine distance: {violationExample.Distance:F4}""";

        var benignLine = benignExample is null
            ? "- Known BENIGN example: none available"
            : $"""- Known BENIGN example — Content: "{benignExample.Content}" | Cosine distance: {benignExample.Distance:F4}""";

        return $"""

            Snippet under review:
            "{chunkContent}"

            This snippet is similarly close to two different reference examples. Cosine distance
            ranges from 0 (identical) to 1 (unrelated), so a lower distance means a closer match.

            {violationLine}
            {benignLine}

            Decide whether the snippet under review is more similar to the VIOLATION example or the
            BENIGN example, and whether it should be flagged as a violation.
            """;
    }
}