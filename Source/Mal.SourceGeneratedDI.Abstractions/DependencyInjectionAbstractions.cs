using System;
using System.Collections.Generic;
using System.Linq;

namespace Mal.SourceGeneratedDI;

/// <summary>
/// Marks a class as a singleton - one shared instance will be created and cached.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SingletonAttribute : Attribute
{
}

/// <summary>
/// Marks a class as a singleton and registers it as the specified service type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SingletonAttribute<TService> : Attribute where TService : class
{
}

/// <summary>
/// Registers a singleton mapping at assembly level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
public sealed class SingletonAttribute<TService, TImplementation> : Attribute
    where TService : class
    where TImplementation : class, TService
{
}

/// <summary>
/// Marks a class to be created as a new instance on each resolve.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class InstanceAttribute : Attribute
{
}

/// <summary>
/// Marks a class to be created as a new instance on each resolve and registers it as the specified service type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class InstanceAttribute<TService> : Attribute where TService : class
{
}

/// <summary>
/// Registers an instance-per-resolve mapping at assembly level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
public sealed class InstanceAttribute<TService, TImplementation> : Attribute
    where TService : class
    where TImplementation : class, TService
{
}

/// <summary>
/// Configures generated container options.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public sealed class DependencyContainerOptionsAttribute : Attribute
{
    public ContainerVisibility Visibility { get; set; } = ContainerVisibility.Public;
    public bool EnableBuilder { get; set; }
}

/// <summary>
/// Visibility options for generated container types.
/// </summary>
public enum ContainerVisibility
{
    Public = 0,
    Internal = 1
}

/// <summary>
/// Duplicate registration handling policy.
/// </summary>
public enum DuplicateRegistrationPolicy
{
    Throw = 0,
    FirstWins = 1,
    LastWins = 2
}

/// <summary>
/// Represents a simple dependency injection container that can resolve services.
/// </summary>
public interface IDependencyContainer
{
    T Resolve<T>() where T : class;
    bool TryResolve<T>(out T? instance) where T : class;
    object Resolve(Type serviceType);
    bool TryResolve(Type serviceType, out object? instance);
}

/// <summary>
/// A registration target used by generated registries and manual wiring.
/// </summary>
public interface IServiceRegistry
{
    void AddSingleton(Type serviceType, Func<IDependencyContainer, object> factory);
    void AddInstance(Type serviceType, Func<IDependencyContainer, object> factory);
}

/// <summary>
/// A source that contributes registrations to an <see cref="IServiceRegistry"/>.
/// </summary>
public interface IRegistrationSource
{
    void Contribute(IServiceRegistry registry);
}

internal sealed class Registration
{
    public Registration(Func<IDependencyContainer, object> factory, bool isInstance)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        IsInstance = isInstance;
    }

    public Func<IDependencyContainer, object> Factory { get; }
    public bool IsInstance { get; }
}

/// <summary>
/// Mutable builder that merges generated registries and host-provided factories.
/// </summary>
public sealed class DependencyContainerBuilder : IServiceRegistry
{
    private readonly Dictionary<Type, Registration> _registrations = new();
    private IServiceProvider? _fallbackProvider;
    private DuplicateRegistrationPolicy _duplicateRegistrationPolicy = DuplicateRegistrationPolicy.Throw;

    public DependencyContainerBuilder AddRegistry(IRegistrationSource source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        source.Contribute(this);
        return this;
    }

    public DependencyContainerBuilder AddRegistry<T>() where T : class, IRegistrationSource, new()
    {
        return AddRegistry(new T());
    }

    public DependencyContainerBuilder WithDuplicatePolicy(DuplicateRegistrationPolicy policy)
    {
        _duplicateRegistrationPolicy = policy;
        return this;
    }

    public DependencyContainerBuilder UseFallback(IServiceProvider fallbackProvider)
    {
        _fallbackProvider = fallbackProvider ?? throw new ArgumentNullException(nameof(fallbackProvider));
        return this;
    }

