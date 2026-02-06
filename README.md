# Mal.SourceGeneratedDI

A C# source generator that provides compile-time dependency injection with minimal runtime overhead.

## Features

- **Compile-time code generation** - No reflection, no runtime scanning
- **Minimal runtime overhead** - Direct constructor calls, efficient singleton caching
- **Type-safe** - All registrations validated at compile time
- **Flexible registration** - Class-level attributes, assembly-level attributes, and manual factories
- **Circular dependency detection** - Clear error messages with full dependency chain
- **Lazy dependencies** - Built-in support for `Lazy<T>` parameters

## Installation

```bash
dotnet add package Mal.SourceGeneratedDI
```

## Quick Start

### 1. Basic Usage with Attributes

Mark your services with `[Singleton]` or `[Instance]`:

```csharp
using Mal.SourceGeneratedDI;

[Singleton]
public class DatabaseService
{
    public void Connect() { }
}

[Instance] // New instance per resolve
public class RequestHandler
{
    private readonly DatabaseService _db;
    
    public RequestHandler(DatabaseService db)
    {
        _db = db;
    }
}

// Use the generated container
var container = new DependencyContainer();
var handler = container.Resolve<RequestHandler>();
```

### 2. Interface Registration

Register implementations under service interfaces:

```csharp
public interface IEmailService { }

[Singleton<IEmailService>]
public class EmailService : IEmailService
{
    public void SendEmail(string to, string subject) { }
}

var container = new DependencyContainer();
var emailService = container.Resolve<IEmailService>();
```

### 3. Assembly-Level Registration

Register types without modifying their source code (useful for third-party libraries):

```csharp
using Mal.SourceGeneratedDI;

[assembly: Singleton<ILogger, FileLogger>]
[assembly: Singleton<ICache, MemoryCache>]

namespace MyApp
{
    // Types don't need attributes
    public class FileLogger : ILogger { }
    public class MemoryCache : ICache { }
}
```

### 4. Manual Factory Registration

For complex initialization scenarios, enable the builder API:

```csharp
[assembly: DependencyContainerOptions(EnableBuilder = true)]

using Mal.SourceGeneratedDI;

var container = new DependencyContainer(builder => builder
    .Register<IPaymentGateway>(() => new StripeGateway(apiKey: "..."))
    .Register<IComplexService>(c => new ComplexService(
        c.Resolve<IDependency1>(),
        c.Resolve<IDependency2>(),
        someSpecialConfig)));

var payment = container.Resolve<IPaymentGateway>();
```

## Configuration

### Container Options

Configure the generated container using the assembly-level attribute:

```csharp
[assembly: DependencyContainerOptions(
    Visibility = ContainerVisibility.Internal,  // Default: Public
    EnableBuilder = true)]                       // Default: false
```

**Options:**

- `Visibility`: Controls whether the generated `DependencyContainer` class is `public` or `internal`
  - **Default: `Public`**
  - Use `Internal` if the container should only be used within the assembly

- `EnableBuilder`: Enables the immutable builder API for manual factory registration
  - **Default: `false`** (builder code is not generated)
  - Set to `true` to enable the `.Register<T>()` fluent API
  - The builder parameter is still optional even when enabled

## Advanced Features

### Lazy Dependencies

Inject `Lazy<T>` to defer service creation:

```csharp
[Singleton]
public class ExpensiveService
{
    public ExpensiveService()
    {
        // Heavy initialization
    }
}

[Singleton]
public class MyService
{
    private readonly Lazy<ExpensiveService> _expensive;
    
    public MyService(Lazy<ExpensiveService> expensive)
    {
        _expensive = expensive;
    }
    
    public void DoWork()
    {
        if (needsExpensiveWork)
        {
            _expensive.Value.DoExpensiveWork(); // Created only when accessed
        }
    }
}
```

### Multiple Service Registrations

Register the same implementation under multiple service types:

```csharp
public interface IReadRepository { }
public interface IWriteRepository { }

[Singleton<IReadRepository>]
[Singleton<IWriteRepository>]
public class DatabaseRepository : IReadRepository, IWriteRepository
{
    // Same instance shared for both interfaces
}
```

### TryResolve Pattern

Safely attempt to resolve optional dependencies:

```csharp
var container = new DependencyContainer();

if (container.TryResolve<IOptionalFeature>(out var feature))
{
    feature.Execute();
}
```

## Lifecycle Management

### Singleton

- One instance created and cached for the lifetime of the container
- Thread-safe lazy initialization
- Instance shared across all resolves

```csharp
[Singleton]
public class ConfigurationService { }
```

### Instance (Per-Resolve)

- New instance created every time the service is resolved
- Not cached
- Useful for stateful or short-lived services

```csharp
[Instance]
public class HttpRequestContext { }
```

## Error Handling

### Circular Dependency Detection

The container detects circular dependencies at runtime with clear error messages:

```csharp
[Singleton]
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

[Singleton]
public class ServiceB
{
    public ServiceB(ServiceA a) { } // Circular!
}

// Throws: InvalidOperationException
// "Circular dependency detected: ServiceA -> ServiceB -> ServiceA"
```

### Missing Registration

```csharp
var container = new DependencyContainer();
var missing = container.Resolve<UnregisteredService>();
// Throws: InvalidOperationException
// "Service of type UnregisteredService is not registered."
```

## Design Philosophy

**Compile-time over runtime**: This library uses Roslyn source generators to produce all dependency registration code at compile time. There's no assembly scanning, no reflection, and no runtime overhead beyond simple dictionary lookups and constructor calls.

**Minimal and focused**: Unlike heavyweight DI frameworks, this library focuses on the essential 90% use case. No property injection, no method injection, no lifecycle management beyond singleton/instance, no interceptors.

**Explicit over magic**: All registrations are visible in your code, either as attributes or manual registrations. No convention-based scanning, no auto-discovery.

## Comparison

**vs. Microsoft.Extensions.DependencyInjection**
- ✅ Faster (no reflection)
- ✅ Smaller binary footprint
- ❌ Fewer features (no scopes, property injection, etc.)

**vs. Manual DI**
- ✅ Less boilerplate
- ✅ Automatic constructor resolution
- ❌ Slightly less control

## Requirements

- .NET Standard 2.0+ (generator)
- C# 8.0+ (nullable reference types)
- Supports .NET Framework 4.7.2+, .NET Core 3.1+, .NET 5+

## Contributing

Contributions are welcome! When submitting changes, please:

1. **Discuss major changes first** - For significant features or breaking changes, please open an issue to discuss with maintainers before investing time
2. **Update version numbers** - Increment the version in the `.csproj` file according to semantic versioning
3. **Update release notes** - Add an entry to the release notes documenting your changes
4. Follow existing code style and patterns
5. Include tests for new functionality
6. Ensure all tests pass before submitting

## License

This project is licensed under the MIT License.

```
MIT License

Copyright (c) 2026

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
```
