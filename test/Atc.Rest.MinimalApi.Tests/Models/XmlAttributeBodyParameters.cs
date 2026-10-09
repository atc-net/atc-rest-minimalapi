namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record XmlAttributeBodyParameters(
    [property: Required] XmlAttributePet Request);