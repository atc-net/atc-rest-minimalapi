namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed class EmployeeRequest : PersonRequest
{
    public string? Department { get; set; }
}