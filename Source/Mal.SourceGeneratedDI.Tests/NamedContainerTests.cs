namespace Mal.SourceGeneratedDI;

[TestFixture]
public class NamedContainerTests : GeneratorTestBase
{
    [Test]
    public void NamedContainer_ProducesNamedRegistryClass()
    {
        var source = """
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton(Container = "Window")]
                public class OverlayService { }
            }
            """;

        var windowRegistry = GenerateFile(source, "WindowGeneratedRegistry.g.cs");

        Assert.That(windowRegistry, Does.Contain("sealed partial class WindowGeneratedRegistry"));
        Assert.That(windowRegistry, Does.Contain("TestNamespace.OverlayService"));
    }

    [Test]
    public void NamedContainer_DefaultRegistryAlwaysGenerated()
    {
        var source = """
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton(Container = "Window")]
                public class OverlayService { }
            }
            """;

        var files = GenerateAllFiles(source);

        Assert.That(files, Contains.Key("GeneratedRegistry.g.cs"),
            "Default registry must always be emitted, even when all services are in named containers");
    }

    [Test]
    public void NamedContainer_BothRegistriesProducedWhenMixing()
    {
        var source = """
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton]
                public class GlobalService { }

                [Singleton(Container = "Window")]
                public class OverlayService { }
            }
            """;

        var files = GenerateAllFiles(source);

        Assert.That(files, Contains.Key("GeneratedRegistry.g.cs"));
        Assert.That(files, Contains.Key("WindowGeneratedRegistry.g.cs"));
        Assert.That(files["GeneratedRegistry.g.cs"], Does.Contain("TestNamespace.GlobalService"));
        Assert.That(files["WindowGeneratedRegistry.g.cs"], Does.Contain("TestNamespace.OverlayService"));
        Assert.That(files["GeneratedRegistry.g.cs"], Does.Not.Contain("OverlayService"));
        Assert.That(files["WindowGeneratedRegistry.g.cs"], Does.Not.Contain("GlobalService"));
    }

    [Test]
    public void NamedContainer_AssemblyLevelAttribute()
    {
        var source = """
            using Mal.SourceGeneratedDI;
            using TestNamespace;

            [assembly: Singleton<IOverlay, OverlayService>(Container = "Window")]

            namespace TestNamespace
            {
                public interface IOverlay { }
                public class OverlayService : IOverlay { }
            }
            """;

        var windowRegistry = GenerateFile(source, "WindowGeneratedRegistry.g.cs");

        Assert.That(windowRegistry, Does.Contain("sealed partial class WindowGeneratedRegistry"));
        Assert.That(windowRegistry, Does.Contain("TestNamespace.IOverlay"));
    }

    [Test]
    public void CustomNamespace_GeneratesInSpecifiedNamespace()
    {
        var source = """
            using Mal.SourceGeneratedDI;

            [assembly: DependencyContainerOptions(Namespace = "MyApp.DI")]

            namespace TestNamespace
            {
                [Singleton]
                public class MyService { }
            }
            """;

        var code = GenerateFile(source);

        Assert.That(code, Does.Contain("namespace MyApp.DI;"));
        Assert.That(code, Does.Contain("sealed partial class GeneratedRegistry"));
    }

    [Test]
    public void CustomPrefix_AppliedToAllRegistryClassNames()
    {
        var source = """
            using Mal.SourceGeneratedDI;

            [assembly: DependencyContainerOptions(Prefix = "Hub")]

            namespace TestNamespace
            {
                [Singleton]
                public class GlobalService { }

                [Singleton(Container = "Window")]
                public class WindowService { }
            }
            """;

        var files = GenerateAllFiles(source);

        Assert.That(files, Contains.Key("HubGeneratedRegistry.g.cs"));
        Assert.That(files, Contains.Key("HubWindowGeneratedRegistry.g.cs"));
        Assert.That(files["HubGeneratedRegistry.g.cs"], Does.Contain("class HubGeneratedRegistry"));
        Assert.That(files["HubWindowGeneratedRegistry.g.cs"], Does.Contain("class HubWindowGeneratedRegistry"));
    }

    [Test]
    public void DefaultRegistry_IsInAssemblyRootNamespace()
    {
        var source = """
            using Mal.SourceGeneratedDI;
            namespace TestNamespace
            {
                [Singleton]
                public class MyService { }
            }
            """;

        // Compilation assembly name is "TestAssembly" — that becomes the default namespace
        var code = GenerateFile(source);

        Assert.That(code, Does.Contain("namespace TestAssembly;"));
    }
}
