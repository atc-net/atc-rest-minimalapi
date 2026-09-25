namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A parameters class shaped like the generator's [AsParameters] records: a required query parameter
/// and an optional [FromBody] member.
/// </summary>
public sealed class OptionalBodyParameters
{
    [FromQuery(Name = "name")]
    [Required]
    public string? Name { get; set; }

    [FromBody]
    public PersonRequest? Request { get; set; }
}