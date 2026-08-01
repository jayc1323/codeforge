namespace CodeForge.Core.Languages;

public static class LanguageRegistry
{
    private static readonly IReadOnlyDictionary<string, LanguageDefinition> Languages =
        new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = new LanguageDefinition(
                Id: "python",
                DisplayName: "Python",
                SourceFileName: "main.py",
                Compile: null,
                Run: new CommandTemplate("python3", "main.py"),
                VersionCommand: new CommandTemplate("python3", "--version"),
                DocsUrl: "https://docs.python.org/3/"),

            ["cpp"] = new LanguageDefinition(
                Id: "cpp",
                DisplayName: "C++",
                SourceFileName: "main.cpp",
                Compile: new CommandTemplate("g++", "-O2 -std=c++20 -o main main.cpp"),
                Run: new CommandTemplate("./main", ""),
                VersionCommand: new CommandTemplate("g++", "--version"),
                DocsUrl: "https://en.cppreference.com/w/"),

            ["csharp"] = new LanguageDefinition(
                Id: "csharp",
                DisplayName: "C#",
                SourceFileName: "Program.cs",
                Compile: new CommandTemplate("dotnet", "build -c Release -v q --nologo"),
                Run: new CommandTemplate("dotnet", "bin/Release/net8.0/Main.dll"),
                VersionCommand: new CommandTemplate("dotnet", "--version"),
                DocsUrl: "https://learn.microsoft.com/en-us/dotnet/csharp/",
                ExtraFiles: new Dictionary<string, string>
                {
                    ["Main.csproj"] = """
                        <Project Sdk="Microsoft.NET.Sdk">
                          <PropertyGroup>
                            <OutputType>Exe</OutputType>
                            <TargetFramework>net8.0</TargetFramework>
                            <ImplicitUsings>enable</ImplicitUsings>
                            <Nullable>enable</Nullable>
                            <AssemblyName>Main</AssemblyName>
                          </PropertyGroup>
                        </Project>
                        """
                },
                CompileTimeout: TimeSpan.FromSeconds(120)),

            ["fsharp"] = new LanguageDefinition(
                Id: "fsharp",
                DisplayName: "F#",
                SourceFileName: "main.fsx",
                Compile: null,
                Run: new CommandTemplate("dotnet", "fsi main.fsx"),
                VersionCommand: new CommandTemplate("dotnet", "fsi --version"),
                DocsUrl: "https://learn.microsoft.com/en-us/dotnet/fsharp/",
                RunTimeout: TimeSpan.FromSeconds(30)),

            ["typescript"] = new LanguageDefinition(
                Id: "typescript",
                DisplayName: "TypeScript",
                SourceFileName: "main.ts",
                Compile: null,
                Run: new CommandTemplate("tsx", "main.ts"),
                VersionCommand: new CommandTemplate("tsc", "--version"),
                DocsUrl: "https://www.typescriptlang.org/docs/"),
        };

    public static LanguageDefinition? Find(string id) =>
        Languages.TryGetValue(id, out var definition) ? definition : null;

    public static IEnumerable<LanguageDefinition> All => Languages.Values;
}
