namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>
/// The endpoints of the validation behaviour suite, each shaped like a generated endpoint: an <c>[AsParameters]</c>
/// record and the validation filter.
/// </summary>
public static class ValidationBehaviourEndpoints
{
    public static void AddValidators(IServiceCollection services)
    {
        services.AddSingleton<IValidator<FluentRequest>, FluentRequestValidator>();
        services.AddSingleton<IValidator<FluentChild>, FluentChildValidator>();
        services.AddSingleton<IValidator<NamedRequest>, NamedRequestValidator>();
        services.AddSingleton<IValidator<FluentParameters>, FluentParametersValidator>();
    }

    public static void Map(
        Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)
    {
        app.MapPost("/required", ([AsParameters] RequiredParameters parameters) => TypedResults.Ok())
            .Validated<RequiredParameters>();
        app.MapPost("/attributes", ([AsParameters] AttributesParameters parameters) => TypedResults.Ok())
            .Validated<AttributesParameters>();
        app.MapPost("/graph", ([AsParameters] GraphParameters parameters) => TypedResults.Ok())
            .Validated<GraphParameters>();
        app.MapPost("/array", ([AsParameters] ArrayParameters parameters) => TypedResults.Ok())
            .Validated<ArrayParameters>();
        app.MapPost("/validatable", ([AsParameters] ValidatableParameters parameters) => TypedResults.Ok())
            .Validated<ValidatableParameters>();
        app.MapPost("/type-level", ([AsParameters] TypeLevelParameters parameters) => TypedResults.Ok())
            .Validated<TypeLevelParameters>();
        app.MapPost("/named", ([AsParameters] NamedParameters parameters) => TypedResults.Ok())
            .Validated<NamedParameters>();
        app.MapPost("/fluent", ([AsParameters] FluentParameters parameters) => TypedResults.Ok())
            .Validated<FluentParameters>();
        app.MapPost("/init-only", ([AsParameters] InitOnlyParameters parameters) => TypedResults.Ok())
            .Validated<InitOnlyParameters>();
        app.MapPost("/cyclic", ([AsParameters] CyclicParameters parameters) => TypedResults.Ok())
            .Validated<CyclicParameters>();
        app.MapPost("/sources/{id}", ([AsParameters] SourcesParameters parameters) => TypedResults.Ok())
            .Validated<SourcesParameters>();
        app.MapPost("/list", ([AsParameters] ListBodyParameters parameters) => TypedResults.Ok())
            .Validated<ListBodyParameters>();
        app.MapPost("/bind-async", ([AsParameters] BindAsyncBodyParameters parameters) => TypedResults.Ok())
            .Validated<BindAsyncBodyParameters>();
    }

    /// <summary>
    /// The validation seam: where the suite attaches validation to an endpoint. Step 3 of ISSUE-15 swaps the engine
    /// here (or behind <see cref="ValidationFilter{T}"/>), together with <c>AddValidation()</c> in the fixture's services
    /// and a reference to the validation source generator; the tests stay unchanged.
    /// </summary>
    private static void Validated<TParameters>(this RouteHandlerBuilder builder)
        where TParameters : class
        => builder.AddEndpointFilter<ValidationFilter<TParameters>>();
}