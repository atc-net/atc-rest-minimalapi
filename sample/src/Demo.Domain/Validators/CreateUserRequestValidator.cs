namespace Demo.Domain.Validators;

/// <summary>
/// The main CreateUserRequestValidator Validator.
/// </summary>
public sealed partial class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotNull()
            .Length(2, 10)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.FirstName)} has to start with an uppercase letter.");

        RuleFor(x => x.LastName)
            .NotNull()
            .Length(2, 30)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.LastName)} has to start with an uppercase letter.")
            .NotEqual(x => x.FirstName)
            .WithMessage("FirstName must not be equal to LastName.");

        When(x => x.WorkAddress is not null, () =>
        {
            RuleFor(x => x.WorkAddress!.CityName)
                .NotEmpty()
                .WithMessage("WorkAddress.CityName is required.")
                .MinimumLength(3)
                .WithMessage("WorkAddress.CityName must be min 3 characters long.")
                .MaximumLength(20)
                .WithMessage("WorkAddress.CityName must be max 20 characters long.");
        });
    }

    [GeneratedRegex("^[A-Z]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnsureFirstCharacterUpperCase();
}