using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
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

                // Group items by container name (null = default/unnamed container)
                var byContainer = new System.Collections.Generic.Dictionary<string, ImmutableArray<Item>>();
                var grouped = items.GroupBy(i => i.ContainerName ?? "");
                foreach (var group in grouped)
                    byContainer[group.Key] = group.ToImmutableArray();

                // Ensure the default container always gets a file
                if (!byContainer.ContainsKey(""))
                    byContainer[""] = ImmutableArray<Item>.Empty;

                foreach (var kvp in byContainer)
                {
                    var containerName = kvp.Key;
                    var containerItems = kvp.Value;
                    var className = names.GetRegistryClassName(containerName);
                    var producer = new RegistryProducer(containerItems, options, className, names.Namespace);
                    var fileName = $"{className}.g.cs";
                    spc.AddSource(fileName, producer.Produce());
                }
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
                var containerName = GetContainerName(attr);

                switch (ac.Arity)
                {
                    case 0:
                        items.Add(new Item(impl, impl, isInstance, containerName));
                        break;

                    case 1:
                        var t = ac.TypeArguments[0];
                        items.Add(new Item(impl, t, isInstance, containerName));
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
                var containerName = GetContainerName(attr);

                // Assembly-level attributes must have 2 type arguments
                if (ac.Arity == 2)
                {
                    var service = ac.TypeArguments[0];
                    var impl = ac.TypeArguments[1];
                    
                    if (impl is INamedTypeSymbol namedImpl)
                    {
                        items.Add(new Item(namedImpl, service, isInstance, containerName));
                    }
                }
            }
        }

        return items.ToImmutable();
    }

    private static string? GetContainerName(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == "Container" && namedArg.Value.Value is string name && name.Length > 0)
                return name;
        }
        return null;
    }

    private static ContainerOptions GetContainerOptions(Compilation compilation)
    {
        var options = new ContainerOptions(true, false); // Default: public

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "DependencyContainerOptionsAttribute" or "DependencyContainerOptions")
            {
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Visibility" && namedArg.Value.Value is not null)
                    {
                        var v = System.Convert.ToInt32(namedArg.Value.Value, CultureInfo.InvariantCulture);
                        options = new ContainerOptions(v == 0, options.EnableBuilder);
                    }
                    else if (namedArg.Key == "EnableBuilder" && namedArg.Value.Value is bool enableBuilder)
                        options = new ContainerOptions(options.IsPublic, enableBuilder);
                }
            }
        }

        return options;
    }
    
    private static OutputNames GetOutputNames(Compilation compilation)
    {
        string? customNamespace = null;
        string? prefix = null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "DependencyContainerOptionsAttribute" or "DependencyContainerOptions")
            {
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Namespace" && namedArg.Value.Value is string ns && ns.Length > 0)
                        customNamespace = ns;
                    else if (namedArg.Key == "Prefix" && namedArg.Value.Value is string p)
                        prefix = p;
                }
            }
        }

        // Default namespace: assembly root namespace (first segment of assembly name, or full if no dots)
        var rootNamespace = customNamespace ?? GetRootNamespace(compilation);
        return new OutputNames(rootNamespace, prefix ?? "");
    }

    private static string GetRootNamespace(Compilation compilation)
    {
        // Use the assembly name as the namespace (it's typically the root namespace)
        return compilation.AssemblyName ?? "GeneratedRegistries";
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
        public string Namespace { get; }
        public string Prefix { get; }
        
        public OutputNames(string @namespace, string prefix)
        {
            Namespace = @namespace;
            Prefix = prefix;
        }

        /// <summary>
        /// Gets the registry class name for the given container name.
        /// Empty container name = default/unnamed registry.
        /// </summary>
        public string GetRegistryClassName(string containerName)
            => containerName == "" ? $"{Prefix}GeneratedRegistry" : $"{Prefix}{containerName}GeneratedRegistry";
    }

    public readonly struct Item
    {
        public readonly INamedTypeSymbol Implementation;
        public readonly ITypeSymbol Service;
        public readonly bool IsInstance;
        public readonly string? ContainerName;

        public Item(INamedTypeSymbol implementation, ITypeSymbol service, bool isInstance, string? containerName)
        {
            Implementation = implementation;
            Service = service;
            IsInstance = isInstance;
            ContainerName = containerName;
        }
    }
}