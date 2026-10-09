namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record BindableBodyParameters(
    [property: Required] BindablePet Request);