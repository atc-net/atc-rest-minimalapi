namespace Demo.Domain.Validators;

/// <summary>
/// The main UpdateUserRequest Validator.
/// </summary>
/// <remarks>
/// Validates the request model (the [FromBody] type of UpdateUserByIdParameters) rather than the parameters record:
/// the ValidationFilter runs validators registered for the [FromBody] type, and FluentValidation's display names
/// then read "First Name" instead of "Request First Name".
/// </remarks>
public sealed partial class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        // Partial update: only validate fields when provided
        When(x => x.Gender is not null, () =>
        {
            RuleFor(x => x.Gender)
                .NotEqual(GenderType.None)
                .WithMessage("Gender must be specified (not None).");
        });

        When(x => x.FirstName is not null, () =>
        {
            RuleFor(x => x.FirstName)
                .Length(2, 10)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage(x => $"{nameof(x.FirstName)} has to start with an uppercase letter.");
        });

        When(x => x.LastName is not null, () =>
        {
            RuleFor(x => x.LastName)
                .Length(2, 30)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage(x => $"{nameof(x.LastName)} has to start with an uppercase letter.");
        });

        When(x => x.WorkAddress is not null, () =>
        {
            RuleFor(x => x.WorkAddress!.CityName)
                .NotEmpty()
                .WithMessage("WorkAddress.CityName is required.")
                .MinimumLength(3)
                .WithMessage("WorkAddress.CityName must be min 3 characters long.")
                .MaximumLength(50)
                .WithMessage("WorkAddress.CityName must be max 50 characters long.");
        });
    }

    [GeneratedRegex("^[A-Z]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnsureFirstCharacterUpperCase();
}