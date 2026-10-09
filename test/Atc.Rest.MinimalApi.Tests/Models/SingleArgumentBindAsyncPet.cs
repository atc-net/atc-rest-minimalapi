namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A body model with the one-argument <c>BindAsync(HttpContext)</c> overload of the Minimal APIs binding contract.
/// </summary>
public sealed record SingleArgumentBindAsyncPet(
    [property: MaxLength(10)] string? Name)
{
    public static ValueTask<SingleArgumentBindAsyncPet?> BindAsync(
        HttpContext context)
        => ValueTask.FromResult<SingleArgumentBindAsyncPet?>(null);
}