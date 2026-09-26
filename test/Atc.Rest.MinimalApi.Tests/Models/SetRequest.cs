namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A generator-style body model: PascalCase members without [JsonPropertyName] (the naming policy gives camelCase).
/// </summary>
public sealed record SetRequest(
    [property: MaxLength(5)] string? Reason,
    string? FirstName,
    IList<SetRequestLine> Lines);