namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a body model from atc-rest-api-source-generator when the body is offered as XML: no <c>[FromBody]</c>
/// on the parameters member, and a <c>BindAsync</c> that reads JSON or XML by the request's <c>Content-Type</c>.
/// The XML element name differs from the JSON name only where <c>[XmlElement]</c> says so.
/// </summary>
public sealed record BindAsyncPet(
    [property: Required, MaxLength(10)] string? Name,
    [property: System.Xml.Serialization.XmlElement("surname"), MaxLength(5)] string? LastName = null)
{
    public static async ValueTask<BindAsyncPet?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
    {
        if (context.Request.ContentLength is 0 or null && !context.Request.Headers.TransferEncoding.Any())
        {
            return null;
        }

        if (context.Request.ContentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            var element = await System.Xml.Linq.XElement.LoadAsync(context.Request.Body, System.Xml.Linq.LoadOptions.None, context.RequestAborted);
            return new BindAsyncPet(
                (string?)element.Element("name"),
                (string?)element.Element("surname"));
        }

        return await context.Request.ReadFromJsonAsync<BindAsyncPet>(context.RequestAborted);
    }
}