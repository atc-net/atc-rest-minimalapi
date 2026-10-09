// The request models of the validation behaviour suite. Positional records with [property: ...] targets, as
// atc-rest-api-source-generator emits them, unless a case needs another shape.
namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

// Generator-shaped parameters records: one concrete record per endpoint, whose only member is the JSON body.
public sealed record RequiredParameters(
    [property: FromBody, Required] RequiredRequest Request);

public sealed record AttributesParameters(
    [property: FromBody, Required] AttributesRequest Request);

public sealed record GraphParameters(
    [property: FromBody, Required] GraphRequest Request);

public sealed record ArrayParameters(
    [property: FromBody, Required] ArrayRequest Request);

public sealed record ValidatableParameters(
    [property: FromBody, Required] ValidatableRequest Request);

public sealed record TypeLevelParameters(
    [property: FromBody, Required] TypeLevelRequest Request);

public sealed record NamedParameters(
    [property: FromBody, Required] NamedRequest Request);

public sealed record FluentParameters(
    [property: FromBody, Required] FluentRequest Request);

public sealed record InitOnlyParameters(
    [property: FromBody, Required] InitOnlyRequest Request);

/// <summary>Required, also for an empty and a whitespace-only string.</summary>
public sealed record RequiredRequest(
    [property: Required] string? Name);

/// <summary>One property per attribute. All are optional, so a request fails only on the property it sends.</summary>
public sealed record AttributesRequest(
    [property: StringLength(5)] string? StringLengthMax = null,
    [property: StringLength(5, MinimumLength = 2)] string? StringLengthMinMax = null,
    [property: MinLength(2)] string? MinLengthString = null,
    [property: MaxLength(3)] string? MaxLengthString = null,
    [property: MinLength(2)] IList<int>? MinLengthList = null,
    [property: MaxLength(2)] IList<int>? MaxLengthList = null,
    [property: Range(1, 10)] int? RangeInt = null,
    [property: Range(1L, 10L)] long? RangeLong = null,
    [property: Range(0.5, 1.5)] double? RangeDouble = null,
    [property: Range(typeof(decimal), "0.5", "1.5")] decimal? RangeDecimal = null,
    [property: Range(typeof(DateTime), "2020-01-01", "2020-12-31")] DateTime? RangeDate = null,
    [property: RegularExpression("^[a-z]+$")] string? Pattern = null,
    [property: EmailAddress] string? Email = null,
    [property: Url] string? Website = null,
    [property: Phone] string? PhoneNumber = null,
    string? Password = null,
    [property: Compare("Password")] string? ConfirmPassword = null,
    [property: AllowedValues("red", "green", null)] string? Allowed = null,
    [property: DeniedValues("blue")] string? Denied = null,
    [property: Length(2, 3)] string? LengthString = null,
    [property: Length(2, 3)] IList<int>? LengthList = null,
    [property: Base64String] string? Base64 = null,
    [property: EvenNumber] int? Even = null,
    [property: MaxLength(3, ErrorMessage = "{0} is too long (max {1}).")] string? CustomMessage = null,
    [property: MaxLength(3, ErrorMessageResourceType = typeof(ValidationBehaviourMessages), ErrorMessageResourceName = nameof(ValidationBehaviourMessages.TooLong))] string? ResourceMessage = null,
    [property: System.ComponentModel.DataAnnotations.Display(Name = "Nick name"), MaxLength(3)] string? NickName = null);

/// <summary>A custom property attribute.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EvenNumberAttribute : ValidationAttribute
{
    public EvenNumberAttribute()
        : base("The field {0} must be an even number.")
    {
    }

    public override bool IsValid(object? value)
        => value is not int number || number % 2 == 0;
}

/// <summary>Messages for <c>ErrorMessageResourceType</c>.</summary>
public static class ValidationBehaviourMessages
{
    public static string TooLong => "{0} may have at most {1} characters.";
}

public sealed record GraphRequest(
    [property: Required] GraphAddress? Address,
    GraphAddress? OptionalAddress = null,
    IList<GraphLine>? Lines = null,
    IList<GraphClassLine>? ClassLines = null,
    Dictionary<string, GraphLine>? Map = null,
    GraphNode? Node = null);

/// <summary>An array of objects instead of a list.</summary>
public sealed class ArrayRequest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "An array member is a case of the suite.")]
    public GraphLine[]? Rows { get; init; }
}

public sealed record GraphAddress(
    [property: Required, MaxLength(10)] string? City);

public sealed record GraphLine(
    [property: Required] string? Sku);

/// <summary>A row that is a class (reference equality), next to the record <see cref="GraphLine"/>.</summary>
public sealed class GraphClassLine
{
    [Required]
    public string? Sku { get; init; }
}

/// <summary>A recursive type, for depth.</summary>
public sealed class GraphNode
{
    [MaxLength(3)]
    public string? Name { get; set; }

    public GraphNode? Child { get; set; }
}

/// <summary>A body that binds itself into a cycle (JSON cannot express one): <c>Self</c> refers back to the same object.</summary>
public sealed class CyclicRequest
{
    [MaxLength(3)]
    public string? Name { get; set; }

    public CyclicRequest? Self { get; set; }

