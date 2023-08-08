namespace Atc.Rest.MinimalApi.Tests.Validators;

public sealed class CreateLocationRequestWithRequestValidator : AbstractValidator<CreateLocationRequestWithRequest>
{
    public CreateLocationRequestWithRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request must not be null.");

        RuleFor(x => x.Request.Telephone)
            .NotNull()
            .Length(2, 10);

        RuleFor(x => x.Request.Address)
            .NotNull();

        RuleFor(x => x.Request.Address.City)
            .NotNull()
            .Length(2, 10)
            .WithMessage("CityName is too short.");
    }
}