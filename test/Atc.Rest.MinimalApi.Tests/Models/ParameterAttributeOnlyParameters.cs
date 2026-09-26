namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A hand-written positional record with the binding attributes on the constructor parameters only
/// (no <c>property:</c> target), which [AsParameters] binding honors.
/// </summary>
public sealed record ParameterAttributeOnlyParameters(
    [FromQuery(Name = "page-size")] int PageSize,
    [FromBody] SetRequest Request);