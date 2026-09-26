namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record CollisionRequest(
    [property: Required] string? Id);