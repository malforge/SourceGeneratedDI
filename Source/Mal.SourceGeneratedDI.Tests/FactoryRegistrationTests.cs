using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Mal.SourceGeneratedDI;

[TestFixture]
public class FactoryRegistrationTests : GeneratorTestBase
{
    [Test]
    public async Task SimpleFactoryRegistration_WithNoParameters()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        // Dummy attribute to trigger generator
                        [Singleton]
                        public class _Dummy { }
                    
                        public class SimpleService
                        {
                            public string Value { get; set; } = "Default";
                        }

                        public class Program
                        {
                            public static void Main()
                            {
                                var container = new DependencyContainer(b => b
                                    .Register<SimpleService>(() => new SimpleService { Value = "Custom" }));
                                    
                                var service = container.Resolve<SimpleService>();
                                Console.WriteLine(service.Value);
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(SimpleFactoryRegistration_WithNoParameters));
    }

    [Test]
    public async Task FactoryRegistration_WithContainerAccess()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        [Singleton]
                        public class DependencyService
                        {
                            public string Value => "Dependency";
                        }

                        public class CompositeService
                        {
                            public DependencyService Dependency { get; }
                            
                            public CompositeService(DependencyService dependency)
                            {
                                Dependency = dependency;
                            }
                        }

                        public class Program
                        {
                            public static void Main()
                            {
                                var container = new DependencyContainer(b => b
                                    .Register<CompositeService>(c => 
                                        new CompositeService(c.Resolve<DependencyService>())));
                                    
                                var service = container.Resolve<CompositeService>();
                                Console.WriteLine(service.Dependency.Value);
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(FactoryRegistration_WithContainerAccess));
    }

    [Test]
    public async Task FactoryRegistration_FluentChaining()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        // Dummy attribute to trigger generator
                        [Singleton]
                        public class _Dummy { }
                    
                        public interface IServiceA { }
                        public interface IServiceB { }
                        public interface IServiceC { }
                        
                        public class ServiceA : IServiceA { }
                        public class ServiceB : IServiceB { }
                        public class ServiceC : IServiceC { }

                        public class Program
                        {
                            public static void Main()
                            {
                                var container = new DependencyContainer(b => b
                                    .Register<IServiceA>(() => new ServiceA())
                                    .Register<IServiceB>(() => new ServiceB())
                                    .Register<IServiceC>(() => new ServiceC()));
                                    
                                var a = container.Resolve<IServiceA>();
                                var b = container.Resolve<IServiceB>();
                                var c = container.Resolve<IServiceC>();
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(FactoryRegistration_FluentChaining));
    }

    [Test]
    public async Task FactoryRegistration_MixedWithAttributes()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        [Singleton]
                        public class AttributeService
                        {
                            public string Value => "Attribute";
                        }

                        public class FactoryService
                        {
                            public string Value { get; set; } = "Factory";
                        }

                        public class Program
                        {
                            public static void Main()
                            {
                                var container = new DependencyContainer(b => b
                                    .Register<FactoryService>(() => new FactoryService { Value = "Custom" }));
                                    
                                var attrService = container.Resolve<AttributeService>();
                                var factService = container.Resolve<FactoryService>();
                                Console.WriteLine($"{{attrService.Value}}, {{factService.Value}}");
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(FactoryRegistration_MixedWithAttributes));
    }

    [Test]
    public async Task FactoryRegistration_OptionalBuilder()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        [Singleton]
                        public class AttributeService
                        {
                            public string Value => "Attribute";
                        }

                        public class Program
                        {
                            public static void Main()
                            {
                                // No builder, just attributes
                                var container = new DependencyContainer();
                                    
                                var service = container.Resolve<AttributeService>();
                                Console.WriteLine(service.Value);
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(FactoryRegistration_OptionalBuilder));
    }

    [Test]
    public async Task FactoryRegistration_ComplexDependencyGraph()
    {
        var test = new CSharpSourceGeneratorTest<DependencyRegistryGenerator, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using Mal.SourceGeneratedDI;

                    [assembly: DependencyContainerOptions(EnableBuilder = true)]

                    namespace TestNamespace
                    {
                        [Singleton]
                        public class BaseService
                        {
                            public string Value => "Base";
                        }

                        public class MiddleService
                        {
                            public BaseService Base { get; }
                            
                            public MiddleService(BaseService baseService)
                            {
                                Base = baseService;
                            }
                        }

                        public class TopService
                        {
                            public MiddleService Middle { get; }
                            public BaseService Base { get; }
                            
                            public TopService(MiddleService middle, BaseService baseService)
                            {
                                Middle = middle;
                                Base = baseService;
                            }
                        }

                        public class Program
                        {
                            public static void Main()
                            {
                                var container = new DependencyContainer(b => b
                                    .Register<MiddleService>(c => 
                                        new MiddleService(c.Resolve<BaseService>()))
                                    .Register<TopService>(c => 
                                        new TopService(c.Resolve<MiddleService>(), c.Resolve<BaseService>())));
                                    
                                var top = container.Resolve<TopService>();
                                Console.WriteLine(top.Base.Value);
                            }
                        }
                    }
                    """
                }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck
        };

        await RunGeneratorTest(test, nameof(FactoryRegistration_ComplexDependencyGraph));
    }
}
