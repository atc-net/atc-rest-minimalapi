namespace Demo.Web.Models;

/// <summary>
/// Represents validation problem details from the API.
/// This is a simple DTO for deserializing RFC 7807 problem details responses.
/// </summary>
/// <remarks>
/// We use a custom class instead of HttpValidationProblemDetails because
/// the built-in class has a read-only Errors property that cannot be
/// populated by System.Text.Json during deserialization.
/// </remarks>
public sealed class ApiValidationProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("errors")]
    public IDictionary<string, string[]>? Errors { get; set; }
}
