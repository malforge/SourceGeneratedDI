# Mal.SourceGeneratedDI

Source-generated dependency injection with compile-time registrations and runtime composition.

**What is Dependency Injection?** Dependency injection is a design pattern where objects receive their dependencies from external sources rather than creating them. This makes code more testable, maintainable, and flexible. This library uses source generation to wire up your dependencies at compile-time, giving you type safety and performance without runtime reflection.

## Table of Contents

- [Quick Start](#quick-start)
- [Migrating from V1](#migrating-from-v1)
- [Understanding the Basics](#understanding-the-basics)
  - [Service Lifetimes](#service-lifetimes)
  - [Service Registration](#service-registration)
  - [Dependency Injection](#dependency-injection)
  - [The Container](#the-container)
- [Advanced Usage](#advanced-usage)
  - [Assembly-Level Registrations](#assembly-level-registrations)
  - [Lazy Dependencies](#lazy-dependencies)
  - [Cross-Assembly Composition](#cross-assembly-composition)
  - [Named Containers](#named-containers)
  - [Manual Registrations](#manual-registrations)
  - [Fallback Provider](#fallback-provider)
  - [Duplicate Registration Policy](#duplicate-registration-policy)
  - [Customizing Generated Class Names](#customizing-generated-class-names)
- [Analyzer Diagnostics](#analyzer-diagnostics)
- [License](#license)

## Quick Start

**Installation**
```bash
dotnet add package Mal.SourceGeneratedDI
dotnet add package Mal.SourceGeneratedDI.Abstractions
```

Both packages are required (.NET Standard 2.0+):
- `Mal.SourceGeneratedDI` - source generator and analyzers
- `Mal.SourceGeneratedDI.Abstractions` - runtime attributes, contracts, builder, and container

> **Note:** The source generator package cannot automatically include the Abstractions package as a transitive dependency, so both must be explicitly referenced.

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
// GeneratedRegistry is auto-generated in your assembly's root namespace
var container = new DependencyContainerBuilder()
    .AddRegistry<GeneratedRegistry>()
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
    .AddRegistry<GeneratedRegistry>()
    .Build();
    
var service = container.Resolve<MyService>();
```

**Key changes:**
- Use `DependencyContainerBuilder` with registries instead of direct container construction
- Manual factories: add via builder or generated registry partial hooks

## Understanding the Basics

### Service Lifetimes

**Singleton** - One instance is created and shared across all requests:
```csharp
[Singleton]
public class DatabaseConnection
{
    // This will be created once and reused everywhere
}
```

**Instance** (Transient) - A new instance is created every time:
```csharp
[Instance]
public class RequestHandler
{
    // A fresh instance is created for each Resolve<RequestHandler>() call
}
```

### Service Registration

Mark your classes with lifetime attributes to register them:

```csharp
// Register as the concrete type
[Singleton]
public class UserService { }

// Register as an interface
[Singleton<IUserService>]
public class UserService : IUserService { }
```

The source generator scans your assembly and creates registration code at compile-time.

### Dependency Injection

Dependencies are automatically injected through constructor parameters:

```csharp
[Singleton]
public class DatabaseService { }

[Instance]
public class UserRepository
{
    private readonly DatabaseService _db;
    
    // DatabaseService is automatically injected
    public UserRepository(DatabaseService db)
    {
        _db = db;
    }
}
```

### The Container

The source generator creates a `GeneratedRegistry` class in your assembly's root namespace. You compose one or more registries into a container, then resolve services:

```csharp
// MyApp.dll → MyApp.GeneratedRegistry
var container = new DependencyContainerBuilder()
    .AddRegistry<GeneratedRegistry>()
    .Build();

var userRepo = container.Resolve<UserRepository>();
```

## Advanced Usage

### Assembly-Level Registrations

When you can't or don't want to put attributes on the class itself, use assembly-level attributes:

```csharp
// In any .cs file in your project
[assembly: Singleton<ILogger, ConsoleLogger>]
[assembly: Instance<IHttpClient, HttpClientWrapper>]
```

This is useful for:
- Registering third-party types you don't control
- Mapping interfaces to implementations without modifying the implementation class
- Centralizing all registrations in one place

### Lazy Dependencies

Use `Lazy<T>` when you want to defer creation until needed, or to break circular dependencies:

**Breaking Circular Dependencies**
```csharp
[Singleton]
public class ServiceA
{
    private readonly Lazy<ServiceB> _serviceB;
    
    public ServiceA(Lazy<ServiceB> serviceB)
    {
        _serviceB = serviceB;
    }
    
    public void DoWork()
    {
        _serviceB.Value.Process();
    }
}

[Singleton]
public class ServiceB
{
    private readonly ServiceA _serviceA;
    
    // ServiceA → ServiceB → ServiceA would be circular,
    // but Lazy<ServiceB> breaks the cycle
    public ServiceB(ServiceA serviceA)
    {
        _serviceA = serviceA;
    }
}
```

**Deferring Expensive Operations**
```csharp
[Instance]
public class ExpensiveOperation
{
    private readonly Lazy<DatabaseService> _db;
    
    public ExpensiveOperation(Lazy<DatabaseService> db)
    {
        _db = db;
    }
    
    public void Execute()
    {
        // Database is only created if/when Value is accessed
        _db.Value.Query(...);
    }
}
```

### Cross-Assembly Composition

When your application uses multiple libraries that each contribute services, compose their registries together:

```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry<CoreLib.GeneratedRegistry>()      // From CoreLib.dll
    .AddRegistry<FeatureA.GeneratedRegistry>()     // From FeatureA.dll
    .AddRegistry<FeatureB.GeneratedRegistry>()     // From FeatureB.dll
    .AddRegistry<GeneratedRegistry>()              // From your app
    .Build();
```

Each assembly that references the source generator will have its own `GeneratedRegistry` in its own root namespace.

### Named Containers

When different parts of your application need isolated sets of services — for example, per-window services in a multi-window desktop app — use named containers.

**Marking services for a named container**
```csharp
// Goes into the default GeneratedRegistry
[Singleton]
public class AppSettingsService { }

// Goes into WindowGeneratedRegistry
[Singleton(Container = "Window")]
public class OverlayService { }

[Singleton<IOverlayService>(Container = "Window")]
public class OverlayService : IOverlayService { }
```

The `Container` name becomes part of the generated class name: `Container = "Window"` → `WindowGeneratedRegistry`.

**Composing named and default registries**

Each registry is independent. Because `Container = "Window"` explicitly partitions services, there should be no overlap between `WindowGeneratedRegistry` and `GeneratedRegistry` — so you can compose them directly without any duplicate policy:

```csharp
var windowContainer = new DependencyContainerBuilder()
    .AddRegistry<WindowGeneratedRegistry>()
    .AddRegistry<GeneratedRegistry>()
    .Build();
```

The default policy is `Throw`, which means any accidental duplicate registration will surface immediately as a build-time error rather than silently misbehaving at runtime. This is intentional: if you're partitioning services correctly, duplicates shouldn't exist. The error is a signal that your partitioning needs attention.

If you have a situation where duplicates are genuinely unavoidable — for example, composing third-party registries you don't control — you can opt out:

```csharp
var windowContainer = new DependencyContainerBuilder()
    .WithDuplicatePolicy(DuplicateRegistrationPolicy.FirstWins)
    .AddRegistry<WindowGeneratedRegistry>()  // window-specific services win
    .AddRegistry<GeneratedRegistry>()
    .Build();
```

Every window gets its own container instance with its own `OverlayService`, while all windows share the same `AppSettingsService`.

**Assembly-level attributes**
```csharp
[assembly: Singleton<IOverlayService, OverlayService>(Container = "Window")]
```

### Manual Registrations

Sometimes you need to register services at runtime:

**Option 1: Host-level (in your startup code)**
```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry<GeneratedRegistry>()
    .RegisterSingleton<IClock>(() => new UtcClock())
    .RegisterInstance<IConfig>(() => LoadConfigFromFile())
    .Build();
```

**Option 2: Assembly-level (via partial method)**
```csharp
// In your project, create a partial class matching your assembly's root namespace
namespace MyApp;

public sealed partial class GeneratedRegistry
{
    static partial void AddManualFactories(IServiceRegistry registry)
    {
        registry.AddSingleton(typeof(IClock), _ => new UtcClock());
        registry.AddInstance(typeof(IConfig), _ => LoadConfigFromFile());
    }
}
```

The generated registry class includes a partial hook that gets called during registration.

### Fallback Provider

Integrate with other DI systems (like ASP.NET Core's built-in DI):

```csharp
var container = new DependencyContainerBuilder()
    .AddRegistry<GeneratedRegistry>()
    .UseFallback(serviceProvider)  // IServiceProvider from another DI system
    .Build();
```

If a service isn't found in your generated registrations, it falls back to the provided `IServiceProvider`.

### Duplicate Registration Policy

When composing multiple registries, you might register the same service twice:

```csharp
var container = new DependencyContainerBuilder()
    .WithDuplicatePolicy(DuplicateRegistrationPolicy.LastWins)
    .AddRegistry<LibraryGeneratedRegistry>()  // Registers ILogger
    .AddRegistry<GeneratedRegistry>()         // Also registers ILogger
    .Build();
```

Options:
- `Throw` (default) - Throws an exception on duplicate
- `FirstWins` - Keeps the first registration
- `LastWins` - Overwrites with the last registration

### Customizing Generated Class Names

By default the generated class is named `GeneratedRegistry` in your assembly's root namespace. You can override this with `[assembly: DependencyContainerOptions(...)]`:

```csharp
[assembly: DependencyContainerOptions(
    Namespace = "MyApp.DI",   // Default: assembly root namespace
    Prefix    = "Hub"         // Default: no prefix
)]
```

With `Prefix = "Hub"`, the generated classes become `HubGeneratedRegistry`, `HubWindowGeneratedRegistry`, etc.
With `Namespace = "MyApp.DI"`, they are placed in that namespace instead.

## Analyzer Diagnostics

The following diagnostics are produced by the included Roslyn analyzer:

| ID    | Severity | Description |
|-------|----------|-------------|
| DI001 | Error    | A class has both `[Singleton]` and `[Instance]` attributes — pick one lifetime. |
| DI002 | Error    | The service type in `[Singleton<T>]` is not assignable from the implementation. |
| DI003 | Warning  | `[Singleton<MyClass>]` on `MyClass` is redundant — use plain `[Singleton]`. |
| DI004 | Error    | `Container = "..."` value is not a valid C# identifier. |
| DI005 | Error    | `Prefix = "..."` in `[DependencyContainerOptions]` is not a valid C# identifier. |
| DI006 | Error    | `Namespace = "..."` in `[DependencyContainerOptions]` is not a valid C# namespace. |

## License

MIT License

Copyright (c) 2026 The SourceGeneratedDI Authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

