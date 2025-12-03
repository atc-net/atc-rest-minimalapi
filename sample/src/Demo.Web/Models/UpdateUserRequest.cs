namespace Demo.Web.Models;

public class UpdateUserRequest
{
    public GenderType Gender { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public Address? WorkAddress { get; set; }
}