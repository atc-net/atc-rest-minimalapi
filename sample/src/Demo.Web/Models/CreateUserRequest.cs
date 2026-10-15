namespace Demo.Web.Models;

public class CreateUserRequest
{
    public GenderType? Gender { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? Telephone { get; set; }

    public string? HomePage { get; set; }

    public Address? HomeAddress { get; set; }

    public Address? WorkAddress { get; set; }

    /// <summary>
    /// The request to send: an empty field is left out (null), and so is an address or country with no values,
    /// so the API validates exactly what the user filled in.
    /// </summary>
    /// <returns>A copy with blank values set to null.</returns>
    public CreateUserRequest ToRequestBody()
        => new()
        {
            Gender = Gender,
            FirstName = RequestBodyValues.NullIfBlank(FirstName),
            LastName = RequestBodyValues.NullIfBlank(LastName),
            Email = RequestBodyValues.NullIfBlank(Email),
            Telephone = RequestBodyValues.NullIfBlank(Telephone),
            HomePage = RequestBodyValues.NullIfBlank(HomePage),
            HomeAddress = RequestBodyValues.NullIfEmpty(HomeAddress),
            WorkAddress = RequestBodyValues.NullIfEmpty(WorkAddress),
        };
}