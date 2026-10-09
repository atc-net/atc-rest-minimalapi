namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record SingleArgumentBindAsyncParameters(
    [property: Required] SingleArgumentBindAsyncPet Request);