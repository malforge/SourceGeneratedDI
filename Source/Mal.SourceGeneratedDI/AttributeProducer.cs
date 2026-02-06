namespace Mal.SourceGeneratedDI
{
    public class FrameworkProducer
    {
        public static readonly FrameworkProducer Instance = new();
        
        const string AttributeTemplate = 
            """
            #nullable enable
            
            using System;
            using System.Diagnostics.CodeAnalysis;
            
            namespace Mal.SourceGeneratedDI;
            
            /// <summary>
            /// Marks a class as a singleton - one shared instance will be created and cached.
            /// </summary>
            /// <typeparam name="TService">The service type to register the class as.</typeparam>
            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            sealed class SingletonAttribute<TService> : Attribute where TService: class;

            /// <summary>
            /// Marks a class as a singleton - one shared instance will be created and cached.
            /// The class will be registered as its own type.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            sealed class SingletonAttribute : Attribute;
            
            /// <summary>
            /// Registers a type as a singleton at assembly level - one shared instance will be created and cached.
            /// </summary>
            /// <typeparam name="TService">The service type to register.</typeparam>
            /// <typeparam name="TImplementation">The implementation type.</typeparam>
            [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
            sealed class SingletonAttribute<TService, TImplementation> : Attribute 
                where TService: class 
                where TImplementation: class, TService;
            
            /// <summary>
            /// Marks a class to be created as a new instance on each resolve.
            /// </summary>
            /// <typeparam name="TService">The service type to register the class as.</typeparam>
            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            sealed class InstanceAttribute<TService> : Attribute where TService: class;

            /// <summary>
            /// Marks a class to be created as a new instance on each resolve.
            /// The class will be registered as its own type.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            sealed class InstanceAttribute : Attribute;
            
            /// <summary>
            /// Registers a type to be created as a new instance on each resolve at assembly level.
            /// </summary>
            /// <typeparam name="TService">The service type to register.</typeparam>
            /// <typeparam name="TImplementation">The implementation type.</typeparam>
            [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
            sealed class InstanceAttribute<TService, TImplementation> : Attribute 
                where TService: class 
                where TImplementation: class, TService;
            
            /// <summary>
            /// Configures options for the generated DependencyContainer.
            /// </summary>
            [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
            sealed class DependencyContainerOptionsAttribute : Attribute
            {
                /// <summary>
                /// Gets or sets the visibility of the generated DependencyContainer class.
                /// Default is <see cref="ContainerVisibility.Public"/>.
                /// </summary>
                public ContainerVisibility Visibility { get; set; } = ContainerVisibility.Public;
                
                /// <summary>
                /// Gets or sets whether to enable the immutable builder API for manual factory registration.
                /// Default is false.
                /// </summary>
                public bool EnableBuilder { get; set; } = false;
            }
            
            /// <summary>
            /// Specifies the visibility of the generated DependencyContainer class.
            /// </summary>
            enum ContainerVisibility
            {
                /// <summary>
                /// The DependencyContainer class will be public.
                /// </summary>
                Public = 0,
                
                /// <summary>
                /// The DependencyContainer class will be internal.
                /// </summary>
                Internal = 1
            }
            
            /// <summary>
            /// Represents a simple dependency injection container that can resolve registered services.
            /// </summary>
            public interface IDependencyContainer
            {
                /// <summary>
                /// Resolves an instance of the specified service type.
                /// </summary>
                /// <typeparam name="T">The type of the service to resolve.</typeparam>
                /// <returns>The resolved service instance.</returns>
                /// <exception cref="InvalidOperationException">Thrown if the service type is not registered.</exception>
                T Resolve<T>() where T: class;
                
                /// <summary>
                /// Tries to resolve an instance of the specified service type.
                /// </summary>
                /// <param name="instance">The resolved instance, or null if the service is not registered.</param>
                /// <returns>True if the service was resolved; otherwise, false.</returns>
                bool TryResolve<T>([MaybeNullWhen(false)] out T instance) where T: class;
                
                /// <summary>
                /// Resolves an instance of the specified service type.
                /// </summary>
                /// <param name="serviceType">The type of the service to resolve.</param>
                /// <returns>The resolved service instance.</returns>
                /// <exception cref="InvalidOperationException">Thrown if the service type is not registered.</exception>
                object Resolve(Type serviceType);
                
                /// <summary>
                /// Tries to resolve an instance of the specified service type.
                /// </summary>
                /// <param name="serviceType">The type of the service to resolve.</param>
                /// <param name="instance">The resolved instance, or null if the service is not registered.</param>
                /// <returns>True if the service was resolved; otherwise, false.</returns>
                bool TryResolve(Type serviceType, [MaybeNullWhen(false)] out object instance);
            }
            """;
        
        public string Produce()
        {
            return AttributeTemplate;
        }
    }
}