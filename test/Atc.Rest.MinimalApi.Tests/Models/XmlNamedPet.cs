namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a model from atc-rest-api-source-generator that a JSON and an XML request both carry: the message names a
/// member by its <c>[Display(Name)]</c> (the JSON name), the XML element can have another name.
/// </summary>
public sealed record XmlNamedPet(
    [property: System.ComponentModel.DataAnnotations.Display(Name = "nickName"), System.Xml.Serialization.XmlElement("alias"), StringLength(3)] string? NickName,
    [property: System.ComponentModel.DataAnnotations.Display(Name = "code"), System.Xml.Serialization.XmlElement("code"), MaxLength(2)] string? Code,
    XmlNamedAddress? Address);

/// <summary>
/// A nested model whose member has an XML name and no <c>[Display]</c>: its message names the C# member.
/// </summary>
public sealed record XmlNamedAddress(
    [property: System.Xml.Serialization.XmlElement("town"), StringLength(3)] string? City);

public sealed record XmlNamedPetParameters(
    [property: FromBody, Required] XmlNamedPet Request);