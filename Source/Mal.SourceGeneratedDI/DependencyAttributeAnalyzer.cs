using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Mal.SourceGeneratedDI;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DependencyAttributeAnalyzer : DiagnosticAnalyzer
{
    public const string MixedLifetimeDiagnosticId = "DI001";
    public const string InvalidServiceTypeDiagnosticId = "DI002";
    public const string RedundantServiceTypeDiagnosticId = "DI003";
    public const string InvalidContainerNameDiagnosticId = "DI004";
    public const string InvalidPrefixDiagnosticId = "DI005";
    public const string InvalidNamespaceDiagnosticId = "DI006";

    private static readonly DiagnosticDescriptor MixedLifetimeRule = new(
        MixedLifetimeDiagnosticId,
        "Mixed singleton and instance attributes",
        "Type '{0}' cannot have both [Singleton] and [Instance] attributes. Use one lifetime consistently.",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type must use either singleton or instance lifetime exclusively, not both.");

    private static readonly DiagnosticDescriptor InvalidServiceTypeRule = new(
        InvalidServiceTypeDiagnosticId,
        "Invalid service type for registration",
        "Type '{0}' cannot be registered as '{1}' because it is not assignable to that type",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Service types must be assignable from the implementation type.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    private static readonly DiagnosticDescriptor RedundantServiceTypeRule = new(
        RedundantServiceTypeDiagnosticId,
        "Redundant service type specification",
        "Specifying the same type '{0}' in the attribute is redundant. Use [{1}] instead of [{1}<{0}>].",
        "DependencyInjection",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When registering a type as itself, the generic type parameter is unnecessary.");

    private static readonly DiagnosticDescriptor InvalidContainerNameRule = new(
        InvalidContainerNameDiagnosticId,
        "Invalid container name",
        "Container name '{0}' is not a valid C# identifier. Container names must start with a letter or underscore and contain only letters, digits, and underscores.",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Container names are used as part of the generated registry class name and must be valid C# identifier segments.");

    private static readonly DiagnosticDescriptor InvalidPrefixRule = new(
        InvalidPrefixDiagnosticId,
        "Invalid registry prefix",
        "Prefix '{0}' is not a valid C# identifier. The prefix must start with a letter or underscore and contain only letters, digits, and underscores.",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Prefix in DependencyContainerOptions is prepended to generated registry class names and must be a valid C# identifier segment.");

    private static readonly DiagnosticDescriptor InvalidNamespaceRule = new(
        InvalidNamespaceDiagnosticId,
        "Invalid registry namespace",
        "Namespace '{0}' is not a valid C# namespace. Each segment must be a valid identifier.",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Namespace in DependencyContainerOptions must be a valid C# namespace.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(MixedLifetimeRule, InvalidServiceTypeRule, RedundantServiceTypeRule,
            InvalidContainerNameRule, InvalidPrefixRule, InvalidNamespaceRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterCompilationAction(AnalyzeAssembly);
    }

    private static bool IsValidIdentifier(string name)
        => !string.IsNullOrEmpty(name)
           && (char.IsLetter(name[0]) || name[0] == '_')
           && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static bool IsValidNamespace(string ns)
        => !string.IsNullOrEmpty(ns) && ns.Split('.').All(IsValidIdentifier);

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        
        if (namedType.TypeKind != TypeKind.Class)
            return;

        var attributes = namedType.GetAttributes();
        bool hasSingleton = false;
        bool hasInstance = false;

        foreach (var attr in attributes)
        {
            if (attr.AttributeClass is not { Name: "SingletonAttribute" or "Singleton" or "InstanceAttribute" or "Instance" })
                continue;

            var isSingleton = attr.AttributeClass.Name is "SingletonAttribute" or "Singleton";
            var isInstance = attr.AttributeClass.Name is "InstanceAttribute" or "Instance";

            if (isSingleton) hasSingleton = true;
            if (isInstance) hasInstance = true;

            // Validate Container name
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Container" && namedArg.Value.Value is string containerName)
                {
                    if (!IsValidIdentifier(containerName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidContainerNameRule,
                            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? namedType.Locations[0],
                            containerName));
                    }
                }
            }

            // Check for invalid service type (generic attributes only)
            if (attr.AttributeClass.Arity == 1)
            {
                var serviceType = attr.AttributeClass.TypeArguments[0];
                
                if (SymbolEqualityComparer.Default.Equals(serviceType, namedType))
                {
                    var attributeName = isSingleton ? "Singleton" : "Instance";
                    context.ReportDiagnostic(Diagnostic.Create(
                        RedundantServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? namedType.Locations[0],
                        namedType.Name,
                        attributeName));
                }
                else if (!namedType.AllInterfaces.Contains(serviceType, SymbolEqualityComparer.Default) &&
                    !IsBaseTypeOf(serviceType, namedType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? namedType.Locations[0],
                        namedType.Name,
                        serviceType.ToDisplayString()));
                }
            }
        }

        if (hasSingleton && hasInstance)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MixedLifetimeRule,
                namedType.Locations[0],
                namedType.Name));
        }
    }

    private static bool IsBaseTypeOf(ITypeSymbol potentialBase, INamedTypeSymbol derived)
    {
        var current = derived.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, potentialBase))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static void AnalyzeAssembly(CompilationAnalysisContext context)
    {
        var assemblyAttributes = context.Compilation.Assembly.GetAttributes();
        var registeredServices = new System.Collections.Generic.HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var attr in assemblyAttributes)
        {
            // Validate DependencyContainerOptions Prefix and Namespace
            if (attr.AttributeClass?.Name is "DependencyContainerOptionsAttribute" or "DependencyContainerOptions")
            {
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Prefix" && namedArg.Value.Value is string prefix && prefix.Length > 0)
                    {
                        if (!IsValidIdentifier(prefix))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                InvalidPrefixRule,
                                attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
                                prefix));
                        }
                    }
                    else if (namedArg.Key == "Namespace" && namedArg.Value.Value is string ns && ns.Length > 0)
                    {
                        if (!IsValidNamespace(ns))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                InvalidNamespaceRule,
                                attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
                                ns));
                        }
                    }
                }
            }

            if (attr.AttributeClass is not { Name: "SingletonAttribute" or "Singleton" or "InstanceAttribute" or "Instance" })
                continue;

            if (attr.AttributeClass.Arity != 2)
                continue;

            // Validate Container name on assembly-level attributes
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Container" && namedArg.Value.Value is string containerName)
                {
                    if (!IsValidIdentifier(containerName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidContainerNameRule,
                            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
                            containerName));
                    }
                }
            }

            var serviceType = attr.AttributeClass.TypeArguments[0];
            var implementationType = attr.AttributeClass.TypeArguments[1];

            if (implementationType is INamedTypeSymbol namedImpl)
            {
                if (!SymbolEqualityComparer.Default.Equals(serviceType, implementationType) &&
                    !namedImpl.AllInterfaces.Contains(serviceType, SymbolEqualityComparer.Default) &&
                    !IsBaseTypeOf(serviceType, namedImpl))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
                        implementationType.ToDisplayString(),
                        serviceType.ToDisplayString()));
                }
            }

            registeredServices.Add(serviceType);
        }
    }
}
