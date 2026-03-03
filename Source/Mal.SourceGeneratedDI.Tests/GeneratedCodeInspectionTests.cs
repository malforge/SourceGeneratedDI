using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class GeneratedCodeInspectionTests : GeneratorTestBase
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
        Assert.That(generatedCode, Does.Contain("public sealed partial class GeneratedRegistry"));
        Assert.That(generatedCode, Does.Contain("public void Contribute(global::Mal.SourceGeneratedDI.IServiceRegistry registry)"));
        Assert.That(generatedCode, Does.Contain("static partial void AddManualFactories(global::Mal.SourceGeneratedDI.IServiceRegistry registry);"));
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
        Assert.That(generatedCode, Does.Contain("public sealed partial class GeneratedRegistry"));
        Assert.That(generatedCode, Does.Not.Contain("ContainerFactory"));
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
        Assert.That(generatedCode, Does.Contain("internal sealed partial class GeneratedRegistry"));
        Assert.That(generatedCode, Does.Not.Contain("ContainerFactory"));
    }

    private string GenerateCode(string source, string fileHint = "GeneratedRegistry.g.cs")
        => GenerateFile(source, fileHint);
}