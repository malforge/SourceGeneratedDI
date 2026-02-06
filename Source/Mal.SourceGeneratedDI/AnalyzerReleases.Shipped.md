; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0-beta.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DI001 | DependencyInjection | Error | Mixed singleton and instance attributes
DI002 | DependencyInjection | Error | Invalid service type for registration
DI003 | DependencyInjection | Warning | Redundant service type specification
