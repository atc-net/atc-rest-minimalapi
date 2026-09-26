namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record SetRequestLine(
    [property: Required] string? Sku);