using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mal.SourceGeneratedDI;

[Generator]
public class DependencyRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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

        // Collect naming metadata
        var outputNames = context.CompilationProvider
            .Select(static (compilation, _) => GetOutputNames(compilation));

        // Combine all sources
        var allItems = classAttributes.Collect()
            .Combine(assemblyAttributes)
            .Select(static (pair, _) => pair.Left.AddRange(pair.Right))
            .Combine(containerOptions)
            .Combine(outputNames);

        context.RegisterSourceOutput(allItems,
            static (spc, data) =>
            {
                var ((items, options), names) = data;

                var producer = new RegistryProducer(items, options, names.RegistryClassName);
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
        var options = new ContainerOptions(true, false); // Default: public

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "DependencyContainerOptionsAttribute" or "DependencyContainerOptions")
            {
                // Check for Visibility property
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Visibility" && namedArg.Value.Value is int visibility)
                    {
                        options = new ContainerOptions(visibility == 0, options.EnableBuilder); // 0 = Public, 1 = Internal
                    }
                    else if (namedArg.Key == "Visibility" && namedArg.Value.Value is not null)
                    {
                        var visibilityValue = System.Convert.ToInt32(namedArg.Value.Value, CultureInfo.InvariantCulture);
                        options = new ContainerOptions(visibilityValue == 0, options.EnableBuilder);
                    }
                    else if (namedArg.Key == "EnableBuilder" && namedArg.Value.Value is bool enableBuilder)
                    {
                        // Kept for backwards compatibility with existing attribute payloads.
                        options = new ContainerOptions(options.IsPublic, enableBuilder);
                    }
                }
            }
        }

        return options;
    }
    
    private static OutputNames GetOutputNames(Compilation compilation)
    {
        var assemblyName = compilation.AssemblyName ?? "Assembly";
        var sanitized = SanitizeIdentifier(assemblyName);
        var registryClassName = $"{sanitized}GeneratedRegistry";
        return new OutputNames(registryClassName);
    }
    
    private static string SanitizeIdentifier(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "Assembly";

        var builder = new StringBuilder(input.Length);
        var first = input[0];
        builder.Append(char.IsLetter(first) || first == '_' ? first : '_');

        for (var i = 1; i < input.Length; i++)
        {
            var c = input[i];
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return builder.ToString();
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
    
    public readonly struct OutputNames
    {
        public string RegistryClassName { get; }
        
        public OutputNames(string registryClassName)
        {
            RegistryClassName = registryClassName;
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