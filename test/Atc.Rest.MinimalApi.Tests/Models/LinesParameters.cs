namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a parameters record from atc-rest-api-source-generator for an array body: the JSON body is a
/// collection, bound to the <c>Request</c> member.
/// </summary>
public sealed record LinesParameters(
    [property: FromBody, Required] IList<SetRequestLine> Request);