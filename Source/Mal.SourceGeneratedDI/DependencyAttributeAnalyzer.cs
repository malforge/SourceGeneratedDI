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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(MixedLifetimeRule, InvalidServiceTypeRule, RedundantServiceTypeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterCompilationAction(AnalyzeAssembly);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        
        // Only analyze classes
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

            if (isSingleton)
                hasSingleton = true;
            if (isInstance)
                hasInstance = true;

            // Check for invalid service type (generic attributes only)
            if (attr.AttributeClass.Arity == 1)
            {
                var serviceType = attr.AttributeClass.TypeArguments[0];
                
                // Check for redundant self-registration (e.g., MyClass has [Singleton<MyClass>])
                if (SymbolEqualityComparer.Default.Equals(serviceType, namedType))
                {
                    var attributeName = isSingleton ? "Singleton" : "Instance";
                    var diagnostic = Diagnostic.Create(
                        RedundantServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? namedType.Locations[0],
                        namedType.Name,
                        attributeName);
                    context.ReportDiagnostic(diagnostic);
                }
                // Check if implementation is assignable to service type
                // An implementation can be used as the service type if there's an implicit conversion
                else if (!namedType.AllInterfaces.Contains(serviceType, SymbolEqualityComparer.Default) &&
                    !IsBaseTypeOf(serviceType, namedType))
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? namedType.Locations[0],
                        namedType.Name,
                        serviceType.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Check for mixed lifetime attributes
        if (hasSingleton && hasInstance)
        {
            var diagnostic = Diagnostic.Create(
                MixedLifetimeRule,
                namedType.Locations[0],
                namedType.Name);
            context.ReportDiagnostic(diagnostic);
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
            if (attr.AttributeClass is not { Name: "SingletonAttribute" or "Singleton" or "InstanceAttribute" or "Instance" })
                continue;

            // Assembly-level attributes always have arity 2 (enforced by AttributeTargets)
            if (attr.AttributeClass.Arity != 2)
                continue;

            var serviceType = attr.AttributeClass.TypeArguments[0];
            var implementationType = attr.AttributeClass.TypeArguments[1];

            // Check if implementation is assignable to service type
            // The constraint "where TImplementation : TService" enforces this at compile time,
            // but we validate it here for better error messages
            if (implementationType is INamedTypeSymbol namedImpl)
            {
                if (!SymbolEqualityComparer.Default.Equals(serviceType, implementationType) &&
                    !namedImpl.AllInterfaces.Contains(serviceType, SymbolEqualityComparer.Default) &&
                    !IsBaseTypeOf(serviceType, namedImpl))
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidServiceTypeRule,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
                        implementationType.ToDisplayString(),
                        serviceType.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Track duplicate service registrations
            if (!registeredServices.Add(serviceType))
            {
                // Note: This is informational only - generator handles last-wins
                // Could add a diagnostic here if desired
            }
        }
    }
}
