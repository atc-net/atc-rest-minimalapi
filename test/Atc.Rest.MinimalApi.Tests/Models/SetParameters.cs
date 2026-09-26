namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a parameters record from atc-rest-api-source-generator: binding names from the OpenAPI spec,
/// and the JSON body as the <c>Request</c> member.
/// </summary>
public sealed record SetParameters(
    [property: FromRoute(Name = "mode"), Required] string Mode,
    [property: FromQuery(Name = "page-size"), Range(1, 100)] int PageSize,
    [property: FromBody, Required] SetRequest Request);