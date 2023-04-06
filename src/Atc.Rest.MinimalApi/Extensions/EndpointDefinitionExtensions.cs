namespace Atc.Rest.MinimalApi.Extensions;

public static class EndpointDefinitionExtensions
{
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
                    .Select(Activator.CreateInstance).Cast<IEndpointDefinition>());
        }

        services.AddSingleton(endpointDefinitions as IReadOnlyCollection<IEndpointDefinition>);
    }

    public static void UseEndpointDefinitions(
        this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var definitions = app.Services.GetRequiredService<IReadOnlyCollection<IEndpointDefinition>>();

        foreach (var endpointDefinition in definitions)
        {
            endpointDefinition.DefineEndpoints(app);
        }
    }
}