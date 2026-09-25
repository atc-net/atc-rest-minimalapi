namespace Atc.Rest.MinimalApi.Tests.Models;

public class PersonRequest
{
    [Required]
    public string? Name { get; set; }
}