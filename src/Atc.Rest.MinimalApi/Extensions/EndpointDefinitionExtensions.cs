namespace Atc.Rest.MinimalApi.Extensions;

/// <summary>
/// Provides extension methods for registering and using endpoint definitions in a Minimal API application.
/// </summary>
public static class EndpointDefinitionExtensions
{
    /// <summary>
    /// Adds the endpoint definitions to the specified service collection by scanning the assemblies of the provided marker types.
    /// This method looks for types that implement the <see cref="IEndpointDefinition"/> interface and are neither abstract nor an interface,
    /// and adds them to the service collection as a single instance of <see cref="IReadOnlyCollection{IEndpointDefinition}"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the endpoint definitions to.</param>
    /// <param name="scanMarkers">The types used as markers to find the assemblies to scan for endpoint definitions.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="scanMarkers"/> is <c>null</c>.</exception>
    public static void AddEndpointDefinitions(
        this IServiceCollection services,
        params Type[] scanMarkers)
    {
        ArgumentNullException.ThrowIfNull(scanMarkers);

        var endpointDefinitions = new List<IEndpointDefinition>();

        foreach (var marker in scanMarkers)
        {
            endpointDefinitions.AddRange(
                marker.Assembly.ExportedTypes
                    .Where(x => typeof(IEndpointDefinition).IsAssignableFrom(x) &&
                                x is { IsInterface: false, IsAbstract: false })
                    .Select(TryCreateInstance)
                    .Where(x => x is not null)
                    .Cast<IEndpointDefinition>());
        }

        services.AddSingleton(endpointDefinitions as IReadOnlyCollection<IEndpointDefinition>);
    }

    /// <summary>
    /// Attempts to create an instance of the specified type, returning null if instantiation fails.
    /// </summary>
    private static object? TryCreateInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adds the endpoint definitions to the specified service collection by scanning the assemblies of the provided marker types.
    /// This method looks for types that implement the <see cref="IEndpointDefinition"/> and <see cref="IEndpointAndServiceDefinition"/>
    /// interface and are neither abstract nor an interface,
    /// and adds them to the service collection as a single instance of <see cref="IReadOnlyCollection{IEndpointDefinition}"/>
    /// and <see cref="IReadOnlyCollection{IEndpointAndServiceDefinition}"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the endpoint definitions to.</param>
    /// <param name="scanMarkers">The types used as markers to find the assemblies to scan for endpoint definitions.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="scanMarkers"/> is <c>null</c>.</exception>
    public static void AddEndpointAndServiceDefinitions(
        this IServiceCollection services,
        params Type[] scanMarkers)
    {
        ArgumentNullException.ThrowIfNull(scanMarkers);

        services.AddEndpointDefinitions(scanMarkers);

        var endpointDefinitions = new List<IEndpointAndServiceDefinition>();

        foreach (var marker in scanMarkers)
        {
            endpointDefinitions.AddRange(
                marker.Assembly.ExportedTypes
                    .Where(x => typeof(IEndpointAndServiceDefinition).IsAssignableFrom(x) &&
                                x is { IsInterface: false, IsAbstract: false })
                    .Select(TryCreateInstance)
                    .Where(x => x is not null)
                    .Cast<IEndpointAndServiceDefinition>());
        }

        foreach (var endpointDefinition in endpointDefinitions)
        {
            endpointDefinition.DefineServices(services);
        }
    }

    /// <summary>
    /// Applies the endpoint definitions to the specified web application.
    /// This method retrieves the registered endpoint definitions from the application's services and invokes
    /// their <see cref="IEndpointDefinition.DefineEndpoints"/> and/or <see cref="IEndpointAndServiceDefinition.DefineEndpoints"/> method.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to which the endpoint definitions are applied.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="app"/> is <c>null</c>.</exception>
    public static void UseEndpointDefinitions(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var definitions = app.Services.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        if (definitions is not null)
        {
            foreach (var endpointDefinition in definitions)
            {
                endpointDefinition.DefineEndpoints(app);
            }
        }

        var definitionWithDefineServices = app.Services.GetService<IReadOnlyCollection<IEndpointAndServiceDefinition>>();
        if (definitionWithDefineServices is not null)
        {
            foreach (var endpointDefinition in definitionWithDefineServices)
            {
                endpointDefinition.DefineEndpoints(app);
            }
        }
    }
}