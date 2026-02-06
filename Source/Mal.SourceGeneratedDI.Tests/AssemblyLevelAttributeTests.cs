using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class AssemblyLevelAttributeTests : GeneratorTestBase
{
    [Test]
    public async Task GeneratesRegistry_ForAssemblyLevelSingleton()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    namespace TestNamespace
                    {
                        public class MyService
                        {
                        }
                    }
                    """,
                    """
                    using Mal.SourceGeneratedDI;
                    using TestNamespace;
                    
                    [assembly: Singleton<MyService, MyService>]
                    """
                }
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForAssemblyLevelSingleton));
    }

    [Test]
    public async Task GeneratesRegistry_ForAssemblyLevelInstance()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    namespace TestNamespace
                    {
                        public class MyService
                        {
                        }
                    }
                    """,
                    """
                    using Mal.SourceGeneratedDI;
                    using TestNamespace;
                    
                    [assembly: Instance<MyService, MyService>]
                    """
                }
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForAssemblyLevelInstance));
    }

    [Test]
    public async Task GeneratesRegistry_ForAssemblyLevelWithInterface()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    namespace TestNamespace
                    {
                        public interface IService { }

                        public class MyService : IService
                        {
                        }
                    }
                    """,
                    """
                    using Mal.SourceGeneratedDI;
                    using TestNamespace;
                    
                    [assembly: Singleton<IService, MyService>]
                    """
                }
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_ForAssemblyLevelWithInterface));
    }

    [Test]
    public async Task GeneratesRegistry_MixingClassAndAssemblyAttributes()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using Mal.SourceGeneratedDI;
                    
                    namespace TestNamespace
                    {
                        public interface IExternalService { }
                        
                        public class ExternalService : IExternalService { }

                        [Singleton]
                        public class LocalService { }
                    }
                    """,
                    """
                    using Mal.SourceGeneratedDI;
                    using TestNamespace;
                    
                    [assembly: Singleton<IExternalService, ExternalService>]
                    """
                }
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_MixingClassAndAssemblyAttributes));
    }

    [Test]
    public async Task GeneratesRegistry_MultipleAssemblyAttributes()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    namespace TestNamespace
                    {
                        public interface IServiceA { }
                        public class ServiceA : IServiceA { }
                        
                        public interface IServiceB { }
                        public class ServiceB : IServiceB { }
                        
                        public class ServiceC { }
                    }
                    """,
                    """
                    using Mal.SourceGeneratedDI;
                    using TestNamespace;
                    
                    [assembly: Singleton<IServiceA, ServiceA>]
                    [assembly: Instance<IServiceB, ServiceB>]
                    [assembly: Singleton<ServiceC, ServiceC>]
                    """
                }
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        await RunGeneratorTest(test, nameof(GeneratesRegistry_MultipleAssemblyAttributes));
    }
}
