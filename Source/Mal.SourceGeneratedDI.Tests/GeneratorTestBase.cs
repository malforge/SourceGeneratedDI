using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

/// <summary>
/// Base class for generator tests that automatically captures and outputs generated code to console.
/// </summary>
public abstract class GeneratorTestBase
{
    /// <summary>
    /// Runs a generator test and outputs the generated code to the console.
    /// </summary>
    protected async Task RunGeneratorTest(
        CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier> test,
        string testName)
    {
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IDependencyContainer).Assembly.Location));
        await test.RunAsync();
        
        // Also manually run generator to capture output
        var compilation = CreateCompilation(test.TestState.Sources.Select(s => s.content.ToString()).ToArray());
        var generator = new DependencyRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        
        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results[0].GeneratedSources;
        
        // Output each generated source to console
        TestContext.Out.WriteLine($"\n{new string('=', 80)}");
        TestContext.Out.WriteLine($"Generated Code for: {testName}");
        TestContext.Out.WriteLine($"{new string('=', 80)}");
        
        foreach (var source in generatedSources)
        {
            TestContext.Out.WriteLine($"\n--- {source.HintName} ---\n");
            TestContext.Out.WriteLine(source.SourceText.ToString());
        }
        
        TestContext.Out.WriteLine($"\n{new string('=', 80)}\n");
    }

    /// <summary>
    /// Runs the generator on the given source and returns the content of the named generated file.
    /// </summary>
    protected static string GenerateFile(string source, string fileHint = "GeneratedRegistry.g.cs")
    {
        var runResult = RunGenerator(source);
        var tree = runResult.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains(fileHint));
        return tree?.ToString() ?? "NO CODE GENERATED";
    }

    /// <summary>
    /// Runs the generator on the given source and returns all generated file contents keyed by hint name.
    /// </summary>
    protected static Dictionary<string, string> GenerateAllFiles(string source)
    {
        var runResult = RunGenerator(source);
        return runResult.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.ToString());
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(IDependencyContainer).Assembly.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DependencyRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    private Compilation CreateCompilation(string[] sources)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDependencyContainer).Assembly.Location),
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
