# Mal.SourceGeneratedDI

Source-generated dependency injection with compile-time registrations and runtime composition.

## Quick Start

**Installation**
```bash
dotnet add package Mal.SourceGeneratedDI
```

**1. Declare services**
```csharp
using Mal.SourceGeneratedDI;

[Singleton]
public class DatabaseService { }

[Instance]
public class RequestHandler
{
    public RequestHandler(DatabaseService db) { }
}
```

**2. Build and resolve**
```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry(new MyAppGeneratedRegistry()) // auto-generated
    .Build();

var handler = container.Resolve<RequestHandler>();
```

## Migrating from V1

V1 used direct container construction. V2 uses registry-based composition for cross-assembly support.

**Before (v1)**
```csharp
var container = new DependencyContainer();
var service = container.Resolve<MyService>();
```

**After (v2)**
```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry(new MyAppGeneratedRegistry())
    .Build();
    
var service = container.Resolve<MyService>();
```

**Key changes:**
- Use `DependencyContainerBuilder` with registries instead of direct container construction
- Manual factories: add via builder or generated registry partial hooks

## Features

**Lifetime Attributes**
- `[Singleton]` - one instance per container
- `[Instance]` - new instance per resolution

**Advanced Features**
- Constructor injection with automatic dependency resolution
- `Lazy<T>` constructor dependencies
- Generic service mapping attributes
- Assembly-level registration attributes
- Circular dependency detection with dependency chain output
- Cross-assembly composition

## Configuration

**Cross-Assembly Composition**
```csharp
var builder = new DependencyContainerBuilder()
    .AddRegistry(new CoreLibGeneratedRegistry())
    .AddRegistry(new FeatureLibGeneratedRegistry());
var container = builder.Build();
```

**Manual Registrations (Host-Level)**
```csharp
var container = DependencyContainer.Create(builder =>
{
    builder.AddRegistry(new MyAppGeneratedRegistry());
    builder.Register<IClock>(() => new UtcClock());
});
```

**Manual Registrations (Assembly-Level)**
```csharp
namespace Mal.SourceGeneratedDI;

public sealed partial class MyAppGeneratedRegistry
{
    static partial void AddManualFactories(IServiceRegistry registry)
    {
        registry.AddSingleton(typeof(IClock), _ => new UtcClock());
    }
}
```

**Fallback Provider**
```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry(new MyAppGeneratedRegistry())
    .UseFallback(serviceProvider)
    .Build();
```

**Duplicate Registration Strategy**
- `Throw` (default)
- `FirstWins`
- `LastWins`

## Packages

- `Mal.SourceGeneratedDI` - source generator and analyzers
- `Mal.SourceGeneratedDI.Abstractions` - runtime attributes, contracts, builder, and container

## Requirements

.NET Standard 2.0+ target support

## License

MIT
