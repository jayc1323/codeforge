namespace CodeForge.Core.Languages;

public sealed record CommandTemplate(string FileName, string Arguments);

public sealed record LanguageDefinition(
    string Id,
    string DisplayName,
    string SourceFileName,
    CommandTemplate? Compile,
    CommandTemplate Run,
    CommandTemplate VersionCommand,
    string DocsUrl,
    IReadOnlyDictionary<string, string>? ExtraFiles = null,
    TimeSpan? CompileTimeout = null,
    TimeSpan? RunTimeout = null);
