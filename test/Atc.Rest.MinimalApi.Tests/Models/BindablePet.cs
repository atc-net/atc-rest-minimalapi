namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A body model that binds through <see cref="IBindableFromHttpContext{TSelf}"/>, implemented explicitly
/// (no public static <c>BindAsync</c> on the type).
/// </summary>
public sealed record BindablePet(
    [property: MaxLength(10)] string? Name) : IBindableFromHttpContext<BindablePet>
{
    static ValueTask<BindablePet?> IBindableFromHttpContext<BindablePet>.BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
        => ValueTask.FromResult<BindablePet?>(null);
}