    public DependencyContainerBuilder Register<TService>(Func<TService> factory) where TService : class
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        AddSingleton(typeof(TService), _ => factory());
        return this;
    }

    public DependencyContainerBuilder Register<TService>(Func<IDependencyContainer, TService> factory) where TService : class
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        AddSingleton(typeof(TService), factory);
        return this;
    }

    public void AddSingleton(Type serviceType, Func<IDependencyContainer, object> factory)
        => Add(serviceType, new Registration(factory, isInstance: false));

    public void AddInstance(Type serviceType, Func<IDependencyContainer, object> factory)
        => Add(serviceType, new Registration(factory, isInstance: true));

    public DependencyContainer Build()
        => new(_registrations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), _fallbackProvider);

    private void Add(Type serviceType, Registration registration)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));
        if (registration == null)
            throw new ArgumentNullException(nameof(registration));

        if (!_registrations.ContainsKey(serviceType))
        {
            _registrations.Add(serviceType, registration);
            return;
        }

        switch (_duplicateRegistrationPolicy)
        {
            case DuplicateRegistrationPolicy.Throw:
                throw new InvalidOperationException($"Service of type {serviceType} is already registered.");
            case DuplicateRegistrationPolicy.FirstWins:
                return;
            case DuplicateRegistrationPolicy.LastWins:
                _registrations[serviceType] = registration;
                return;
            default:
                throw new InvalidOperationException($"Unknown duplicate policy: {_duplicateRegistrationPolicy}.");
        }
    }
}

/// <summary>
/// Runtime container built from one or more registration sources.
/// </summary>
public sealed class DependencyContainer : IDependencyContainer, IServiceProvider
{
    private readonly Dictionary<Type, Registration> _registrations;
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly IServiceProvider? _fallbackProvider;
    private readonly Stack<Type> _resolvingStack = new();
    private readonly HashSet<Type> _resolvingSet = new();

    internal DependencyContainer(Dictionary<Type, Registration> registrations, IServiceProvider? fallbackProvider)
    {
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _fallbackProvider = fallbackProvider;
        _singletons[typeof(DependencyContainer)] = this;
        _singletons[typeof(IDependencyContainer)] = this;
        _singletons[typeof(IServiceProvider)] = this;
    }
    
    public DependencyContainer()
        : this(new Dictionary<Type, Registration>(), null)
    {
    }
    
    public DependencyContainer(Func<DependencyContainerBuilder, DependencyContainerBuilder> configure)
        : this(BuildRegistrations(configure), null)
    {
    }
    
    private static Dictionary<Type, Registration> BuildRegistrations(Func<DependencyContainerBuilder, DependencyContainerBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var builder = new DependencyContainerBuilder();
        builder = configure(builder);
        var container = builder.Build();
        return container._registrations;
    }

    public object? GetService(Type serviceType)
        => TryResolve(serviceType, out var instance) ? instance : null;

    public T Resolve<T>() where T : class
        => (T)Resolve(typeof(T));

    public bool TryResolve<T>(out T? instance) where T : class
    {
        if (TryResolve(typeof(T), out var value))
        {
            instance = (T)value!;
            return true;
        }

        instance = null;
        return false;
    }

    public object Resolve(Type serviceType)
    {
        if (TryResolve(serviceType, out var instance))
            return instance!;

        throw new InvalidOperationException($"Service of type {serviceType} is not registered.");
    }

    public bool TryResolve(Type serviceType, out object? instance)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));

        if (_singletons.TryGetValue(serviceType, out instance))
            return true;

        if (!_registrations.TryGetValue(serviceType, out var registration))
        {
            var fallback = _fallbackProvider?.GetService(serviceType);
            if (fallback != null)
            {
                instance = fallback;
                return true;
            }

            instance = null;
            return false;
        }

        if (!_resolvingSet.Add(serviceType))
        {
            var chain = string.Join(" -> ", _resolvingStack.Reverse().Select(t => t.Name)) + " -> " + serviceType.Name;
            throw new InvalidOperationException($"Circular dependency detected: {chain}");
        }

        _resolvingStack.Push(serviceType);
        try
        {
            instance = registration.Factory(this);
        }
        finally
        {
            _resolvingStack.Pop();
            _resolvingSet.Remove(serviceType);
        }

        if (!registration.IsInstance)
            _singletons[serviceType] = instance;

        return true;
    }
}
