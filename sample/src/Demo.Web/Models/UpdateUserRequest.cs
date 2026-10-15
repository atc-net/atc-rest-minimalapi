namespace Demo.Web.Models;

public class UpdateUserRequest
{
    public GenderType? Gender { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    [JsonPropertyName("address")]
    public Address? WorkAddress { get; set; }

    /// <summary>
    /// The request to send: an empty field is left out (null), and so is an address or country with no values.
    /// This is a partial update, so a field that is left out keeps its current value.
    /// </summary>
    /// <returns>A copy with blank values set to null.</returns>
    public UpdateUserRequest ToRequestBody()
        => new()
        {
            Gender = Gender,
            FirstName = RequestBodyValues.NullIfBlank(FirstName),
            LastName = RequestBodyValues.NullIfBlank(LastName),
            Email = RequestBodyValues.NullIfBlank(Email),
            WorkAddress = RequestBodyValues.NullIfEmpty(WorkAddress),
        };
}