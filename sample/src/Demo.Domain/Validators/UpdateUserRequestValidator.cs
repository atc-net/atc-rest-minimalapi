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

        RuleFor(x => x.Request.FirstName)
            .NotNull()
            .Length(2, 10)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.Request.FirstName)} has to start with an uppercase letter.");

        RuleFor(x => x.Request.LastName)
            .NotNull()
            .Length(2, 10)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.Request.LastName)} has to start with an uppercase letter.")
            .NotEqual(x => x.Request.FirstName)
            .WithMessage("LastName must not be equal to FirstName.");

        RuleFor(x => x.Request.WorkAddress)
            .NotNull();

        RuleFor(x => x.Request.WorkAddress.CityName)
            .NotNull()
            .Length(2, 10)
            .WithMessage("CityName is too short.");
    }

    [GeneratedRegex("^[A-Z]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnsureFirstCharacterUpperCase();
}