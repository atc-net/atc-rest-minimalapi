namespace Demo.Api.Extensions;

internal static class EndpointRouteBuilderExtensions
{
    private static JsonSerializerOptions JsonSerializerOptions
        => JsonSerializerOptionsFactory.Create();

    public static void MapHealthChecks(this IEndpointRouteBuilder app)
    {
        app
            .MapHealthChecks(
                HealthCheckEndpoints.Aliveness,
                new HealthCheckOptions { Predicate = static r => r.Tags.Contains("live") });

        app
            .MapHealthChecks(
                HealthCheckEndpoints.Health,
                new HealthCheckOptions { Predicate = static h => h.Tags.Contains("dependency") });

        app
            .MapHealthChecks(
                HealthCheckEndpoints.Extended,
                HealthCheckOptionsFactory.CreateJson("Demo.Api", JsonSerializerOptions));
    }
}