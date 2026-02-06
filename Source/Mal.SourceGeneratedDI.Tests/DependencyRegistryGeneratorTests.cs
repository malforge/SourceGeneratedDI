using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class DependencyRegistryGeneratorTests : GeneratorTestBase
{
    [Test]
    public async Task GeneratesAttributeDefinition_OnPostInitialization()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = "",
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesAttributeDefinition_OnPostInitialization));
    }

    [Test]
    public async Task GeneratesRegistry_ForSingletonAttribute()
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

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForSingletonAttribute));
    }

    [Test]
    public async Task GeneratesRegistry_ForInstanceAttribute()
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

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForInstanceAttribute));
    }

    [Test]
    public async Task GeneratesRegistry_WithInterface()
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

        await RunGeneratorTest(test, nameof(GeneratesRegistry_WithInterface));
    }

    [Test]
    public async Task GeneratesRegistry_ForMultipleServices()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                using Mal.SourceGeneratedDI;
                
                namespace TestNamespace
                {
                    [Singleton]
                    public class ServiceA { }

                    [Singleton]
                    public class ServiceB { }

                    [Instance]
                    public class ServiceC { }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForMultipleServices));
    }

    [Test]
    public async Task NoErrors_WhenNoAttributes()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestCode = """
                namespace TestNamespace
                {
                    public class MyService { }
                }
                """,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(NoErrors_WhenNoAttributes));
    }
}


