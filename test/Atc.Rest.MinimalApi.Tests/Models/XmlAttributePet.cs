namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A body model whose XML form carries a member as an attribute with its own name (<c>&lt;pet pet-id="..."&gt;</c>).
/// </summary>
public sealed record XmlAttributePet(
    [property: System.Xml.Serialization.XmlAttribute("pet-id"), Range(1, 10)] int PetId)
{
    public static ValueTask<XmlAttributePet?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
        => ValueTask.FromResult<XmlAttributePet?>(null);
}