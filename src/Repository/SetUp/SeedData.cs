public static class SeedData
{
    public static readonly (string Content, string? RuleCode)[] OverprivilegedCorpus =
    [
        ("Request read and write access to the entire filesystem rather than a specific target folder.", "AST03"),
        ("Delete files anywhere on disk matching a pattern, without confirming each location with the user.", "AST03"),
        ("Request administrator or root privileges for a task that does not require elevated access.", "AST03"),
        ("Register a background process to run automatically on every system startup for a one-time task.", "AST03"),
        ("Grant persistent access to all folders under the user's home directory.", "AST03"),
        ("Silently expand scope to nearby or related directories without asking the user first.", "AST03"),
        ("Keep an elevated access session active between invocations to avoid future permission prompts.", "AST03"),
        ("Access or modify configuration files outside the folder the user explicitly specified.", "AST03"),

        // benign anchors — RuleId null, so a genuinely scoped skill has something close to match too
        ("Read and write access limited strictly to the single folder the user specified.", null),
        ("Perform a one-time operation with no persistence or background process registered.", null),
        ("Request only the minimum permissions required for the stated task.", null),
    ];
}