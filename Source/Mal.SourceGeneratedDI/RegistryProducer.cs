using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Mal.SourceGeneratedDI;

public class RegistryProducer
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;
    
    readonly ImmutableArray<DependencyRegistryGenerator.Item> _items;
    readonly DependencyRegistryGenerator.ContainerOptions _options;
    readonly string _registryClassName;

    public RegistryProducer(
        ImmutableArray<DependencyRegistryGenerator.Item> items,
        DependencyRegistryGenerator.ContainerOptions options,
        string registryClassName)
    {
        _items = items;
        _options = options;
        _registryClassName = registryClassName;
    }
    
    const string RegistryTemplate =
        """
        #nullable enable
        
        using System;

        namespace Mal.SourceGeneratedDI;
        
        /// <summary>
        /// Auto-generated registrations for this assembly.
        /// </summary>
        {{VISIBILITY}} sealed partial class {{REGISTRY_CLASS}} : IRegistrationSource
        {{
            /// <inheritdoc />
            public void Contribute(IServiceRegistry registry)
            {{
                {{REGISTRATIONS}}
                AddManualFactories(registry);
            }}

            /// <summary>
            /// Partial method hook for adding manual factory registrations.
            /// Implement this method in a separate partial class to add custom registrations.
            /// </summary>
            /// <param name="registry">The service registry to add registrations to.</param>
            static partial void AddManualFactories(IServiceRegistry registry);
        }}
        """;

    const string SingletonItemTemplate = "registry.AddSingleton(typeof({0}), dr => new {1}({2}));";
    const string SingletonAliasTemplate = "registry.AddSingleton(typeof({0}), dr => dr.Resolve<{1}>());";
    const string InstanceItemTemplate = "registry.AddInstance(typeof({0}), dr => new {1}({2}));";
    const string ParameterTemplate = "dr.Resolve<{0}>()";
    const string LazyParameterTemplate = "new Lazy<{0}>(() => dr.Resolve<{0}>())";

    public string Produce()
    {
        var items = new List<string>();
        
        // Group items by implementation type to handle multiple registrations
        var grouped = _items.GroupBy(item => item.Implementation, SymbolEqualityComparer.Default);
        
        foreach (var group in grouped)
        {
            var itemList = group.ToList();
            var firstItem = itemList[0];
            
            // Get constructor for the implementation
            var ctor = firstItem.Implementation.Constructors.Length > 0
                ? firstItem.Implementation.Constructors.OrderByDescending(c => c.Parameters.Length).First()
                : null;
            
            var parameters = new List<string>();
            if (ctor is not null)
            {
                foreach (var p in ctor.Parameters)
                {
                    // If this is a Lazy, we need to resolve the inner type, and make the argument a new Lazy<>
                    if (p.Type is INamedTypeSymbol { IsGenericType: true, Name: "Lazy", TypeArguments.Length: 1 } lazyType)
                    {
                        parameters.Add(string.Format(LazyParameterTemplate, lazyType.TypeArguments[0].ToDisplayString(FullyQualifiedFormat)));
                        continue;
                    }
                    parameters.Add(string.Format(ParameterTemplate, p.Type.ToDisplayString(FullyQualifiedFormat)));
                }
            }
            
            if (firstItem.IsInstance)
            {
                // For instances, each registration gets its own factory (no sharing)
                foreach (var item in itemList)
                {
                    items.Add(string.Format(
                        InstanceItemTemplate,
                        item.Service.ToDisplayString(FullyQualifiedFormat),
                        item.Implementation.ToDisplayString(FullyQualifiedFormat),
                        string.Join(", ", parameters)));
                }
            }
            else
            {
                // For singletons with multiple registrations:
                // - First one becomes the "master" with the actual factory
                // - Others become "aliases" that resolve the master type
                
                // Add the master registration (first item or the one matching the implementation type)
                var masterItem = itemList.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.Service, i.Implementation));
                if (masterItem.Equals(default(DependencyRegistryGenerator.Item)))
                    masterItem = firstItem;
                    
                items.Add(string.Format(
                    SingletonItemTemplate,
                    masterItem.Service.ToDisplayString(FullyQualifiedFormat),
                    masterItem.Implementation.ToDisplayString(FullyQualifiedFormat),
                    string.Join(", ", parameters)));
                
                // Add alias registrations for the rest
                foreach (var item in itemList)
                {
                    if (SymbolEqualityComparer.Default.Equals(item.Service, masterItem.Service))
                        continue; // Skip the master
                    
                    // Alias: just resolve the master type
                    items.Add(string.Format(
                        SingletonAliasTemplate,
                        item.Service.ToDisplayString(FullyQualifiedFormat),
                        masterItem.Service.ToDisplayString(FullyQualifiedFormat)));
                }
            }
        }
        
        var visibility = _options.IsPublic ? "public" : "internal";
        
        var result = RegistryTemplate
            .Replace("{{REGISTRY_CLASS}}", _registryClassName)
            .Replace("{{VISIBILITY}}", visibility)
            .Replace("{{REGISTRATIONS}}", string.Join("\n                ", items))
            .Replace("{{", "{")
            .Replace("}}", "}");
            
        return result;
    }
}