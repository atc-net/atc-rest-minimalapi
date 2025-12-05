namespace Demo.Domain.Validators;

/// <summary>
/// The main UpdateUserRequest Validator.
/// </summary>
public sealed partial class UpdateUserRequestValidator : AbstractValidator<UpdateUserByIdParameters>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request must not be null.");

        // Partial update: only validate fields when provided
        When(x => x.Request.Gender is not null, () =>
        {
            RuleFor(x => x.Request.Gender)
                .NotEqual(GenderType.None)
                .WithMessage("Gender must be specified (not None).");
        });

        When(x => x.Request.FirstName is not null, () =>
        {
            RuleFor(x => x.Request.FirstName)
                .Length(2, 10)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage(x => $"{nameof(x.Request.FirstName)} has to start with an uppercase letter.");
        });

        When(x => x.Request.LastName is not null, () =>
        {
            RuleFor(x => x.Request.LastName)
                .Length(2, 30)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage(x => $"{nameof(x.Request.LastName)} has to start with an uppercase letter.");
        });

        When(x => x.Request.WorkAddress is not null, () =>
        {
            RuleFor(x => x.Request.WorkAddress!.CityName)
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