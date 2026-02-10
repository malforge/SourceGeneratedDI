using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class GeneratedCodeInspectionTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
        Directory.CreateDirectory(_outputDir);
        TestContext.Out.WriteLine($"Generated code will be written to: {_outputDir}");
    }

    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "SourceGeneratedDI_Inspection");

    [Test]
    public void GenerateAndInspect_BuilderEnabled()
    {
        var source = """
                     using System;
                     using Mal.SourceGeneratedDI;

                     [assembly: DependencyContainerOptions(EnableBuilder = true, Visibility = ContainerVisibility.Public)]

                     namespace TestNamespace
                     {
                         [Singleton]
                         public class TestService { }
                     }
                     """;

        var generatedCode = GenerateCode(source);
        var outputPath = Path.Combine(_outputDir, "BuilderEnabled.g.cs");
        File.WriteAllText(outputPath, generatedCode);

        TestContext.Out.WriteLine($"✓ Builder ENABLED code written to: {outputPath}");
        Assert.That(generatedCode, Does.Contain("public sealed partial class TestAssemblyGeneratedRegistry"));
        Assert.That(generatedCode, Does.Contain("public void Contribute(IServiceRegistry registry)"));
        Assert.That(generatedCode, Does.Contain("static partial void AddManualFactories(IServiceRegistry registry);"));
    }

    [Test]
    public void GenerateAndInspect_BuilderDisabled()
    {
        var source = """
                     using System;
                     using Mal.SourceGeneratedDI;

                     namespace TestNamespace
                     {
                         [Singleton]
                         public class TestService { }
                     }
                     """;

        var generatedCode = GenerateCode(source);
        var outputPath = Path.Combine(_outputDir, "BuilderDisabled.g.cs");
        File.WriteAllText(outputPath, generatedCode);

        TestContext.Out.WriteLine($"✓ Builder DISABLED code written to: {outputPath}");
        Assert.That(generatedCode, Does.Contain("public static class TestAssemblyGeneratedContainerFactory"));
        Assert.That(generatedCode, Does.Contain("builder.AddRegistry(new TestAssemblyGeneratedRegistry());"));
    }

    [Test]
    public void GenerateAndInspect_InternalVisibility()
    {
        var source = """
                     using System;
                     using Mal.SourceGeneratedDI;

                     [assembly: DependencyContainerOptions(Visibility = ContainerVisibility.Internal)]

                     namespace TestNamespace
                     {
                         [Singleton]
                         public class TestService { }
                     }
                     """;

        var generatedCode = GenerateCode(source);
        var outputPath = Path.Combine(_outputDir, "InternalVisibility.g.cs");
        File.WriteAllText(outputPath, generatedCode);

        TestContext.Out.WriteLine($"✓ INTERNAL visibility code written to: {outputPath}");
        Assert.That(generatedCode, Does.Contain("TestAssemblyGeneratedRegistry"));
        Assert.That(generatedCode, Does.Contain("TestAssemblyGeneratedContainerFactory"));
    }

    private string GenerateCode(string source)
    {
        // Create a compilation
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDependencyContainer).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new DependencyRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        // Get the generated source
        var runResult = driver.GetRunResult();
        var generatedTree =
            runResult.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("DependencyRegistry.g.cs"));

        return generatedTree?.ToString() ?? "NO CODE GENERATED";
    }
}