    public static async ValueTask<CyclicRequest?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
    {
        var request = await context.Request.ReadFromJsonAsync<CyclicRequest>(context.RequestAborted);
        if (request is not null)
        {
            request.Self = request;
        }

        return request;
    }
}

public sealed record CyclicParameters(
    [property: Required] CyclicRequest Request);

/// <summary>
/// <see cref="IValidatableObject"/> on the root: <c>Min</c> greater than <c>Max</c> names both members; the name
/// <c>all</c> fails the object as a whole (no member names).
/// </summary>
public sealed record ValidatableRequest(
    [property: MaxLength(3)] string? Name = null,
    int? Min = null,
    int? Max = null,
    ValidatableChild? Child = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Min > Max)
        {
            yield return new ValidationResult("Min must not be greater than Max.", [nameof(Min), nameof(Max)]);
        }

        if (Name == "all")
        {
            yield return new ValidationResult("The request as a whole is rejected.");
        }
    }
}

/// <summary><see cref="IValidatableObject"/> on a nested object: a <c>Code</c> starting with <c>bad</c> is rejected.</summary>
public sealed record ValidatableChild(
    [property: MaxLength(3)] string? Code = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Code?.StartsWith("bad", StringComparison.Ordinal) == true)
        {
            yield return new ValidationResult("Code is bad.", [nameof(Code)]);
        }
    }
}

/// <summary>A type-level attribute: <c>Min</c> must not be greater than <c>Max</c>.</summary>
[MinNotGreaterThanMax]
public sealed record TypeLevelRequest(
    int? Min = null,
    int? Max = null,
    [property: MaxLength(3)] string? Name = null);

[AttributeUsage(AttributeTargets.Class)]
public sealed class MinNotGreaterThanMaxAttribute : ValidationAttribute
{
    public MinNotGreaterThanMaxAttribute()
        : base("Min must not be greater than Max.")
    {
    }

    public override bool IsValid(object? value)
        => value is not TypeLevelRequest request || !(request.Min > request.Max);
}

/// <summary>A generator-shaped parameters record with every binding source.</summary>
public sealed record SourcesParameters(
    [property: FromRoute(Name = "id"), Range(1, 100)] int Id,
    [property: FromQuery(Name = "page-size"), Range(1, 50)] int PageSize,
    [property: FromHeader(Name = "x-tenant"), Required, MaxLength(5)] string? Tenant,
    [property: FromQuery(Name = "limit"), Range(1, 10)] int? Limit,
    [property: FromBody, Required] SourcesBody Request);

public sealed record SourcesBody(
    [property: Required, MaxLength(5)] string? FirstName);

/// <summary>A generator-shaped list body.</summary>
public sealed record ListBodyParameters(
    [property: FromBody, Required] IList<GraphLine> Request);

/// <summary><c>[JsonPropertyName]</c> next to a name that follows the naming policy (also validated by <see cref="NamedRequestValidator"/>).</summary>
public sealed record NamedRequest(
    [property: JsonPropertyName("country_code"), MinLength(3)] string? CountryCode = null,
    [property: MaxLength(3)] string? CityName = null);

/// <summary>Validated by DataAnnotations and by FluentValidation (<see cref="FluentRequestValidator"/>).</summary>
public sealed record FluentRequest(
    [property: MaxLength(5)] string? Name = null,
    FluentChild? Child = null,
    IList<string>? Tags = null);

public sealed record FluentChild(
    [property: JsonPropertyName("child_code")] string? Code = null);

public sealed class FluentRequestValidator : AbstractValidator<FluentRequest>
{
    public FluentRequestValidator()
    {
        RuleFor(x => x.Name).NotEqual("taken").WithMessage("Name is taken.");
        RuleFor(x => x.Name).Must(n => n is null || !n.StartsWith("taken", StringComparison.Ordinal)).WithMessage("Name starts with taken.");
        RuleForEach(x => x.Tags).MaximumLength(3);
        RuleFor(x => x.Child!.Code).NotEqual("nested").When(x => x.Child is not null).WithMessage("The nested code is rejected.");
        RuleFor(x => x.Name).NotEqual("alias").OverridePropertyName("Alias").WithMessage("Rejected under an alias.");
        RuleFor(x => x.Name).NotEqual("dots").OverridePropertyName("Extra.Info").WithMessage("Rejected under a dotted alias.");
    }
}

/// <summary>Registered, but only reached when a parent validator calls <c>SetValidator</c>: nested types are not discovered.</summary>
public sealed class FluentChildValidator : AbstractValidator<FluentChild>
{
    public FluentChildValidator()
        => RuleFor(x => x.Code).NotEmpty();
}

/// <summary>A validator for the parameters record itself, next to the body's own validator.</summary>
public sealed class FluentParametersValidator : AbstractValidator<FluentParameters>
{
    public FluentParametersValidator()
        => RuleFor(x => x.Request.Name).NotEqual("wrapper").WithMessage("Rejected by the parameters validator.");
}

/// <summary>An init-only class instead of a positional record.</summary>
public sealed class InitOnlyRequest
{
    [Required]
    public string? Code { get; init; }

    [MaxLength(3)]
    public string? Name { get; init; }
}

public sealed class NamedRequestValidator : AbstractValidator<NamedRequest>
{
    public NamedRequestValidator()
        => RuleFor(x => x.CountryCode).NotEqual("xxx").WithMessage("The country code xxx is reserved.");
}