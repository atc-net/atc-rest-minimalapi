namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A route parameter and a body property that both map to the wire key "id".
/// </summary>
public sealed record CollisionParameters(
    [property: FromRoute(Name = "id"), Range(1, int.MaxValue)] int Id,
    [property: FromBody] CollisionRequest Request);