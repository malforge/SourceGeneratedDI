using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mal.SourceGeneratedDI;

[Generator]
public class DependencyRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pregenerate the attribute
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("DependencyRegistryAttribute.g.cs", FrameworkProducer.Instance.Produce()));

        // Collect class-level attributes
        var classAttributes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => TransformClass(ctx))
            .Where(static x => x.HasValue)
            .SelectMany(static (items, _) => items!.Value);

        // Collect assembly-level attributes
        var assemblyAttributes = context.CompilationProvider
            .Select(static (compilation, _) => TransformAssembly(compilation));

        // Collect container options
        var containerOptions = context.CompilationProvider
            .Select(static (compilation, _) => GetContainerOptions(compilation));

        // Combine all sources
        var allItems = classAttributes.Collect()
            .Combine(assemblyAttributes)
            .Select(static (pair, _) => pair.Left.AddRange(pair.Right))
            .Combine(containerOptions);

        context.RegisterSourceOutput(allItems,
            static (spc, data) =>
            {
                var (items, options) = data;
                if (items.Length == 0) return;

                var producer = new RegistryProducer(items, options);
                spc.AddSource("DependencyRegistry.g.cs", producer.Produce());
            });
    }

    private static ImmutableArray<Item>? TransformClass(GeneratorSyntaxContext ctx)
    {
        var cds = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(cds) is not INamedTypeSymbol impl)
            return null;

        var items = ImmutableArray.CreateBuilder<Item>();

        foreach (var attr in impl.GetAttributes())
        {
            if (attr.AttributeClass is { Name: "SingletonAttribute" or "Singleton" or "InstanceAttribute" or "Instance" } ac)
            {
                var isInstance = ac.Name is "InstanceAttribute" or "Instance";
                switch (ac.Arity)
                {
                    case 0:
                        items.Add(new Item(impl, impl, isInstance));
                        break;

                    case 1:
                        var t = ac.TypeArguments[0];
                        items.Add(new Item(impl, t, isInstance));
                        break;

                    case > 1:
                        // Skip invalid attributes
                        break;
                }
            }
        }

        return items.Count > 0 ? items.ToImmutable() : null;
    }

    private static ImmutableArray<Item> TransformAssembly(Compilation compilation)
    {
        var items = ImmutableArray.CreateBuilder<Item>();

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass is { Name: "SingletonAttribute" or "Singleton" or "InstanceAttribute" or "Instance" } ac)
            {
                var isInstance = ac.Name is "InstanceAttribute" or "Instance";
                
                // Assembly-level attributes must have 2 type arguments
                if (ac.Arity == 2)
                {
                    var service = ac.TypeArguments[0];
                    var impl = ac.TypeArguments[1];
                    
                    if (impl is INamedTypeSymbol namedImpl)
                    {
                        items.Add(new Item(namedImpl, service, isInstance));
                    }
                }
            }
        }

        return items.ToImmutable();
    }

    private static ContainerOptions GetContainerOptions(Compilation compilation)
    {
        var options = new ContainerOptions(true, false); // Default: public, builder disabled

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "DependencyContainerOptionsAttribute")
            {
                // Check for Visibility property
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Visibility" && namedArg.Value.Value is int visibility)
                    {
                        options = new ContainerOptions(visibility == 0, options.EnableBuilder); // 0 = Public, 1 = Internal
                    }
                    else if (namedArg.Key == "EnableBuilder" && namedArg.Value.Value is bool enableBuilder)
                    {
                        options = new ContainerOptions(options.IsPublic, enableBuilder);
                    }
                }
            }
        }

        return options;
    }

    public readonly struct ContainerOptions
    {
        public bool IsPublic { get; }
        public bool EnableBuilder { get; }
        
        public ContainerOptions(bool isPublic, bool enableBuilder)
        {
            IsPublic = isPublic;
            EnableBuilder = enableBuilder;
        }
    }

    public readonly struct Item
    {
        public readonly INamedTypeSymbol Implementation;
        public readonly ITypeSymbol Service;
        public readonly bool IsInstance;

        public Item(INamedTypeSymbol implementation, ITypeSymbol service, bool isInstance)
        {
            Implementation = implementation;
            Service = service;
            IsInstance = isInstance;
        }
    }
}