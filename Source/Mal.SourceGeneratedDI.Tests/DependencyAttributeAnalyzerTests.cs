using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class DependencyAttributeAnalyzerTests : GeneratorTestBase
{
    private static CSharpAnalyzerTest<DependencyAttributeAnalyzer, DefaultVerifier> AnalyzerTest(string source)
    {
        var test = new CSharpAnalyzerTest<DependencyAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(IDependencyContainer).Assembly.Location));
        return test;
    }

    // --- Happy paths ---

    [Test]
    public async Task NoError_WhenOnlySingleton()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                using Mal.SourceGeneratedDI;
                
                namespace TestNamespace
                {
                    [Singleton]
                    public class MyService
                    {
                    }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(NoError_WhenOnlySingleton));
    }

    [Test]
    public async Task NoError_WhenOnlyInstance()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                using Mal.SourceGeneratedDI;
                
                namespace TestNamespace
                {
                    [Instance]
                    public class MyService
                    {
                    }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(NoError_WhenOnlyInstance));
    }

    [Test]
    public async Task NoError_WhenValidInterface()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                using Mal.SourceGeneratedDI;
                
                namespace TestNamespace
                {
                    public interface IService { }

                    [Singleton<IService>]
                    public class MyService : IService
                    {
                    }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(NoError_WhenValidInterface));
    }

    [Test]
    public async Task NoError_WhenValidBaseClass()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                using Mal.SourceGeneratedDI;
                
                namespace TestNamespace
                {
                    public class BaseService { }

                    [Singleton<BaseService>]
                    public class MyService : BaseService
                    {
                    }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(NoError_WhenValidBaseClass));
    }

    [Test]
    public async Task NoError_WhenValidContainerName()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton(Container = "Window")]
                public class MyService { }
            }
            """);

        await test.RunAsync();
    }

    // --- DI001: mixed lifetime ---

    [Test]
    public async Task DI001_Error_WhenMixedLifetime()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton]
                [Instance]
                public class {|#0:MyService|} { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.MixedLifetimeDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyService"));

        await test.RunAsync();
    }

    // --- DI002: invalid service type ---

    [Test]
    public async Task DI002_Error_WhenServiceTypeNotAssignable()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                public interface IUnrelated { }

                [{|#0:Singleton<IUnrelated>|}]
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidServiceTypeDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyService", "TestNamespace.IUnrelated"));

        await test.RunAsync();
    }

    // --- DI003: redundant self-registration ---

    [Test]
    public async Task DI003_Warning_WhenRedundantSelfRegistration()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [{|#0:Singleton<MyService>|}]
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.RedundantServiceTypeDiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("MyService", "Singleton"));

        await test.RunAsync();
    }

    // --- DI004: invalid container name ---

    [Test]
    public async Task DI004_Error_WhenContainerNameStartsWithDigit()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [{|#0:Singleton(Container = "123Invalid")|}]
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidContainerNameDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("123Invalid"));

        await test.RunAsync();
    }

    [Test]
    public async Task DI004_Error_WhenContainerNameHasSpaces()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [{|#0:Singleton(Container = "My Window")|}]
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidContainerNameDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("My Window"));

        await test.RunAsync();
    }

    // --- DI005: invalid prefix ---

    [Test]
    public async Task DI005_Error_WhenPrefixIsInvalid()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;

            [assembly: {|#0:DependencyContainerOptions(Prefix = "123Bad")|}]

            namespace TestNamespace
            {
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidPrefixDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("123Bad"));

        await test.RunAsync();
    }

    // --- DI006: invalid namespace ---

    [Test]
    public async Task DI006_Error_WhenNamespaceIsInvalid()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;

            [assembly: {|#0:DependencyContainerOptions(Namespace = "My.Bad Namespace")|}]

            namespace TestNamespace
            {
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidNamespaceDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("My.Bad Namespace"));

        await test.RunAsync();
    }

    [Test]
    public async Task DI006_Error_WhenNamespaceSegmentStartsWithDigit()
    {
        var test = AnalyzerTest("""
            using Mal.SourceGeneratedDI;

            [assembly: {|#0:DependencyContainerOptions(Namespace = "My.2App")|}]

            namespace TestNamespace
            {
                public class MyService { }
            }
            """);

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(DependencyAttributeAnalyzer.InvalidNamespaceDiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("My.2App"));

        await test.RunAsync();
    }
}

