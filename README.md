# Mal.SourceGeneratedDI

Source-generated dependency injection with compile-time registrations and runtime composition.

## Version 2.0.0

Version 2 introduces a registry-first architecture for cross-assembly composition.

Breaking changes:

- `DependencyContainer` registrations are no longer emitted directly into each assembly.
- The generator now emits assembly-specific registration sources (`<AssemblyName>GeneratedRegistry`).
- Attributes and runtime contracts come from `Mal.SourceGeneratedDI.Abstractions`.
- Existing projects using `new DependencyContainer()` with only generated registrations must migrate to registry composition or generated factory usage.

## Breaking changes in v2

If you are upgrading from v1, the main behavior change is container composition.

Before (v1 style):

```csharp
var container = new DependencyContainer();
var service = container.Resolve<MyService>();
```

After (v2 style):

```csharp
var builder = new DependencyContainerBuilder();
builder.AddRegistry(new MyAppGeneratedRegistry());
var container = builder.Build();
var service = container.Resolve<MyService>();
```

Also note:

- Runtime attributes/contracts now come from `Mal.SourceGeneratedDI.Abstractions`.
- Generated registries replace assembly-local generated container registration bodies.
- Manual factories can be added in two places: host builder and generated registry partial hook.

## Packages

- `Mal.SourceGeneratedDI` - source generator and analyzers
- `Mal.SourceGeneratedDI.Abstractions` - runtime attributes, contracts, builder, and container

For most apps and DI-contributing libraries, reference both.

## Core model

Each assembly contributes generated registrations through `IRegistrationSource`:

- `<AssemblyName>GeneratedRegistry` (generated)
- `void Contribute(IServiceRegistry registry)` (generated)
- `static partial void AddManualFactories(IServiceRegistry registry)` (generated partial hook)

The final runtime container is built from one or more registration sources via `DependencyContainerBuilder`.

## Installation

```bash
dotnet add package Mal.SourceGeneratedDI
dotnet add package Mal.SourceGeneratedDI.Abstractions
```

## Quick start

### 1) Declare services

```csharp
using Mal.SourceGeneratedDI;

[Singleton]
public class DatabaseService
{
}

[Instance]
public class RequestHandler
{
    private readonly DatabaseService _db;

    public RequestHandler(DatabaseService db)
    {
        _db = db;
    }
}
```

### 2) Compose registries and build container

```csharp
using Mal.SourceGeneratedDI;

var builder = new DependencyContainerBuilder();
builder.AddRegistry(new MyAppGeneratedRegistry()); // generated for your assembly
var container = builder.Build();

var handler = container.Resolve<RequestHandler>();
```

### 3) Cross-assembly composition

```csharp
var builder = new DependencyContainerBuilder()
    .AddRegistry(new CoreLibGeneratedRegistry())
    .AddRegistry(new FeatureLibGeneratedRegistry());

var container = builder.Build();
```

## Manual factory registrations

Version 2 supports both manual paths.

### Host-level manual registrations

```csharp
var container = DependencyContainer.Create(builder =>
{
    builder.AddRegistry(new MyAppGeneratedRegistry());
    builder.Register<IClock>(() => new UtcClock());
});
```

### Assembly-level advanced factories via partial hook

For the generated registry class (example assembly name `MyApp`):

```csharp
using Mal.SourceGeneratedDI;

namespace Mal.SourceGeneratedDI;

public sealed partial class MyAppGeneratedRegistry
{
    static partial void AddManualFactories(IServiceRegistry registry)
    {
        registry.AddSingleton(typeof(IClock), _ => new UtcClock());
    }
}
```

## Optional fallback provider

You can configure a fallback `IServiceProvider` for unresolved services:

```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry(new MyAppGeneratedRegistry())
    .UseFallback(serviceProvider)
    .Build();
```

Resolution order is generated/container registrations first, fallback second.

## Duplicate registrations

`DependencyContainerBuilder` supports:

- `Throw` (default)
- `FirstWins`
- `LastWins`

## Existing features retained

- Class-level `[Singleton]` / `[Instance]`
- Generic service mapping attributes
- Assembly-level registration attributes
- Constructor injection
- `Lazy<T>` constructor dependencies
- Circular dependency detection with dependency chain output

## Migration from v1

1. Add `Mal.SourceGeneratedDI.Abstractions` to consuming projects.
2. Replace direct usage assumptions of generated `DependencyContainer` registrations.
3. Build containers via `DependencyContainerBuilder` + generated registries (or generated factory helper).
4. Move custom factory wiring to host-level registration and/or generated registry partial hooks.

## Requirements

- .NET Standard 2.0+ target support
- C# 11 in this repository

## License

MIT
