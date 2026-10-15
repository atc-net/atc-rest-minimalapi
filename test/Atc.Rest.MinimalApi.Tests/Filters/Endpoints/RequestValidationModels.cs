// The models of RequestValidationTests: a form model, its parameters record as atc-rest-api-source-generator emits it
// for a form endpoint (the form model has no binding attribute), and the same record for a JSON body.
namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

public sealed record FormAddress(
    [property: Required] string? Street,
    [property: JsonPropertyName("post_code")] string? PostCode);

public sealed record ShipmentForm(
    [property: Required] string? Label,
    FormAddress? Address);

/// <summary>Shaped like a generated form endpoint's parameters record: the form model has no binding attribute.</summary>
public sealed record FormParameters(
    [property: FromRoute(Name = "shipmentId"), Required] string? ShipmentId,
    [property: Required] ShipmentForm? Request);

/// <summary>The same record for a JSON body.</summary>
public sealed record BodyParameters(
    [property: FromRoute(Name = "shipmentId"), Required] string? ShipmentId,
    [property: FromBody, Required] ShipmentForm? Request);

public sealed class ShipmentFormValidator : AbstractValidator<ShipmentForm>
{
    public ShipmentFormValidator()
        => RuleFor(x => x.Label).MinimumLength(2).WithMessage("The label is too short.");
}

public sealed class FormParametersValidator : AbstractValidator<FormParameters>
{
    public FormParametersValidator()
        => RuleFor(x => x.ShipmentId).MinimumLength(2).WithMessage("The shipment id is too short.");
}