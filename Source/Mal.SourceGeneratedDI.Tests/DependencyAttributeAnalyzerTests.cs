using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class DependencyAttributeAnalyzerTests : GeneratorTestBase
{
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
}

